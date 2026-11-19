using System.Windows.Threading;
using NetwatchPro.Models;
using NetwatchPro.ViewModels;

namespace NetwatchPro.Services;

/// <summary>
/// Milestone-1 simulated telemetry. Produces clearly-labelled SIMULATED data with
/// realistic random-walk dynamics, vendor-flavoured component inventory, WFP
/// objects, flows and occasional diagnostic events. No real collection occurs.
/// </summary>
public sealed class SimulatedTelemetryProvider : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly Random _rng = new();
    private readonly DispatcherTimer _timer;
    private readonly DateTime _boot = DateTime.Now.AddDays(-3).AddHours(-7);
    private long _captureBytes;

    // random-walk state
    private double _cpu = 28, _ram = 44, _disk = 61;
    private double _latency = 34, _jitter = 6, _loss = 0.12, _dns = 11, _conn = 24;
    private double _avail = 99.97;
    private double[] _cat = new double[4]; // private, proxy, direct, other (B/s)
    private int _tickCount;

    private static readonly string[] Hosts =
    {
        "cdn.cloudflare.com", "api.github.com", "login.microsoftonline.com",
        "graph.microsoft.com", "www.rymantech.com", "sydney1.corp.internal",
        "mel01.tunnel.secure", "edge.azureedge.net", "ocsp.digicert.com",
        "dns.google", "1.1.1.1", "update.contoso.net", "proxy.swg.gateway",
        "vpn-gw.corp.local", "telemetry.example.net",
    };

    public SimulatedTelemetryProvider(MainViewModel vm)
    {
        _vm = vm;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.RefreshRateIndex)) ApplyInterval();
        };
    }

    public void Start()
    {
        Seed();
        ApplyInterval();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void ManualRefresh()
    {
        if (_vm.RefreshRateIndex == RefreshRatesManual) Tick();
    }

    private const int RefreshRatesManual = 4;

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
        if (_vm.RefreshRateIndex == RefreshRatesManual) _timer.Stop();
        else if (!_timer.IsEnabled) _timer.Start();
    }

    private void Tick()
    {
        _tickCount++;
        if (_vm.IsPaused) return;

        _vm.TimeDate = DateTime.Now.ToString("ddd yyyy-MM-dd HH:mm:ss");
        _vm.Uptime = (DateTime.Now - _boot).ToString(@"d\d\ hh\:mm\:ss");

        UpdateSystem();
        UpdateTraffic();
        UpdateLatency();
        UpdateFlows();
        UpdateCapture();
        UpdateAlerts();
    }

    private void UpdateSystem()
    {
        _cpu = Walk(_cpu, 4.5, 4, 98, 30);
        _ram = Walk(_ram, 2.2, 20, 92, 46);
        _disk = Walk(_disk, 0.08, 40, 95, 62);
        _vm.CpuUsage = _cpu;
        _vm.CpuTemp = Math.Round(38 + _cpu * 0.45 + (_rng.NextDouble() - 0.5) * 3, 1);
        _vm.RamUsage = _ram;
        _vm.DiskUsage = _disk;

        var totalRam = 31.7;
        _vm.SetRamText($"{(_ram / 100 * totalRam):F1} / {totalRam:F1} GB");
        var totalDisk = 473.0;
        _vm.SetDiskText($"{(_disk / 100 * totalDisk):F0} / {totalDisk:F0} GB");
        _vm.SetNetText(FormatRate(_vm.NetRxRate), FormatRate(_vm.NetTxRate));

        foreach (var itf in _vm.Interfaces)
        {
            itf.RxRate = Walk(itf.RxRate, itf.RxRate * 0.12, 0, 120_000_000, 8_000_000);
            itf.TxRate = Walk(itf.TxRate, itf.TxRate * 0.12, 0, 60_000_000, 3_000_000);
        }
    }

    private void UpdateTraffic()
    {
        for (int i = 0; i < 4; i++)
        {
            double baseRate = i switch { 0 => 900_000, 1 => 1_600_000, 2 => 7_500_000, _ => 420_000 };
            _cat[i] = Walk(_cat[i], baseRate * 0.18, 20_000, 42_000_000, baseRate);
        }

        // occasional burst
        if (_rng.NextDouble() < 0.04) _cat[2] *= 2.6;

        var total = _cat.Sum();
        _vm.IncomingRate = _cat[0] * 0.4 + _cat[1] * 0.5 + _cat[2] * 0.55 + _cat[3] * 0.4;
        _vm.OutgoingRate = total - _vm.IncomingRate;
        _vm.TotalRate = total;
        _vm.PeakRate = Math.Max(_vm.PeakRate, total);
        _vm.AvgRate = _vm.AvgRate == 0 ? total : _vm.AvgRate * 0.9 + total * 0.1;
        _vm.NetRxRate = _vm.IncomingRate;
        _vm.NetTxRate = _vm.OutgoingRate;

        _vm.SetTrafficText(
            FormatRate(_vm.IncomingRate), FormatRate(_vm.OutgoingRate),
            FormatRate(_vm.TotalRate), FormatRate(_vm.PeakRate), FormatRate(_vm.AvgRate));

        _vm.PushTraffic(new TrafficSample
        {
            Timestamp = DateTime.Now,
            PrivateTunnel = _cat[0],
            WebProxySwg = _cat[1],
            DirectInternet = _cat[2],
            OtherUnknown = _cat[3],
        });
    }

    private void UpdateLatency()
    {
        _latency = Walk(_latency, 3.5, 12, 420, 34);
        _jitter = Walk(_jitter, 1.1, 1, 60, 6);
        _loss = Walk(_loss, 0.05, 0, 4.2, 0.12);
        _dns = Walk(_dns, 1.6, 4, 160, 11);
        _conn = Walk(_conn, 2.4, 6, 300, 24);
        _avail = Walk(_avail, 0.01, 99.0, 100, 99.98);

        _vm.AvgLatency = _latency;
        _vm.MaxLatency = _latency * 1.8;
        _vm.Jitter = _jitter;
        _vm.PacketLoss = _loss;
        _vm.DnsTime = _dns;
        _vm.ConnEstTime = _conn;
        _vm.Availability = _avail;

        _vm.SetLatencyText(
            $"{_latency:F0} ms", $"{_latency * 1.8:F0} ms", $"{_jitter:F1} ms",
            $"{_loss:F2}%", $"{_dns:F0} ms", $"{_conn:F0} ms", $"{_avail:F2}%");

        _vm.PushLatency(new LatencySample
        {
            Timestamp = DateTime.Now,
            LatencyMs = _latency,
            JitterMs = _jitter,
            PacketLoss = _loss,
        });
    }

    private void UpdateFlows()
    {
        foreach (var f in _vm.Flows)
        {
            f.RxRate = Walk(f.RxRate, f.RxRate * 0.16, 200, 40_000_000, 400_000);
            f.TxRate = Walk(f.TxRate, f.TxRate * 0.16, 100, 18_000_000, 180_000);
            f.RxBytes += (long)(f.RxRate * 1);
            f.TxBytes += (long)(f.TxRate * 1);
        }

        // occasional new event
        if (_rng.NextDouble() < 0.18)
        {
            AddEvent();
            _vm.AlertCount = Math.Min(_vm.AlertCount + 1, 99);
            _vm.AlertActive = true;
        }
    }

    private void UpdateCapture()
    {
        if (_vm.Capture.State == "Capturing")
        {
            _captureBytes += (long)(_vm.TotalRate * 1);
            _vm.Capture.SizeBytes = _captureBytes;
        }
    }

    private void UpdateAlerts()
    {
        bool over = _cpu > _vm.CpuThreshold || _latency > _vm.LatencyThreshold || _loss > _vm.PacketLossThreshold;
        _vm.OverallHealth = over ? "DEGRADED" : "NOMINAL";
        _vm.AlertActive = over || _vm.AlertCount > 0;
    }

    private void AddEvent()
    {
        var templates = new (Severity s, string src, string desc)[]
        {
            (Severity.Warning, "WFP", "Packet drop observed at ALE_AUTH_CONNECT_V4"),
            (Severity.Warning, "DNS", "DNS query timeout to 8.8.8.8 (1200 ms)"),
            (Severity.Info, "ADAPTER", "Interface 'Ethernet 2' link renegotiated to 1 Gbps"),
            (Severity.Warning, "TUNNEL", "Tunnel MTU mismatch on Private/Tunnel path"),
            (Severity.Critical, "DRIVER", "NDIS filter 'ndisfw' reported reset"),
            (Severity.Info, "TCP", "High retransmission rate on direct internet flow"),
        };
        var t = templates[_rng.Next(templates.Length)];
        var host = Hosts[_rng.Next(Hosts.Length)];
        _vm.Events.Insert(0, new DiagnosticEvent
        {
            Timestamp = DateTime.Now,
            Severity = t.s,
            Source = t.src,
            Description = t.desc,
            RelatedEntity = host,
            RelatedProcess = _rng.NextDouble() < 0.5 ? "chrome.exe" : "svchost.exe",
        });
        while (_vm.Events.Count > 200) _vm.Events.RemoveAt(_vm.Events.Count - 1);
    }

    private double Walk(double cur, double step, double min, double max, double anchor)
    {
        var pull = (anchor - cur) * 0.06;
        var n = cur + pull + (_rng.NextDouble() - 0.5) * 2 * step;
        return Math.Clamp(n, min, max);
    }

    private static string FormatRate(double bps) => bps switch
    {
        >= 1_000_000_000 => $"{bps / 1_000_000_000:F2} GB/s",
        >= 1_000_000 => $"{bps / 1_000_000:F1} MB/s",
        >= 1_000 => $"{bps / 1_000:F0} KB/s",
        _ => $"{bps:F0} B/s",
    };

    // ---------------- seeding ----------------
    private void Seed()
    {
        SeedInterfaces();
        SeedComponents();
        SeedWfp();
        SeedProcesses();
        SeedFlows();
        SeedEvents();
    }

    private void SeedInterfaces()
    {
        _vm.Interfaces.Clear();
        _vm.Interfaces.Add(new NetworkInterface { Id = "if0", Name = "Ethernet 2", Description = "Realtek PCIe GbE Family Controller", Type = InterfaceKind.Physical, Mac = "A4:BB:6D:11:22:33", IPv4 = "192.168.1.24", IPv6 = "fe80::a4bb:6dff:fe11:2233", Gateway = "192.168.1.1", DnsServers = "8.8.8.8, 1.1.1.1", LinkSpeedMbps = 1000, State = "Up", Confidence = ConfidenceLevel.Confirmed });
        _vm.Interfaces.Add(new NetworkInterface { Id = "if1", Name = "Wi-Fi", Description = "Intel Wi-Fi 6 AX201", Type = InterfaceKind.Physical, Mac = "D8:3A:DD:44:55:66", IPv4 = "—", IPv6 = "—", Gateway = "—", DnsServers = "—", LinkSpeedMbps = 0, State = "Disconnected", SignalStrength = "—", Confidence = ConfidenceLevel.Confirmed });
        _vm.Interfaces.Add(new NetworkInterface { Id = "if2", Name = "Private Access Tunnel", Description = "ZTNA client virtual adapter", Type = InterfaceKind.Tunnel, Mac = "00:FF:9C:01:02:03", IPv4 = "10.90.2.17", IPv6 = "—", Gateway = "10.90.2.1", DnsServers = "10.90.2.53", LinkSpeedMbps = 100, State = "Up", Confidence = ConfidenceLevel.Inferred });
        _vm.Interfaces.Add(new NetworkInterface { Id = "if3", Name = "Loopback", Description = "Microsoft KM-TEST Loopback Adapter", Type = InterfaceKind.Loopback, Mac = "—", IPv4 = "127.0.0.1", IPv6 = "::1", Gateway = "—", DnsServers = "—", LinkSpeedMbps = 1073741824, State = "Up", Confidence = ConfidenceLevel.Confirmed });
        _vm.ActiveAdapter = "Ethernet 2";
    }

    private void SeedComponents()
    {
        _vm.Components.Clear();
        void Add(string id, string name, string vendor, ComponentType t, string svc, string drv, string ver, double traffic, ConfidenceLevel c) =>
            _vm.Components.Add(new Component { Id = id, Name = name, Vendor = vendor, Type = t, Service = svc, Driver = drv, Version = ver, Status = "Running", AssociatedTraffic = traffic, Confidence = c });

        Add("c0", "Global Secure Access Client", "Microsoft", ComponentType.WfpCallout, "gsa", "gsacallout.sys", "1.2.304.0", 1.1, ConfidenceLevel.Inferred);
        Add("c1", "Umbrella Roaming Client", "Cisco", ComponentType.ProxyService, "umbrella", "dnscryptproxy.sys", "3.0.402", 1.9, ConfidenceLevel.Inferred);
        Add("c2", "Zscaler Client Connector", "Zscaler", ComponentType.VpnClient, "zsatunnel", "zsatunnel.sys", "4.3.0.81", 0.9, ConfidenceLevel.Inferred);
        Add("c3", "WFP Base Filtering Engine", "Microsoft", ComponentType.WfpProvider, "BFE", "—", "10.0.22631", 0, ConfidenceLevel.Confirmed);
        Add("c4", "TCP/IP Driver", "Microsoft", ComponentType.NetworkFilter, "Tcpip", "tcpip.sys", "10.0.22631", 12.4, ConfidenceLevel.Confirmed);
        Add("c5", "NDIS Lightweight Filter", "Microsoft", ComponentType.NdisFilter, "ndis", "ndis.sys", "10.0.22631", 12.4, ConfidenceLevel.Confirmed);
        Add("c6", "Realtek GbE Controller", "Realtek", ComponentType.PhysicalAdapter, "rt640x64", "rt640x64.sys", "10.68.315.2022", 12.4, ConfidenceLevel.Confirmed);
        Add("c7", "Kaspersky Network Filter", "Kaspersky", ComponentType.NdisFilter, "klwfp", "klwfp.sys", "21.3.10", 1.2, ConfidenceLevel.Inferred);
        Add("c8", "DNSCrypt Proxy", "Cisco", ComponentType.DnsClient, "dnscrypt", "dnscrypt-proxy.exe", "2.1.5", 0.4, ConfidenceLevel.Inferred);
        Add("c9", "Hyper-V Virtual Switch", "Microsoft", ComponentType.VirtualAdapter, "vmswitch", "vmsif.sys", "10.0.22631", 0.2, ConfidenceLevel.Confirmed);
    }

    private void SeedWfp()
    {
        _vm.WfpObjects.Clear();
        void Add(WfpObjectKind k, string id, string name, string provider, string sublayer, string layer, string action, string callout, int ev, int perm, int block, int drop) =>
            _vm.WfpObjects.Add(new WfpObject { Kind = k, Id = id, Name = name, Provider = provider, Sublayer = sublayer, Layer = layer, Action = action, Callout = callout, EventCount = ev, Permitted = perm, Blocked = block, Dropped = drop });

        Add(WfpObjectKind.Provider, "p0", "Microsoft BFE", "Microsoft", "—", "—", "—", "—", 0, 0, 0, 0);
        Add(WfpObjectKind.Provider, "p1", "GSA Provider", "Microsoft GSA", "—", "—", "—", "—", 0, 0, 0, 0);
        Add(WfpObjectKind.Provider, "p2", "Umbrella WFP", "Cisco", "—", "—", "—", "—", 0, 0, 0, 0);
        Add(WfpObjectKind.Sublayer, "s0", "BFE Sublayer", "Microsoft BFE", "—", "—", "—", "—", 0, 0, 0, 0);
        Add(WfpObjectKind.Sublayer, "s1", "GSA Sublayer", "Microsoft GSA", "—", "—", "—", "—", 0, 0, 0, 0);
        Add(WfpObjectKind.Filter, "f0", "Block outbound malware", "Umbrella WFP", "SWG Sublayer", "ALE_AUTH_CONNECT_V4", "Block", "inspect-callout", 12, 340, 9, 0);
        Add(WfpObjectKind.Filter, "f1", "Permit private route", "GSA Provider", "GSA Sublayer", "ALE_CONNECT_REDIRECT_V4", "Permit", "redirect-callout", 980, 980, 0, 0);
        Add(WfpObjectKind.Filter, "f2", "Inspect HTTPS", "Umbrella WFP", "SWG Sublayer", "STREAM_V4", "Callout", "ssl-inspect", 2200, 2190, 10, 0);
        Add(WfpObjectKind.Filter, "f3", "Default permit", "Microsoft BFE", "BFE Sublayer", "ALE_AUTH_RECV_ACCEPT_V4", "Permit", "—", 4100, 4100, 0, 0);
        Add(WfpObjectKind.Filter, "f4", "Drop spoofed", "Microsoft BFE", "BFE Sublayer", "IP_PACKET_V4", "Block", "—", 77, 0, 77, 3);
        Add(WfpObjectKind.Callout, "k0", "redirect-callout", "GSA Provider", "GSA Sublayer", "ALE_CONNECT_REDIRECT_V4", "Redirect", "—", 980, 0, 0, 0);
        Add(WfpObjectKind.Callout, "k1", "inspect-callout", "Umbrella WFP", "SWG Sublayer", "ALE_AUTH_CONNECT_V4", "Inspect", "—", 12, 0, 0, 0);
        Add(WfpObjectKind.Callout, "k2", "ssl-inspect", "Umbrella WFP", "SWG Sublayer", "STREAM_V4", "Inspect", "—", 2200, 0, 0, 0);
        Add(WfpObjectKind.Layer, "l0", "ALE_AUTH_CONNECT_V4", "—", "—", "—", "—", "—", 380, 0, 0, 0);
        Add(WfpObjectKind.Layer, "l1", "ALE_CONNECT_REDIRECT_V4", "—", "—", "—", "—", "—", 980, 0, 0, 0);
        Add(WfpObjectKind.Layer, "l2", "STREAM_V4", "—", "—", "—", "—", "—", 2200, 0, 0, 0);
        Add(WfpObjectKind.Layer, "l3", "IP_PACKET_V4", "—", "—", "—", "—", "—", 77, 0, 0, 0);
        foreach (var o in _vm.WfpObjects.Where(o => o.Kind == WfpObjectKind.Layer))
            _vm.WfpLayers.Add(o);
    }

    private void SeedProcesses()
    {
        _vm.Processes.Clear();
        void Add(int pid, string name, string exe) => _vm.Processes.Add(new ProcessInfo { Pid = pid, Name = name, Executable = exe, StartTime = _boot.AddMinutes(_rng.Next(0, 4000)).ToString("HH:mm") });
        Add(4, "System", "System");
        Add(812, "svchost.exe", @"C:\Windows\System32\svchost.exe");
        Add(1024, "chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe");
        Add(1176, "chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe");
        Add(1290, "msedge.exe", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe");
        Add(1420, "outlook.exe", @"C:\Program Files\Microsoft Office\root\Office16\OUTLOOK.EXE");
        Add(1610, "Teams.exe", @"C:\Users\operator\AppData\Local\Microsoft\Teams\Teams.exe");
        Add(1788, "OneDrive.exe", @"C:\Users\operator\AppData\Local\Microsoft\OneDrive\OneDrive.exe");
        Add(1940, "gsaclient.exe", @"C:\Program Files\Microsoft GSA\gsaclient.exe");
        Add(2010, "umbrella.exe", @"C:\Program Files\Cisco\Umbrella\umbrella.exe");
        Add(2150, "zscaler.exe", @"C:\Program Files\Zscaler\zscaler.exe");
        Add(2280, "spotify.exe", @"C:\Users\operator\AppData\Roaming\Spotify\Spotify.exe");
    }

    private void SeedFlows()
    {
        _vm.Flows.Clear();
        var cats = new[] { TrafficCategory.DirectInternet, TrafficCategory.DirectInternet, TrafficCategory.WebProxySwg, TrafficCategory.PrivateTunnel, TrafficCategory.OtherUnknown };
        var comps = new[] { "umbrella.exe", "zscaler.exe", "gsaclient.exe", "chrome.exe", "svchost.exe" };
        var drivers = new[] { "tcpip.sys", "klwfp.sys", "gsacallout.sys", "dnscryptproxy.sys", "—" };
        var routes = new[] { "Default", "Tunnel", "Proxy", "Private", "Loopback" };
        var wfpLayers = new[] { "STREAM_V4", "ALE_AUTH_CONNECT_V4", "ALE_CONNECT_REDIRECT_V4", "IP_PACKET_V4", "—" };

        for (int i = 0; i < 36; i++)
        {
            var host = Hosts[_rng.Next(Hosts.Length)];
            var cat = cats[_rng.Next(cats.Length)];
            var port = cat switch
            {
                TrafficCategory.WebProxySwg => 443,
                TrafficCategory.PrivateTunnel => 8443,
                _ => _rng.Next(2) == 0 ? 443 : 80,
            };
            var confidence = _rng.Next(3) switch { 0 => ConfidenceLevel.Confirmed, 1 => ConfidenceLevel.Inferred, _ => ConfidenceLevel.Unknown };

            _vm.Flows.Add(new Flow
            {
                ProcessId = _vm.Processes[_rng.Next(_vm.Processes.Count)].Pid,
                ProcessName = comps[_rng.Next(comps.Length)],
                LocalEndpoint = $"192.168.1.24:{50000 + _rng.Next(15000)}",
                RemoteEndpoint = $"{host}:{port}",
                DestinationHost = host,
                DestinationIp = $"10.{_rng.Next(10, 250)}.{_rng.Next(0, 255)}.{_rng.Next(2, 254)}",
                Port = port,
                Protocol = _rng.Next(5) == 0 ? "UDP" : "TCP",
                State = (ConnectionState)_rng.Next(1, 4),
                InterfaceName = cat == TrafficCategory.PrivateTunnel ? "Private Access Tunnel" : "Ethernet 2",
                Route = routes[_rng.Next(routes.Length)],
                Category = cat,
                Component = comps[_rng.Next(comps.Length)],
                WfpLayer = wfpLayers[_rng.Next(wfpLayers.Length)],
                Driver = drivers[_rng.Next(drivers.Length)],
                Confidence = confidence,
                RxRate = _rng.Next(500, 4_000_000),
                TxRate = _rng.Next(100, 1_200_000),
            });
        }
    }

    private void SeedEvents()
    {
        _vm.Events.Clear();
        for (int i = 0; i < 10; i++) AddEvent();
    }

    public void Dispose() => _timer.Stop();
}
