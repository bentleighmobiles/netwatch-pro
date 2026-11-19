using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NetwatchPro.Models;
using NetwatchPro.ViewModels;

namespace NetwatchPro;

public partial class ScreensaverWindow : Window
{
    private readonly DispatcherTimer _rotator;
    private readonly DispatcherTimer _clock;
    private int _layout;
    private double _dim;

    public ScreensaverWindow(MainViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();

        _rotator = new DispatcherTimer { Interval = TimeSpan.FromSeconds(18) };
        _rotator.Tick += (_, _) => ShowLayout((_layout + 1) % 5);

        _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Cover the primary display including the taskbar.
        WindowState = WindowState.Normal;
        Left = 0; Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        _rotator.Start();
        _clock.Start();
        ShowLayout(0);
        Focus();
    }

    private void ShowLayout(int i)
    {
        _layout = i;
        LayoutNetwork.Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
        LayoutScope.Visibility = i == 1 ? Visibility.Visible : Visibility.Collapsed;
        LayoutTelemetry.Visibility = i == 2 ? Visibility.Visible : Visibility.Collapsed;
        LayoutMatrix.Visibility = i == 3 ? Visibility.Visible : Visibility.Collapsed;
        LayoutWfp.Visibility = i == 4 ? Visibility.Visible : Visibility.Collapsed;
        Hint.Text = $"LAYOUT {i + 1}/5  ·  ESC EXIT  ·  1–5 LAYOUTS  ·  ↑↓ BRIGHTNESS";
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                if (DataContext is MainViewModel vm) vm.Mode = OperatingMode.LightMonitor;
                e.Handled = true;
                break;
            case Key.D1: case Key.NumPad1: ShowLayout(0); e.Handled = true; break;
            case Key.D2: case Key.NumPad2: ShowLayout(1); e.Handled = true; break;
            case Key.D3: case Key.NumPad3: ShowLayout(2); e.Handled = true; break;
            case Key.D4: case Key.NumPad4: ShowLayout(3); e.Handled = true; break;
            case Key.D5: case Key.NumPad5: ShowLayout(4); e.Handled = true; break;
            case Key.Up:
                _dim = Math.Max(0, _dim - 0.05);
                BrightnessOverlay.Opacity = _dim;
                e.Handled = true;
                break;
            case Key.Down:
                _dim = Math.Min(0.85, _dim + 0.05);
                BrightnessOverlay.Opacity = _dim;
                e.Handled = true;
                break;
        }
    }
}
