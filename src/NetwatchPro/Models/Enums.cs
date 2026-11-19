namespace NetwatchPro.Models;

/// <summary>Origin of a telemetry value, displayed verbatim on the console.</summary>
public enum SourceState { Simulated, Live, Unavailable }

/// <summary>Strength of evidence behind an attribution claim.</summary>
public enum ConfidenceLevel { Confirmed, Inferred, Unknown }

/// <summary>Operating mode of the console.</summary>
public enum OperatingMode { LightMonitor, FullDiagnostics, CaptureMode, Screensaver }

/// <summary>Vendor-neutral traffic classification bucket.</summary>
public enum TrafficCategory { PrivateTunnel, WebProxySwg, DirectInternet, OtherUnknown }

public enum Severity { Info, Warning, Critical }

public enum ComponentType
{
    VpnClient, NetworkFilter, NdisFilter, WfpProvider, WfpCallout,
    ProxyService, DnsClient, VirtualAdapter, PhysicalAdapter, Other
}

public enum WfpObjectKind { Provider, Sublayer, Filter, Callout, Layer }

public enum ConnectionState
{
    Established, Listening, TimeWait, CloseWait, SynSent, SynReceived, FinWait, Unknown
}

public enum InterfaceKind { Physical, Virtual, Tunnel, Loopback }
