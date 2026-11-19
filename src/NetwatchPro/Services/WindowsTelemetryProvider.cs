using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using NetwatchPro.Models;
using NetwatchPro.ViewModels;
using NetInterface = System.Net.NetworkInformation.NetworkInterface;

namespace NetwatchPro.Services;

/// <summary>
/// Milestone-2 real Windows telemetry. Collects CPU/RAM/disk (kernel APIs), adapter
/// inventory + per-interface throughput (IP Helper via System.Net.NetworkInformation),
/// process inventory (System.Diagnostics.Process), TCP/UDP connection tables with owner
/// PIDs (GetExtendedTcpTable / GetExtendedUdpTable) and active latency/jitter/loss/DNS/
/// connection-establishment probes. No administrator privileges required.
///
/// Every collector degrades to a diagnostic event rather than fabricating data, and the
/// whole provider is labelled LIVE. Attribution that has no direct evidence is marked
/// INFERRED/UNKNOWN instead of overclaimed.
/// </summary>
public sealed class WindowsTelemetryProvider : ITelemetryProvider
{
    private const int TicksPerConnections = 5;
    private const int TicksPerProcesses = 15;
    private const int TicksPerProbe = 10;
    private const int MaxFlows = 1000;

    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer;
    private int _tick;
    private bool _disposed;

    private DateTime _boot;
    private (ulong Idle, ulong Kernel, ulong User) _lastCpu;
    private bool _hasCpuSample;

    private readonly List<NetInterface> _netIfs = new();
    private readonly Dictionary<string, (long Rx, long Tx)> _ifBytes = new();
    private readonly Dictionary<string, string> _addrToInterface = new();
    private readonly Dictionary<string, InterfaceKind> _addrToKind = new();
    private readonly Dictionary<int, string> _pidName = new();
    private DateTime _lastTraffic;

    private readonly Queue<double> _rtt = new();
    private int _pingOk, _pingTotal;
    private bool _probeRunning;
    private IPAddress? _probeTarget;

    private bool _cpuAlert, _latAlert, _lossAlert;
    private IReadOnlyList<AgentSignature>? _agents;

    public SourceState Source => SourceState.Live;

    public WindowsTelemetryProvider(MainViewModel vm)
    {
        _vm = vm;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    public void Start()
    {
        _vm.ResetTelemetry();
        TryRun(CollectStaticInfo, "static machine info");
        DetermineProbeTarget();
        TryRun(CollectInterfaces, "adapter inventory");
        TryRun(CollectSystem, "CPU/RAM/disk");
        TryRun(CollectTraffic, "interface throughput");
        TryRun(CollectProcesses, "process inventory");
        TryRun(CollectConnections, "connection tables");
        TryRun(CollectComponents, "driver/agent inventory");
        TryRun(CollectWfp, "WFP inventory");
        TryRun(CollectProbes, "network probes");
        _vm.GlobalSource = SourceState.Live;
        ApplyInterval();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void ManualRefresh()
    {
        if (_vm.RefreshRateIndex == 4) Tick();
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    // ------------------------------------------------------------------
    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RefreshRateIndex)) ApplyInterval();
    }

    private void ApplyInterval()
    {
        _timer.Interval = _vm.RefreshRateIndex switch
        {
            0 => TimeSpan.FromSeconds(1),
            1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromSeconds(10),
            3 => TimeSpan.FromSeconds(30),
            _ => TimeSpan.FromSeconds(1),
        };
        if (_vm.RefreshRateIndex == 4) _timer.Stop();
        else if (!_timer.IsEnabled) _timer.Start();
    }

    private void Tick()
    {
        _tick++;
        if (_vm.IsPaused) return;

        _vm.TimeDate = DateTime.Now.ToString("ddd yyyy-MM-dd HH:mm:ss");
        _vm.Uptime = (DateTime.Now - _boot).ToString(@"d\d\ hh\:mm\:ss");

        TryRun(CollectSystem, "CPU/RAM/disk");
        TryRun(CollectTraffic, "interface throughput");
        if (_tick % TicksPerConnections == 0) TryRun(CollectConnections, "connection tables");
        if (_tick % TicksPerProcesses == 0) TryRun(CollectProcesses, "process inventory");
        if (_tick % 120 == 0) TryRun(CollectComponents, "driver/agent inventory");
        if (_tick % 300 == 0) TryRun(CollectWfp, "WFP inventory");
        if (_tick % TicksPerProbe == 0) TryRun(CollectProbes, "network probes");
        UpdateHealth();
    }

    private void TryRun(Action action, string what)
    {
        try { action(); }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NETWATCH PRO] collector '{what}' failed: {ex.Message}");
            AddEvent(Severity.Warning, "COLLECTOR", $"{what} unavailable: {ex.Message}", "", "");
        }
    }

    // ------------------- static machine info -------------------
    private void CollectStaticInfo()
    {
        _vm.ComputerName = Environment.MachineName;
        var v = Environment.OSVersion.Version;
        _vm.WinBuild = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        _vm.WinVersion = (v.Major, v.Build) switch
        {
            (10, >= 22000) => "Windows 11",
            (10, _) => "Windows 10",
            _ => $"Windows {v.Major}.{v.Minor}",
        };
        _vm.Architecture = RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            System.Runtime.InteropServices.Architecture.Arm64 => "ARM64",
            _ => RuntimeInformation.OSArchitecture.ToString(),
        };
        _vm.UserName = $"{Environment.UserDomainName}\\{Environment.UserName}";
        try { _vm.RegionTimeZone = TimeZoneInfo.Local.DisplayName; }
        catch { _vm.RegionTimeZone = TimeZoneInfo.Local.Id; }
        _boot = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
    }

    // ------------------- CPU / RAM / disk -------------------
    private void CollectSystem()
    {
        if (WindowsNative.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong i = WindowsNative.ToU64(idle), k = WindowsNative.ToU64(kernel), u = WindowsNative.ToU64(user);
            if (_hasCpuSample)
            {
                ulong id = i - _lastCpu.Idle;
                ulong total = (k - _lastCpu.Kernel) + (u - _lastCpu.User);
                _vm.CpuUsage = total == 0 ? 0 : Math.Clamp(100.0 * (1.0 - (double)id / total), 0, 100);
            }
            _lastCpu = (i, k, u);
            _hasCpuSample = true;
        }

        _vm.CpuTemp = null; // no WMI thermal read in Milestone 2 — display N/A

        var mem = new WindowsNative.MemoryStatusEx { Length = (uint)Marshal.SizeOf<WindowsNative.MemoryStatusEx>() };
        if (WindowsNative.GlobalMemoryStatusEx(ref mem))
        {
            _vm.RamUsage = mem.MemoryLoad;
            double total = mem.TotalPhys / 1024.0 / 1024 / 1024;
            double used = (mem.TotalPhys - mem.AvailPhys) / 1024.0 / 1024 / 1024;
            _vm.SetRamText($"{used:F1} / {total:F1} GB");
        }

        string drive = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        if (WindowsNative.GetDiskFreeSpaceExW(drive, out _, out ulong totalBytes, out ulong freeBytes))
        {
            ulong used = totalBytes - freeBytes;
            _vm.DiskUsage = totalBytes == 0 ? 0 : (double)used / totalBytes * 100.0;
            _vm.SetDiskText($"{used / 1024.0 / 1024 / 1024.0:F0} / {totalBytes / 1024.0 / 1024 / 1024.0:F0} GB");
        }
    }

    // ------------------- adapters -------------------
    private void CollectInterfaces()
    {
        _vm.Interfaces.Clear();
        _netIfs.Clear();
        _ifBytes.Clear();
        _addrToInterface.Clear();
        _addrToKind.Clear();

        foreach (var ni in NetInterface.GetAllNetworkInterfaces())
        {
            _netIfs.Add(ni);
            var kind = ClassifyInterface(ni);

            string mac = "—";
            try { mac = string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2"))); }
            catch { }

            string ipv4 = "—", ipv6 = "—", gw = "—", dns = "—";
            try
            {
                var ip = ni.GetIPProperties();
                var v4 = ip.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (v4 != null)
                {
                    ipv4 = v4.Address.ToString();
                    _addrToInterface[ipv4] = ni.Name;
                    _addrToKind[ipv4] = kind;
                }
                var v6 = ip.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6);
                if (v6 != null)
                {
                    ipv6 = v6.Address.ToString();
                    _addrToInterface[ipv6] = ni.Name;
                    _addrToKind[ipv6] = kind;
                }
                var g = ip.GatewayAddresses.FirstOrDefault();
                if (g != null) gw = g.Address.ToString();
                dns = string.Join(", ", ip.DnsAddresses.Select(a => a.ToString()));
            }
            catch { }

            long rx = 0, tx = 0;
            try { var s = ni.GetIPStatistics(); rx = s.BytesReceived; tx = s.BytesSent; }
            catch { try { var s4 = ni.GetIPv4Statistics(); rx = s4.BytesReceived; tx = s4.BytesSent; } catch { } }
            _ifBytes[ni.Id] = (rx, tx);

            string state = ni.OperationalStatus switch
            {
                OperationalStatus.Up => "Up",
                OperationalStatus.Down => "Down",
                OperationalStatus.Dormant => "Dormant",
                OperationalStatus.Unknown => "Unknown",
                _ => ni.OperationalStatus.ToString(),
            };

            _vm.Interfaces.Add(new NetwatchPro.Models.NetworkInterface
            {
                Id = ni.Id,
                Name = ni.Name,
                Description = ni.Description,
                Type = kind,
                Mac = mac,
                IPv4 = ipv4,
                IPv6 = ipv6,
                Gateway = gw,
                DnsServers = dns,
                LinkSpeedMbps = ni.Speed > 0 ? ni.Speed / 1_000_000.0 : 0,
                State = state,
                Confidence = ConfidenceLevel.Confirmed,
            });
        }

        var active = _vm.Interfaces.FirstOrDefault(i => i.Type == InterfaceKind.Physical && i.State == "Up" && i.Gateway != "—")
                  ?? _vm.Interfaces.FirstOrDefault(i => i.Type == InterfaceKind.Physical && i.State == "Up")
                  ?? _vm.Interfaces.FirstOrDefault();
        _vm.ActiveAdapter = active?.Name ?? "—";
    }

    private static InterfaceKind ClassifyInterface(NetInterface ni) => ni.NetworkInterfaceType switch
    {
        NetworkInterfaceType.Loopback => InterfaceKind.Loopback,
        NetworkInterfaceType.Tunnel => InterfaceKind.Tunnel,
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or
        NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx or
        NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Wman or
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => InterfaceKind.Physical,
        _ => InterfaceKind.Virtual,
    };

    // ------------------- throughput -------------------
    private void CollectTraffic()
    {
        var now = DateTime.UtcNow;
        double dt = _lastTraffic == default ? 1.0 : Math.Max(0.5, (now - _lastTraffic).TotalSeconds);
        _lastTraffic = now;

        double[] catRx = new double[4], catTx = new double[4];
        double totalRx = 0, totalTx = 0;

        foreach (var ni in _netIfs)
        {
            long rx = 0, tx = 0;
            try { var s = ni.GetIPStatistics(); rx = s.BytesReceived; tx = s.BytesSent; }
            catch { continue; }

            double rxRate = 0, txRate = 0;
            if (_ifBytes.TryGetValue(ni.Id, out var prev))
            {
                rxRate = Math.Max(0, (rx - prev.Rx) / dt);
                txRate = Math.Max(0, (tx - prev.Tx) / dt);
            }
            _ifBytes[ni.Id] = (rx, tx);

            var row = _vm.Interfaces.FirstOrDefault(i => i.Id == ni.Id);
            if (row != null) { row.RxRate = rxRate; row.TxRate = txRate; }

            int bucket = (row?.Type ?? ClassifyInterface(ni)) switch
            {
                InterfaceKind.Tunnel => 0,
                InterfaceKind.Loopback => 3,
                _ => 2,
            };
            catRx[bucket] += rxRate;
            catTx[bucket] += txRate;
            totalRx += rxRate;
            totalTx += txRate;
        }

        double[] catTotal = { catRx[0] + catTx[0], catRx[1] + catTx[1], catRx[2] + catTx[2], catRx[3] + catTx[3] };
        double total = totalRx + totalTx;

        _vm.IncomingRate = totalRx;
        _vm.OutgoingRate = totalTx;
        _vm.TotalRate = total;
        _vm.PeakRate = Math.Max(_vm.PeakRate, total);
        _vm.AvgRate = _vm.AvgRate == 0 ? total : _vm.AvgRate * 0.9 + total * 0.1;
        _vm.NetRxRate = totalRx;
        _vm.NetTxRate = totalTx;
        _vm.SetNetText(FormatRate(totalRx), FormatRate(totalTx));
        _vm.SetTrafficText(FormatRate(totalRx), FormatRate(totalTx), FormatRate(total), FormatRate(_vm.PeakRate), FormatRate(_vm.AvgRate));
        _vm.PushTraffic(new TrafficSample
        {
            Timestamp = DateTime.Now,
            PrivateTunnel = catTotal[0],
            WebProxySwg = catTotal[1],
            DirectInternet = catTotal[2],
            OtherUnknown = catTotal[3],
        });
    }

    // ------------------- processes -------------------
    private void CollectProcesses()
    {
        _vm.Processes.Clear();
        _pidName.Clear();
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName;
                    string exe = "";
                    try { exe = p.MainModule?.FileName ?? ""; } catch { }
                    string start = "";
                    try { start = p.StartTime.ToString("HH:mm"); } catch { start = "—"; }
                    _vm.Processes.Add(new ProcessInfo { Pid = p.Id, Name = name, Executable = exe, StartTime = start });
                    _pidName[p.Id] = name;
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
    }

    // ------------------- connections -------------------
    private void CollectConnections()
    {
        _vm.Flows.Clear();
        if (_pidName.Count == 0) CollectProcesses();

        var flows = new List<Flow>();

        try
        {
            foreach (var r in ReadTcpTable())
            {
                if (r.State != WindowsNative.TcpStateEstablished && r.State != WindowsNative.TcpStateListen) continue;
                var f = BuildFlow(r.LocalAddr, r.LocalPort, r.RemoteAddr, r.RemotePort, "TCP", r.State, r.OwningPid);
                if (f != null) flows.Add(f);
            }
        }
        catch (Exception ex) { AddEvent(Severity.Warning, "COLLECTOR", "TCP table unavailable: " + ex.Message, "", ""); }

        try
        {
            foreach (var r in ReadTcp6Table())
            {
                if (r.State != WindowsNative.TcpStateEstablished && r.State != WindowsNative.TcpStateListen) continue;
                var f = BuildFlow6(r);
                if (f != null) flows.Add(f);
            }
        }
        catch (Exception ex) { AddEvent(Severity.Warning, "COLLECTOR", "TCP6 table unavailable: " + ex.Message, "", ""); }

        try
        {
            foreach (var u in ReadUdpTable())
            {
                var f = BuildFlow(u.LocalAddr, u.LocalPort, 0, 0, "UDP", 0, u.OwningPid);
                if (f != null) flows.Add(f);
            }
        }
        catch (Exception ex) { AddEvent(Severity.Warning, "COLLECTOR", "UDP table unavailable: " + ex.Message, "", ""); }

        foreach (var f in flows.OrderBy(f => f.State == ConnectionState.Established ? 0 : 1).Take(MaxFlows))
            _vm.Flows.Add(f);
    }

    private Flow? MakeFlow(IPAddress localIp, IPAddress remoteIp, int lport, int rport, string proto, uint tcpState, uint pid)
    {
        bool isUdp = proto == "UDP";
        bool isLoopback = IPAddress.IsLoopback(localIp) || (!isUdp && IPAddress.IsLoopback(remoteIp));

        string ifName = _addrToInterface.TryGetValue(localIp.ToString(), out var nm) ? nm : "—";
        InterfaceKind kind = _addrToKind.TryGetValue(localIp.ToString(), out var k) ? k : InterfaceKind.Physical;

        string route;
        TrafficCategory category;
        ConfidenceLevel confidence;
        if (isLoopback)
        {
            route = "Loopback";
            category = TrafficCategory.OtherUnknown;
            confidence = ConfidenceLevel.Confirmed;
        }
        else if (isUdp)
        {
            // UDP table has no remote — direction/route genuinely unknown
            route = "—";
            category = TrafficCategory.OtherUnknown;
            confidence = ConfidenceLevel.Unknown;
        }
        else if (kind == InterfaceKind.Tunnel)
        {
            route = "Tunnel";
            category = TrafficCategory.PrivateTunnel;
            confidence = ConfidenceLevel.Inferred;
        }
        else if (IsPrivate(remoteIp))
        {
            route = "Private";
            category = TrafficCategory.PrivateTunnel;
            confidence = ConfidenceLevel.Inferred;
        }
        else
        {
            route = "Default";
            category = TrafficCategory.DirectInternet;
            confidence = ConfidenceLevel.Inferred;
        }

        string procName = _pidName.TryGetValue((int)pid, out var pn) ? pn : GetProcessName((int)pid);

        return new Flow
        {
            ProcessId = (int)pid,
            ProcessName = procName,
            LocalEndpoint = $"{localIp}:{lport}",
            RemoteEndpoint = isUdp ? "—" : $"{remoteIp}:{rport}",
            DestinationIp = isUdp ? "—" : remoteIp.ToString(),
            DestinationHost = isUdp ? "—" : remoteIp.ToString(),
            Port = isUdp ? lport : rport,
            Protocol = proto,
            State = isUdp ? ConnectionState.Unknown : MapTcpState(tcpState),
            InterfaceName = ifName,
            Route = route,
            Category = category,
            Component = AgentFor(procName),
            WfpLayer = "",
            Driver = "",
            Confidence = confidence,
            RxRate = 0,
            TxRate = 0,
        };
    }

    private Flow? BuildFlow(uint localAddr, uint localPort, uint remoteAddr, uint remotePort, string proto, uint tcpState, uint pid)
        => MakeFlow(ToIp(localAddr), ToIp(remoteAddr), ToPort(localPort), ToPort(remotePort), proto, tcpState, pid);

    private Flow? BuildFlow6(WindowsNative.Tcp6RowOwnerPid r)
        => MakeFlow(new IPAddress(r.LocalAddr), new IPAddress(r.RemoteAddr), ToPort(r.LocalPort), ToPort(r.RemotePort), "TCP", r.State, r.OwningPid);

    private static List<WindowsNative.TcpRowOwnerPid> ReadTcpTable()
    {
        var rows = new List<WindowsNative.TcpRowOwnerPid>();
        int size = 0;
        uint ret = WindowsNative.GetExtendedTcpTable(IntPtr.Zero, ref size, false, WindowsNative.AfInet, WindowsNative.TcpTableOwnerPidAll, 0);
        if (ret != 122 /* ERROR_INSUFFICIENT_BUFFER */ || size <= 0) return rows;

        IntPtr buf = Marshal.AllocHGlobal(size);
        try
        {
            ret = WindowsNative.GetExtendedTcpTable(buf, ref size, false, WindowsNative.AfInet, WindowsNative.TcpTableOwnerPidAll, 0);
            if (ret != 0) return rows;
            uint count = (uint)Marshal.ReadInt32(buf);
            int rowSize = Marshal.SizeOf<WindowsNative.TcpRowOwnerPid>();
            for (uint i = 0; i < count; i++)
                rows.Add(Marshal.PtrToStructure<WindowsNative.TcpRowOwnerPid>(IntPtr.Add(buf, 4 + (int)i * rowSize)));
        }
        finally { Marshal.FreeHGlobal(buf); }
        return rows;
    }

    private static List<WindowsNative.UdpRowOwnerPid> ReadUdpTable()
    {
        var rows = new List<WindowsNative.UdpRowOwnerPid>();
        int size = 0;
        uint ret = WindowsNative.GetExtendedUdpTable(IntPtr.Zero, ref size, false, WindowsNative.AfInet, WindowsNative.UdpTableOwnerPid, 0);
        if (ret != 122 || size <= 0) return rows;

        IntPtr buf = Marshal.AllocHGlobal(size);
        try
        {
            ret = WindowsNative.GetExtendedUdpTable(buf, ref size, false, WindowsNative.AfInet, WindowsNative.UdpTableOwnerPid, 0);
            if (ret != 0) return rows;
            uint count = (uint)Marshal.ReadInt32(buf);
            int rowSize = Marshal.SizeOf<WindowsNative.UdpRowOwnerPid>();
            for (uint i = 0; i < count; i++)
                rows.Add(Marshal.PtrToStructure<WindowsNative.UdpRowOwnerPid>(IntPtr.Add(buf, 4 + (int)i * rowSize)));
        }
        finally { Marshal.FreeHGlobal(buf); }
        return rows;
    }

    private static List<WindowsNative.Tcp6RowOwnerPid> ReadTcp6Table()
    {
        var rows = new List<WindowsNative.Tcp6RowOwnerPid>();
        int size = 0;
        uint ret = WindowsNative.GetExtendedTcpTable(IntPtr.Zero, ref size, false, WindowsNative.AfInet6, WindowsNative.TcpTableOwnerPidAll, 0);
        if (ret != 122 /* ERROR_INSUFFICIENT_BUFFER */ || size <= 0) return rows;

        IntPtr buf = Marshal.AllocHGlobal(size);
        try
        {
            ret = WindowsNative.GetExtendedTcpTable(buf, ref size, false, WindowsNative.AfInet6, WindowsNative.TcpTableOwnerPidAll, 0);
            if (ret != 0) return rows;
            uint count = (uint)Marshal.ReadInt32(buf);
            int rowSize = Marshal.SizeOf<WindowsNative.Tcp6RowOwnerPid>();
            for (uint i = 0; i < count; i++)
                rows.Add(Marshal.PtrToStructure<WindowsNative.Tcp6RowOwnerPid>(IntPtr.Add(buf, 4 + (int)i * rowSize)));
        }
        finally { Marshal.FreeHGlobal(buf); }
        return rows;
    }

    // ------------------- components / drivers / agents -------------------
    private static readonly (string Name, string Display, ComponentType Type)[] CoreServices =
    {
        ("BFE", "Base Filtering Engine", ComponentType.WfpProvider),
        ("MpsSvc", "Windows Defender Firewall", ComponentType.WfpProvider),
        ("wfplwfs", "WFP Lightweight Filter", ComponentType.NdisFilter),
        ("tcpip", "TCP/IP Driver", ComponentType.NetworkFilter),
        ("afd", "Ancillary Function Driver for Winsock", ComponentType.NetworkFilter),
        ("NetBT", "NetBIOS over TCP/IP", ComponentType.NetworkFilter),
        ("ndisuio", "NDIS Usermode I/O", ComponentType.NetworkFilter),
    };

    private void CollectComponents()
    {
        _vm.Components.Clear();

        // 1) Real adapters (confirmed) — inventoried in CollectInterfaces
        foreach (var iface in _vm.Interfaces)
        {
            _vm.Components.Add(new Component
            {
                Id = "if-" + iface.Id,
                Name = iface.Name,
                Vendor = VendorFromDescription(iface.Description),
                Type = iface.Type == InterfaceKind.Physical ? ComponentType.PhysicalAdapter : ComponentType.VirtualAdapter,
                Service = "",
                Driver = iface.Description,
                Version = "",
                Status = iface.State,
                Confidence = ConfidenceLevel.Confirmed,
            });
        }

        // Enumerate services + drivers straight from the SCM registry hive (no SCM dependency)
        var svcText = new List<string>();
        try
        {
            using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (root != null)
            {
                foreach (var name in root.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = root.OpenSubKey(name);
                        if (sub == null) continue;
                        int type = sub.GetValue("Type") as int? ?? 0;
                        int start = sub.GetValue("Start") as int? ?? 3;
                        string group = sub.GetValue("Group") as string ?? "";
                        string image = Environment.ExpandEnvironmentVariables(sub.GetValue("ImagePath") as string ?? "");
                        string display = sub.GetValue("DisplayName") as string ?? name;
                        bool isDriver = type is 1 or 2 or 4;

                        svcText.Add(name);
                        svcText.Add(display);
                        if (image.Length > 0) svcText.Add(image);

                        // 2) Core WFP/NDIS stack services + drivers (confirmed)
                        var core = CoreServices.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                        if (core.Name != null)
                        {
                            _vm.Components.Add(new Component
                            {
                                Id = "core-" + core.Name,
                                Name = core.Display,
                                Vendor = "Microsoft",
                                Type = core.Type,
                                Service = name,
                                Driver = image,
                                Version = "",
                                Status = StartLabel(start),
                                Confidence = ConfidenceLevel.Confirmed,
                            });
                        }

                        // 3) Network filter drivers via Group = NDIS/PNP_TDI (confirmed)
                        if (isDriver && group is "NDIS" or "PNP_TDI" or "NDISWAN" or "NetService")
                        {
                            _vm.Components.Add(new Component
                            {
                                Id = "drv-" + name,
                                Name = string.IsNullOrEmpty(display) ? name : display,
                                Vendor = VendorFromDescription(display),
                                Type = group == "NDIS" ? ComponentType.NdisFilter : ComponentType.NetworkFilter,
                                Service = name,
                                Driver = image,
                                Version = "",
                                Status = StartLabel(start),
                                Confidence = ConfidenceLevel.Confirmed,
                            });
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        // 4) Vendor agent discovery (inferred from running processes/services/drivers)
        var agents = _agents ??= AgentSignatures.Load();
        var procNames = new HashSet<string>(_vm.Processes.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var allText = string.Join(" ", svcText);

        foreach (var agent in agents)
        {
            bool matched = false;
            foreach (var pat in agent.Patterns)
            {
                if (procNames.Any(p => p.Contains(pat, StringComparison.OrdinalIgnoreCase))) { matched = true; break; }
                if (allText.Contains(pat, StringComparison.OrdinalIgnoreCase)) { matched = true; break; }
            }
            if (!matched) continue;
            _vm.Components.Add(new Component
            {
                Id = "agent-" + agent.Name,
                Name = agent.Name,
                Vendor = agent.Vendor,
                Type = agent.ComponentType,
                Service = "",
                Driver = "",
                Version = "",
                Status = "Detected",
                Confidence = ConfidenceLevel.Inferred,
            });
        }
    }

    private static string StartLabel(int start) => start switch
    {
        0 => "Boot",
        1 => "System",
        2 => "Auto",
        3 => "Manual",
        4 => "Disabled",
        _ => "Unknown",
    };

    private void CollectWfp()
    {
        var result = WfpInventory.Collect();
        if (!result.Ok)
        {
            AddEvent(Severity.Info, "WFP", result.Error.Length > 0 ? result.Error : "WFP inventory unavailable.", "", "");
            return;
        }

        _vm.WfpObjects.Clear();
        foreach (var o in result.Objects) _vm.WfpObjects.Add(o);

        _vm.WfpLayers.Clear();
        foreach (var l in result.Layers) _vm.WfpLayers.Add(l);
    }

    private string AgentFor(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return "";
        var agents = _agents ??= AgentSignatures.Load();
        foreach (var a in agents)
            foreach (var pat in a.Patterns)
                if (processName.Contains(pat, StringComparison.OrdinalIgnoreCase))
                    return a.Name;
        return "";
    }

    private static string VendorFromDescription(string desc)
    {
        if (string.IsNullOrEmpty(desc)) return "—";
        var d = desc.ToLowerInvariant();
        if (d.Contains("realtek")) return "Realtek";
        if (d.Contains("intel")) return "Intel";
        if (d.Contains("broadcom")) return "Broadcom";
        if (d.Contains("qualcomm") || d.Contains("atheros")) return "Qualcomm";
        if (d.Contains("mellanox")) return "NVIDIA (Mellanox)";
        if (d.Contains("marvell")) return "Marvell";
        if (d.Contains("microsoft")) return "Microsoft";
        if (d.Contains("vmware")) return "VMware";
        if (d.Contains("virtualbox")) return "Oracle";
        if (d.Contains("cisco")) return "Cisco";
        if (d.Contains("zscaler")) return "Zscaler";
        return "—";
    }

    // ------------------- active probes -------------------
    private void DetermineProbeTarget()
    {
        foreach (var ni in _netIfs)
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            try
            {
                var g = ni.GetIPProperties().GatewayAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (g != null) { _probeTarget = g.Address; return; }
            }
            catch { }
        }
        _probeTarget = IPAddress.Parse("1.1.1.1");
    }

    private void CollectProbes()
    {
        if (_probeRunning) return;
        _probeRunning = true;
        var target = _probeTarget ?? IPAddress.Parse("1.1.1.1");

        _ = Task.Run(() =>
        {
            try
            {
                double? rtt = null;
                try
                {
                    using var ping = new Ping();
                    var reply = ping.Send(target, 1000);
                    if (reply.Status == IPStatus.Success) rtt = reply.RoundtripTime;
                }
                catch { }

                double? dnsMs = null;
                var sw = Stopwatch.StartNew();
                try { Dns.GetHostAddresses("example.com"); dnsMs = sw.Elapsed.TotalMilliseconds; }
                catch { }
                sw.Stop();

                double? connMs = null;
                sw.Restart();
                try
                {
                    using var tcp = new TcpClient();
                    if (tcp.ConnectAsync(target, 443).Wait(2000) && tcp.Connected)
                        connMs = sw.Elapsed.TotalMilliseconds;
                }
                catch { }
                sw.Stop();

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (!_disposed) ApplyProbeResult(rtt, dnsMs, connMs);
                });
            }
            catch { }
            finally { _probeRunning = false; }
        });
    }

    private void ApplyProbeResult(double? rtt, double? dnsMs, double? connMs)
    {
        _pingTotal++;
        if (rtt is double r)
        {
            _pingOk++;
            _rtt.Enqueue(r);
            while (_rtt.Count > 20) _rtt.Dequeue();
            _vm.AvgLatency = _rtt.Average();
            _vm.MaxLatency = _rtt.Max();
            _vm.Jitter = StdDev(_rtt);
        }
        _vm.PacketLoss = _pingTotal == 0 ? 0 : 100.0 * (_pingTotal - _pingOk) / _pingTotal;
        if (dnsMs is double d) _vm.DnsTime = d;
        if (connMs is double c) _vm.ConnEstTime = c;
        _vm.Availability = 100.0 * (_pingTotal == 0 ? 0 : (double)_pingOk / _pingTotal);
        _vm.SetLatencyText(
            $"{_vm.AvgLatency:F0} ms", $"{_vm.MaxLatency:F0} ms", $"{_vm.Jitter:F1} ms",
            $"{_vm.PacketLoss:F2}%", $"{_vm.DnsTime:F0} ms", $"{_vm.ConnEstTime:F0} ms", $"{_vm.Availability:F2}%");
        _vm.PushLatency(new LatencySample
        {
            Timestamp = DateTime.Now,
            LatencyMs = _vm.AvgLatency,
            JitterMs = _vm.Jitter,
            PacketLoss = _vm.PacketLoss,
        });
    }

    // ------------------- health / alerts -------------------
    private void UpdateHealth()
    {
        bool over = _vm.CpuUsage > _vm.CpuThreshold
                 || _vm.AvgLatency > _vm.LatencyThreshold
                 || _vm.PacketLoss > _vm.PacketLossThreshold;
        _vm.OverallHealth = over ? "DEGRADED" : "NOMINAL";

        SetAlert(ref _cpuAlert, _vm.CpuUsage > _vm.CpuThreshold, "CPU", $"CPU {_vm.CpuUsage:F0}% exceeds threshold {_vm.CpuThreshold:F0}%");
        SetAlert(ref _latAlert, _vm.AvgLatency > _vm.LatencyThreshold, "LATENCY", $"Latency {_vm.AvgLatency:F0} ms exceeds threshold {_vm.LatencyThreshold:F0} ms");
        SetAlert(ref _lossAlert, _vm.PacketLoss > _vm.PacketLossThreshold, "NETWORK", $"Packet loss {_vm.PacketLoss:F2}% exceeds threshold {_vm.PacketLossThreshold:F2}%");

        _vm.AlertActive = over || _vm.AlertCount > 0;
    }

    private void SetAlert(ref bool flag, bool over, string src, string desc)
    {
        if (over && !flag)
        {
            flag = true;
            AddEvent(Severity.Warning, src, desc, "", "");
            _vm.AlertCount = Math.Min(_vm.AlertCount + 1, 99);
        }
        else if (!over)
        {
            flag = false;
        }
    }

    private void AddEvent(Severity sev, string src, string desc, string entity, string proc)
    {
        _vm.Events.Insert(0, new DiagnosticEvent
        {
            Timestamp = DateTime.Now,
            Severity = sev,
            Source = src,
            Description = desc,
            RelatedEntity = entity,
            RelatedProcess = proc,
        });
        while (_vm.Events.Count > 200) _vm.Events.RemoveAt(_vm.Events.Count - 1);
    }

    // ------------------- helpers -------------------
    private string GetProcessName(int pid)
    {
        if (_pidName.TryGetValue(pid, out var n)) return n;
        try { using var p = Process.GetProcessById(pid); var name = p.ProcessName; _pidName[pid] = name; return name; }
        catch { return $"pid:{pid}"; }
    }

    private static IPAddress ToIp(uint net) => new(BitConverter.GetBytes(net));

    private static int ToPort(uint net) => (int)(((net & 0xFF) << 8) | ((net >> 8) & 0xFF));

    private static ConnectionState MapTcpState(uint state) => state switch
    {
        2 => ConnectionState.Listening,
        3 => ConnectionState.SynSent,
        4 => ConnectionState.SynReceived,
        5 => ConnectionState.Established,
        6 or 7 or 9 or 10 => ConnectionState.FinWait,
        8 => ConnectionState.CloseWait,
        11 => ConnectionState.TimeWait,
        _ => ConnectionState.Unknown,
    };

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254);
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return true; // fc00::/7 ULA
            if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80) return true; // fe80::/10 link-local
        }
        return false;
    }

    private static double StdDev(IEnumerable<double> vals)
    {
        var arr = vals.ToArray();
        if (arr.Length < 2) return 0;
        double mean = arr.Average();
        return Math.Sqrt(arr.Sum(v => (v - mean) * (v - mean)) / (arr.Length - 1));
    }

    private static string FormatRate(double bps) => bps switch
    {
        >= 1_000_000_000 => $"{bps / 1_000_000_000:F2} GB/s",
        >= 1_000_000 => $"{bps / 1_000_000:F1} MB/s",
        >= 1_000 => $"{bps / 1_000:F0} KB/s",
        _ => $"{bps:F0} B/s",
    };
}
