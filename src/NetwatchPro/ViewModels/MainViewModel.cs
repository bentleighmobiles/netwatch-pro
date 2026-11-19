using System.Collections.ObjectModel;
using System.Windows.Input;
using NetwatchPro.Models;

namespace NetwatchPro.ViewModels;

/// <summary>
/// Single source of truth for the console. UI binds here; the telemetry provider
/// mutates it. No Windows collection logic lives in views.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    public const int HistoryCap = 2880;

    // ---------------- header ----------------
    public string ComputerName { get; set; } = "NETWATCH-01";
    public string WinVersion { get; set; } = "Windows 11 Pro";
    public string WinBuild { get; set; } = "10.0.22631.3958";
    public string Architecture { get; set; } = "x64";
    public string RegionTimeZone { get; set; } = "Australia/Melbourne (UTC+10)";
    public string UserName { get; set; } = @"NETWATCH\operator";
    public string ActiveAdapter { get; set; } = "Ethernet 2";

    private string _timeDate = DateTime.Now.ToString("ddd yyyy-MM-dd HH:mm:ss");
    public string TimeDate { get => _timeDate; set => Set(ref _timeDate, value); }

    private string _health = "NOMINAL";
    public string OverallHealth { get => _health; set { if (Set(ref _health, value)) { OnPropertyChanged(nameof(IsHealthy)); OnPropertyChanged(nameof(IsDegraded)); } } }
    public bool IsHealthy => _health == "NOMINAL";
    public bool IsDegraded => _health != "NOMINAL";

    private SourceState _globalSource = SourceState.Simulated;
    public SourceState GlobalSource
    {
        get => _globalSource;
        set
        {
            if (Set(ref _globalSource, value))
            {
                OnPropertyChanged(nameof(SourceLabel));
                OnPropertyChanged(nameof(SourceBrush));
            }
        }
    }
    public string SourceLabel => _globalSource switch
    {
        SourceState.Live => "LIVE DATA",
        SourceState.Unavailable => "DATA UNAVAILABLE",
        _ => "SIMULATED DATA",
    };
    public string SourceBrush => _globalSource switch
    {
        SourceState.Live => "#5CFF6A",
        SourceState.Unavailable => "#FF3B4E",
        _ => "#FF8C2E",
    };

    private string _uptime = "0d 00:00:00";
    public string Uptime { get => _uptime; set => Set(ref _uptime, value); }

    // ---------------- mode / navigation ----------------
    private OperatingMode _mode = OperatingMode.LightMonitor;
    public OperatingMode Mode
    {
        get => _mode;
        set { if (Set(ref _mode, value)) { OnPropertyChanged(nameof(ModeLabel)); OnPropertyChanged(nameof(IsCaptureMode)); } }
    }

    public string ModeLabel => Mode switch
    {
        OperatingMode.LightMonitor => "LIGHT MONITOR",
        OperatingMode.FullDiagnostics => "FULL DIAGNOSTICS",
        OperatingMode.CaptureMode => "CAPTURE MODE",
        _ => "SCREENSAVER",
    };

    public bool IsCaptureMode => Mode == OperatingMode.CaptureMode;

    private string _section = "OVERVIEW";
    public string SelectedSection { get => _section; set => Set(ref _section, value); }

    private bool _paused;
    public bool IsPaused { get => _paused; set { if (Set(ref _paused, value)) { OnPropertyChanged(nameof(LivePaused)); OnPropertyChanged(nameof(IsLive)); } } }
    public string LivePaused => IsPaused ? "PAUSED" : "LIVE";
    public bool IsLive => !IsPaused;

    // ---------------- system overview ----------------
    private double _cpuUsage;
    public double CpuUsage { get => _cpuUsage; set => Set(ref _cpuUsage, value); }

    private double? _cpuTemp = 58.0;
    public double? CpuTemp { get => _cpuTemp; set => Set(ref _cpuTemp, value); }
    public string CpuTempText => CpuTemp is null ? "N/A" : $"{CpuTemp:F0}°C";

    private double _ramUsage;
    public double RamUsage { get => _ramUsage; set => Set(ref _ramUsage, value); }
    public string RamText { get; private set; } = "0.0 / 0.0 GB";

    private double _diskUsage;
    public double DiskUsage { get => _diskUsage; set => Set(ref _diskUsage, value); }
    public string DiskText { get; private set; } = "0 / 0 GB";

    private double _netRxRate;
    public double NetRxRate { get => _netRxRate; set => Set(ref _netRxRate, value); }
    public string NetRxText { get; private set; } = "0 B/s";

    private double _netTxRate;
    public double NetTxRate { get => _netTxRate; set => Set(ref _netTxRate, value); }
    public string NetTxText { get; private set; } = "0 B/s";

    // ---------------- traffic ----------------
    private double _incomingRate;
    public double IncomingRate { get => _incomingRate; set => Set(ref _incomingRate, value); }
    public string IncomingText { get; private set; } = "0 B/s";

    private double _outgoingRate;
    public double OutgoingRate { get => _outgoingRate; set => Set(ref _outgoingRate, value); }
    public string OutgoingText { get; private set; } = "0 B/s";

    private double _totalRate;
    public double TotalRate { get => _totalRate; set => Set(ref _totalRate, value); }
    public string TotalText { get; private set; } = "0 B/s";

    private double _peakRate;
    public double PeakRate { get => _peakRate; set => Set(ref _peakRate, value); }
    public string PeakText { get; private set; } = "0 B/s";

    private double _avgRate;
    public double AvgRate { get => _avgRate; set => Set(ref _avgRate, value); }
    public string AvgText { get; private set; } = "0 B/s";

    // ---------------- live metrics ----------------
    private double _avgLatency;
    public double AvgLatency { get => _avgLatency; set => Set(ref _avgLatency, value); }
    public string AvgLatencyText { get; private set; } = "0 ms";

    private double _maxLatency;
    public double MaxLatency { get => _maxLatency; set => Set(ref _maxLatency, value); }
    public string MaxLatencyText { get; private set; } = "0 ms";

    private double _jitter;
    public double Jitter { get => _jitter; set => Set(ref _jitter, value); }
    public string JitterText { get; private set; } = "0 ms";

    private double _packetLoss;
    public double PacketLoss { get => _packetLoss; set => Set(ref _packetLoss, value); }
    public string PacketLossText { get; private set; } = "0.00%";

    private double _dnsTime;
    public double DnsTime { get => _dnsTime; set => Set(ref _dnsTime, value); }
    public string DnsTimeText { get; private set; } = "0 ms";

    private double _connEstTime;
    public double ConnEstTime { get => _connEstTime; set => Set(ref _connEstTime, value); }
    public string ConnEstText { get; private set; } = "0 ms";

    private double _availability;
    public double Availability { get => _availability; set => Set(ref _availability, value); }
    public string AvailabilityText { get; private set; } = "100.0%";

    // ---------------- collections ----------------
    public ObservableCollection<NetworkInterface> Interfaces { get; } = new();
    public ObservableCollection<Flow> Flows { get; } = new();
    public ObservableCollection<Component> Components { get; } = new();
    public ObservableCollection<WfpObject> WfpObjects { get; } = new();
    public ObservableCollection<WfpObject> WfpLayers { get; } = new();
    public ObservableCollection<DiagnosticEvent> Events { get; } = new();
    public ObservableCollection<ProcessInfo> Processes { get; } = new();
    public ObservableCollection<TrafficSample> TrafficHistory { get; } = new();
    public ObservableCollection<LatencySample> LatencyHistory { get; } = new();

    // ---------------- selectors (index into static arrays) ----------------
    public static string[] TimeRanges { get; } = { "1m", "5m", "15m", "1h", "6h", "12h", "24h" };
    public static string[] RefreshRates { get; } = { "1s", "5s", "10s", "30s", "MANUAL" };
    public static string[] TrafficFilters { get; } = { "ALL", "PRIVATE", "PROXY", "DIRECT", "OTHER" };
    public static string[] Sections { get; } = { "OVERVIEW", "TRAFFIC", "DRIVERS", "WFP", "CONNECTIONS", "ISSUES", "ANALYSIS", "SETTINGS" };
    public static string[] DataSources { get; } = { "LIVE", "SIMULATED" };

    private int _dataSourceIndex;
    public int DataSourceIndex
    {
        get => _dataSourceIndex;
        set { if (Set(ref _dataSourceIndex, value)) OnPropertyChanged(nameof(DataSourceLabel)); }
    }
    public string DataSourceLabel => DataSources[Math.Clamp(_dataSourceIndex, 0, DataSources.Length - 1)];

    private int _timeRangeIndex = 2;
    public int TimeRangeIndex
    {
        get => _timeRangeIndex;
        set { if (Set(ref _timeRangeIndex, value)) { OnPropertyChanged(nameof(TimeRangeLabel)); OnPropertyChanged(nameof(TimeRangeSeconds)); } }
    }
    public string TimeRangeLabel => TimeRanges[_timeRangeIndex];

    public int TimeRangeSeconds => TimeRanges[_timeRangeIndex] switch
    {
        "1m" => 60, "5m" => 300, "15m" => 900, "1h" => 3600,
        "6h" => 21600, "12h" => 43200, _ => 86400,
    };

    private int _refreshRateIndex;
    public int RefreshRateIndex
    {
        get => _refreshRateIndex;
        set { if (Set(ref _refreshRateIndex, value)) OnPropertyChanged(nameof(RefreshRateLabel)); }
    }
    public string RefreshRateLabel => RefreshRates[_refreshRateIndex];

    private int _trafficFilterIndex;
    public int TrafficFilterIndex
    {
        get => _trafficFilterIndex;
        set { if (Set(ref _trafficFilterIndex, value)) OnPropertyChanged(nameof(TrafficFilterLabel)); }
    }
    public string TrafficFilterLabel => TrafficFilters[_trafficFilterIndex];

    private double _zoom = 1.0;
    public double ZoomLevel { get => _zoom; set => Set(ref _zoom, value); }

    // ---------------- thresholds ----------------
    private double _cpuThreshold = 85;
    public double CpuThreshold { get => _cpuThreshold; set => Set(ref _cpuThreshold, value); }

    private double _latencyThreshold = 150;
    public double LatencyThreshold { get => _latencyThreshold; set => Set(ref _latencyThreshold, value); }

    private double _packetLossThreshold = 2.0;
    public double PacketLossThreshold { get => _packetLossThreshold; set => Set(ref _packetLossThreshold, value); }

    // ---------------- alerts ----------------
    private int _alertCount;
    public int AlertCount { get => _alertCount; set { if (Set(ref _alertCount, value)) OnPropertyChanged(nameof(HasAlerts)); } }
    public bool HasAlerts => AlertCount > 0;

    private bool _alertActive;
    public bool AlertActive { get => _alertActive; set => Set(ref _alertActive, value); }

    // ---------------- preferences ----------------
    private bool _soundEnabled;
    public bool SoundEnabled { get => _soundEnabled; set => Set(ref _soundEnabled, value); }

    private bool _startInLightMonitor = true;
    public bool StartInLightMonitor { get => _startInLightMonitor; set => Set(ref _startInLightMonitor, value); }

    private bool _captureAutoCleanup = true;
    public bool CaptureAutoCleanup { get => _captureAutoCleanup; set => Set(ref _captureAutoCleanup, value); }

    // ---------------- capture ----------------
    public CaptureSession Capture { get; } = new();

    private bool _capturing;
    public bool IsCapturing
    {
        get => _capturing;
        set
        {
            if (Set(ref _capturing, value))
            {
                Capture.State = value ? "Capturing" : "Idle";
                OnPropertyChanged(nameof(CaptureStatus));
            }
        }
    }

    public string CaptureStatus => IsCapturing ? "REC ●" : "IDLE";

    // ---------------- series visibility ----------------
    private bool _showPrivate = true;
    public bool ShowPrivate { get => _showPrivate; set => Set(ref _showPrivate, value); }

    private bool _showProxy = true;
    public bool ShowProxy { get => _showProxy; set => Set(ref _showProxy, value); }

    private bool _showDirect = true;
    public bool ShowDirect { get => _showDirect; set => Set(ref _showDirect, value); }

    private bool _showOther = true;
    public bool ShowOther { get => _showOther; set => Set(ref _showOther, value); }

    // ---------------- selection (detail inspectors) ----------------
    private DiagnosticEvent? _selectedEvent;
    public DiagnosticEvent? SelectedEvent { get => _selectedEvent; set => Set(ref _selectedEvent, value); }

    private Flow? _selectedFlow;
    public Flow? SelectedFlow { get => _selectedFlow; set => Set(ref _selectedFlow, value); }

    private Component? _selectedComponent;
    public Component? SelectedComponent { get => _selectedComponent; set => Set(ref _selectedComponent, value); }

    // ---------------- commands ----------------
    public ICommand SetSectionCommand { get; }
    public ICommand SetModeCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand AcknowledgeAlertsCommand { get; }
    public ICommand StartCaptureCommand { get; }
    public ICommand StopCaptureCommand { get; }
    public ICommand ExportCaptureCommand { get; }

    public MainViewModel()
    {
        SetSectionCommand = new RelayCommand(o => SelectedSection = o as string ?? "OVERVIEW");
        SetModeCommand = new RelayCommand(o => Mode = o is OperatingMode m ? m : Mode);
        TogglePauseCommand = new RelayCommand(_ => IsPaused = !IsPaused);
        AcknowledgeAlertsCommand = new RelayCommand(_ => { AlertCount = 0; AlertActive = false; });
        StartCaptureCommand = new RelayCommand(_ => IsCapturing = true);
        StopCaptureCommand = new RelayCommand(_ => IsCapturing = false);
        ExportCaptureCommand = new RelayCommand(_ => { /* simulated export (Milestone 1) */ });
    }

    /// <summary>Clear all telemetry collections (used when switching data source).</summary>
    public void ResetTelemetry()
    {
        Interfaces.Clear();
        Flows.Clear();
        Components.Clear();
        WfpObjects.Clear();
        WfpLayers.Clear();
        Events.Clear();
        Processes.Clear();
        TrafficHistory.Clear();
        LatencyHistory.Clear();
        AlertCount = 0;
        AlertActive = false;
        OverallHealth = "NOMINAL";
    }

    /// <summary>Push one traffic sample and trim to the bounded history cap.</summary>
    public void PushTraffic(TrafficSample s)
    {
        TrafficHistory.Add(s);
        while (TrafficHistory.Count > HistoryCap) TrafficHistory.RemoveAt(0);
    }

    public void PushLatency(LatencySample s)
    {
        LatencyHistory.Add(s);
        while (LatencyHistory.Count > HistoryCap) LatencyHistory.RemoveAt(0);
    }

    public void SetRamText(string text) => RamText = text;
    public void SetDiskText(string text) => DiskText = text;
    public void SetNetText(string rx, string tx) { NetRxText = rx; NetTxText = tx; }
    public void SetTrafficText(string inc, string outg, string total, string peak, string avg)
    { IncomingText = inc; OutgoingText = outg; TotalText = total; PeakText = peak; AvgText = avg; }
    public void SetLatencyText(string avg, string max, string jitter, string loss, string dns, string conn, string avail)
    { AvgLatencyText = avg; MaxLatencyText = max; JitterText = jitter; PacketLossText = loss; DnsTimeText = dns; ConnEstText = conn; AvailabilityText = avail; }

    public bool CategoryVisible(TrafficCategory c) => _trafficFilterIndex switch
    {
        0 => true,
        1 => c == TrafficCategory.PrivateTunnel,
        2 => c == TrafficCategory.WebProxySwg,
        3 => c == TrafficCategory.DirectInternet,
        4 => c == TrafficCategory.OtherUnknown,
        _ => true,
    };
}
