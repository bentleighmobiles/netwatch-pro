using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetwatchPro.Models;

/// <summary>Observable base for live-updating value objects.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>Single scalar telemetry sample with full provenance.</summary>
public sealed class SystemMetric : ObservableObject
{
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
    public SourceState Source { get; set; } = SourceState.Simulated;

    private string _display = "";
    public string Display
    {
        get => _display;
        set => Set(ref _display, value);
    }
}

public sealed class NetworkInterface : ObservableObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public InterfaceKind Type { get; set; } = InterfaceKind.Physical;
    public string Mac { get; set; } = "";
    public string IPv4 { get; set; } = "";
    public string IPv6 { get; set; } = "";
    public string Gateway { get; set; } = "";
    public string DnsServers { get; set; } = "";
    public double LinkSpeedMbps { get; set; }
    public double RxRate { get; set; }
    public double TxRate { get; set; }
    public string State { get; set; } = "Up";
    public string? SignalStrength { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Confirmed;

    public string TypeLabel => Type switch
    {
        InterfaceKind.Physical => "PHYSICAL",
        InterfaceKind.Virtual => "VIRTUAL",
        InterfaceKind.Tunnel => "TUNNEL",
        _ => "LOOPBACK",
    };
    public string ConfidenceLabel => TelemetryMeta.ConfidenceLabel(Confidence);
    public string ThroughputText => $"↓ {Flow.FormatBytes(RxRate)}/s   ↑ {Flow.FormatBytes(TxRate)}/s";
}

public sealed class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public string Executable { get; set; } = "";
    public string StartTime { get; set; } = "";
}

/// <summary>A flow/connection record with attribution chain evidence.</summary>
public sealed class Flow : ObservableObject
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string LocalEndpoint { get; set; } = "";
    public string RemoteEndpoint { get; set; } = "";
    public string DestinationHost { get; set; } = "";
    public string DestinationIp { get; set; } = "";
    public int Port { get; set; }
    public string Protocol { get; set; } = "TCP";
    public ConnectionState State { get; set; } = ConnectionState.Established;
    public string InterfaceName { get; set; } = "";
    public string Route { get; set; } = "Default";
    public TrafficCategory Category { get; set; } = TrafficCategory.DirectInternet;
    public string Component { get; set; } = "";
    public string WfpLayer { get; set; } = "";
    public string Driver { get; set; } = "";
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Unknown;
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }

    public string CategoryLabel => TelemetryMeta.CategoryLabel(Category);
    public string ConfidenceLabel => TelemetryMeta.ConfidenceLabel(Confidence);
    public string StateLabel => State.ToString().ToUpper();
    public string TrafficText => $"{FormatBytes(RxRate)}/s ↓  {FormatBytes(TxRate)}/s ↑";

    public static string FormatBytes(double bps) => bps switch
    {
        >= 1_000_000 => $"{bps / 1_000_000:F2} MB",
        >= 1_000 => $"{bps / 1_000:F1} KB",
        _ => $"{bps:F0} B",
    };

    private double _rxRate;
    public double RxRate { get => _rxRate; set => Set(ref _rxRate, value); }

    private double _txRate;
    public double TxRate { get => _txRate; set => Set(ref _txRate, value); }
}

public sealed class Component : ObservableObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Vendor { get; set; } = "";
    public ComponentType Type { get; set; } = ComponentType.Other;
    public string Service { get; set; } = "";
    public string Driver { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "Running";
    public double AssociatedTraffic { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Confirmed;

    public string TypeLabel => TelemetryMeta.ComponentTypeLabel(Type);
    public string ConfidenceLabel => TelemetryMeta.ConfidenceLabel(Confidence);
}

public sealed class WfpObject : ObservableObject
{
    public WfpObjectKind Kind { get; set; } = WfpObjectKind.Filter;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Sublayer { get; set; } = "";
    public string Layer { get; set; } = "";
    public string Action { get; set; } = "Permit";
    public string Callout { get; set; } = "";
    public string Application { get; set; } = "";
    public int EventCount { get; set; }
    public int Permitted { get; set; }
    public int Blocked { get; set; }
    public int Dropped { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Confirmed;

    public string KindLabel => Kind.ToString().ToUpper();
    public string ConfidenceLabel => TelemetryMeta.ConfidenceLabel(Confidence);
    public string EventsText => $"ev:{EventCount}  perm:{Permitted}  blk:{Blocked}  drop:{Dropped}";
}

public sealed class DiagnosticEvent : ObservableObject
{
    public DateTime Timestamp { get; set; }
    public Severity Severity { get; set; } = Severity.Info;
    public string Source { get; set; } = "";
    public string Description { get; set; } = "";
    public string RelatedEntity { get; set; } = "";
    public string RelatedProcess { get; set; } = "";

    public string SeverityLabel => Severity.ToString().ToUpper();
    public string TimestampText => Timestamp.ToString("HH:mm:ss");
}

public sealed class CaptureSession : ObservableObject
{
    private string _state = "Idle";
    public string State { get => _state; set => Set(ref _state, value); }

    public string Filters { get; set; } = "";
    public string Destination { get; set; } = "";

    private long _sizeBytes;
    public long SizeBytes { get => _sizeBytes; set { if (Set(ref _sizeBytes, value)) OnPropertyChanged(nameof(SizeText)); } }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string ExportFormat { get; set; } = "PCAPNG";
    public int DurationSeconds { get; set; }

    public string SizeText => SizeBytes switch
    {
        >= 1_000_000_000 => $"{SizeBytes / 1_000_000_000.0:F2} GB",
        >= 1_000_000 => $"{SizeBytes / 1_000_000.0:F1} MB",
        >= 1_000 => $"{SizeBytes / 1_000.0:F0} KB",
        _ => $"{SizeBytes} B",
    };
}

/// <summary>One traffic throughput sample, per category, for charting.</summary>
public sealed class TrafficSample
{
    public DateTime Timestamp { get; set; }
    public double PrivateTunnel { get; set; }
    public double WebProxySwg { get; set; }
    public double DirectInternet { get; set; }
    public double OtherUnknown { get; set; }
}

public sealed class LatencySample
{
    public DateTime Timestamp { get; set; }
    public double LatencyMs { get; set; }
    public double JitterMs { get; set; }
    public double PacketLoss { get; set; }
}

/// <summary>Static helpers for category labels/colours and confidence styling.</summary>
public static class TelemetryMeta
{
    public static string CategoryLabel(TrafficCategory c) => c switch
    {
        TrafficCategory.PrivateTunnel => "PRIVATE/TUNNEL",
        TrafficCategory.WebProxySwg => "WEB PROXY/SWG",
        TrafficCategory.DirectInternet => "DIRECT INTERNET",
        _ => "OTHER/UNKNOWN",
    };

    public static string ConfidenceLabel(ConfidenceLevel c) => c switch
    {
        ConfidenceLevel.Confirmed => "CONFIRMED",
        ConfidenceLevel.Inferred => "INFERRED",
        _ => "UNKNOWN",
    };

    public static string ComponentTypeLabel(ComponentType t) => t switch
    {
        ComponentType.VpnClient => "VPN Client",
        ComponentType.NetworkFilter => "Network Filter",
        ComponentType.NdisFilter => "NDIS Filter",
        ComponentType.WfpProvider => "WFP Provider",
        ComponentType.WfpCallout => "WFP Callout",
        ComponentType.ProxyService => "Proxy Service",
        ComponentType.DnsClient => "DNS Client",
        ComponentType.VirtualAdapter => "Virtual Adapter",
        ComponentType.PhysicalAdapter => "Physical Adapter",
        _ => "Other",
    };

    public static string SourceLabel(SourceState s) => s switch
    {
        SourceState.Simulated => "SIMULATED",
        SourceState.Live => "LIVE",
        _ => "UNAVAILABLE",
    };
}
