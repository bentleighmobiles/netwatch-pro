using System.ComponentModel;
using System.Windows;
using NetwatchPro.Models;
using NetwatchPro.Services;
using NetwatchPro.ViewModels;

namespace NetwatchPro;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly SimulatedTelemetryProvider _provider;
    private ScreensaverWindow? _saver;

    public MainWindow()
    {
        _vm = new MainViewModel();
        var settings = SettingsService.Load();
        SettingsService.ApplyToViewModel(_vm, settings);

        _provider = new SimulatedTelemetryProvider(_vm);
        _provider.Start();           // seed simulated telemetry synchronously before first render

        DataContext = _vm;
        InitializeComponent();

        _vm.PropertyChanged += OnVmPropertyChanged;
        Closed += OnClosed;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Mode)) return;
        if (_vm.Mode == OperatingMode.Screensaver) OpenScreensaver();
        else if (_saver != null) CloseScreensaver();
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
