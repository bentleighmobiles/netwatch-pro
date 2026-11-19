using System.IO;
using System.Text.Json;
using NetwatchPro.Models;
using NetwatchPro.ViewModels;

namespace NetwatchPro.Services;

/// <summary>Persisted user preferences (JSON).</summary>
public sealed class Settings
{
    public int TimeRangeIndex { get; set; } = 2;
    public int RefreshRateIndex { get; set; }
    public int TrafficFilterIndex { get; set; }
    public double ZoomLevel { get; set; } = 1.0;
    public double CpuThreshold { get; set; } = 85;
    public double LatencyThreshold { get; set; } = 150;
    public double PacketLossThreshold { get; set; } = 2.0;
    public bool ShowPrivate { get; set; } = true;
    public bool ShowProxy { get; set; } = true;
    public bool ShowDirect { get; set; } = true;
    public bool ShowOther { get; set; } = true;
    public string DefaultMode { get; set; } = "LightMonitor";
    public bool SoundEnabled { get; set; }
    public bool StartInLightMonitor { get; set; } = true;
    public bool CaptureAutoCleanup { get; set; } = true;
}

public static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(baseDir)) baseDir = AppContext.BaseDirectory;
            return Path.Combine(baseDir, "NetwatchPro", "settings.json");
        }
    }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* corrupt settings fall back to defaults */ }
        return new Settings();
    }

    public static void Save(Settings s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(s, Options));
        }
        catch { /* non-fatal */ }
    }

    public static Settings FromViewModel(MainViewModel vm) => new()
    {
        TimeRangeIndex = vm.TimeRangeIndex,
        RefreshRateIndex = vm.RefreshRateIndex,
        TrafficFilterIndex = vm.TrafficFilterIndex,
        ZoomLevel = vm.ZoomLevel,
        CpuThreshold = vm.CpuThreshold,
        LatencyThreshold = vm.LatencyThreshold,
        PacketLossThreshold = vm.PacketLossThreshold,
        ShowPrivate = vm.ShowPrivate,
        ShowProxy = vm.ShowProxy,
        ShowDirect = vm.ShowDirect,
        ShowOther = vm.ShowOther,
        DefaultMode = vm.Mode.ToString(),
        SoundEnabled = vm.SoundEnabled,
        StartInLightMonitor = vm.StartInLightMonitor,
        CaptureAutoCleanup = vm.CaptureAutoCleanup,
    };

    public static void ApplyToViewModel(MainViewModel vm, Settings s)
    {
        vm.TimeRangeIndex = Clamp(s.TimeRangeIndex, 0, MainViewModel.TimeRanges.Length - 1);
        vm.RefreshRateIndex = Clamp(s.RefreshRateIndex, 0, MainViewModel.RefreshRates.Length - 1);
        vm.TrafficFilterIndex = Clamp(s.TrafficFilterIndex, 0, MainViewModel.TrafficFilters.Length - 1);
        vm.ZoomLevel = s.ZoomLevel;
        vm.CpuThreshold = s.CpuThreshold;
        vm.LatencyThreshold = s.LatencyThreshold;
        vm.PacketLossThreshold = s.PacketLossThreshold;
        vm.ShowPrivate = s.ShowPrivate;
        vm.ShowProxy = s.ShowProxy;
        vm.ShowDirect = s.ShowDirect;
        vm.ShowOther = s.ShowOther;
        if (Enum.TryParse<OperatingMode>(s.DefaultMode, out var m)) vm.Mode = m;
        vm.SoundEnabled = s.SoundEnabled;
        vm.StartInLightMonitor = s.StartInLightMonitor;
        vm.CaptureAutoCleanup = s.CaptureAutoCleanup;
    }

    private static int Clamp(int v, int min, int max) => Math.Clamp(v, min, max);
}
