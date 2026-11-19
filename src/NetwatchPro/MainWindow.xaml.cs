using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using NetwatchPro.Models;
using NetwatchPro.Services;
using NetwatchPro.ViewModels;

namespace NetwatchPro;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private ITelemetryProvider _provider;
    private ScreensaverWindow? _saver;

    public MainWindow()
    {
        _vm = new MainViewModel();
        var settings = SettingsService.Load();
        SettingsService.ApplyToViewModel(_vm, settings);

        _provider = CreateAndStartProvider();

        DataContext = _vm;
        InitializeComponent();

        _vm.PropertyChanged += OnVmPropertyChanged;
        Closed += OnClosed;
    }

    private ITelemetryProvider CreateAndStartProvider()
    {
        if (_vm.DataSourceIndex == 0) // LIVE
        {
            try
            {
                var live = new WindowsTelemetryProvider(_vm);
                live.Start();
                return live;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NETWATCH PRO] Live provider failed ({ex.Message}); falling back to simulated.");
                _vm.ResetTelemetry();
            }
        }

        var sim = new SimulatedTelemetryProvider(_vm);
        sim.Start();
        return sim;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Mode))
        {
            if (_vm.Mode == OperatingMode.Screensaver) OpenScreensaver();
            else if (_saver != null) CloseScreensaver();
        }
        else if (e.PropertyName == nameof(MainViewModel.DataSourceIndex))
        {
            SwapProvider();
        }
    }

    private void SwapProvider()
    {
        bool wantLive = _vm.DataSourceIndex == 0;
        bool isLive = _provider.Source == SourceState.Live;
        if (wantLive == isLive) return;

        _provider.Dispose();
        _vm.ResetTelemetry();
        _provider = CreateAndStartProvider();
    }

    private void OpenScreensaver()
    {
        if (_saver != null) return;
        _saver = new ScreensaverWindow(_vm);
        _saver.Closed += (_, _) =>
        {
            _saver = null;
            if (_vm.Mode == OperatingMode.Screensaver)
                _vm.Mode = OperatingMode.LightMonitor;
        };
        _saver.Show();
    }

    private void CloseScreensaver()
    {
        _saver?.Close();
        _saver = null;
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        SettingsService.Save(SettingsService.FromViewModel(_vm));
        _provider.Dispose();
    }
}
