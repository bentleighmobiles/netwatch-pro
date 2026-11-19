using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace NetwatchPro.Controls;

/// <summary>Blinking connection matrix (traffic matrix) for the screensaver.</summary>
public sealed class TrafficMatrix : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private readonly Random _rng = new();
    private readonly double[,] _cells = new double[24, 24];
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0x26, 0xD3, 0xE8), Color.FromRgb(0x5C, 0xFF, 0x6A),
        Color.FromRgb(0xFF, 0xB0, 0x00), Color.FromRgb(0xA7, 0x8B, 0xFA),
    };

    public TrafficMatrix()
    {
        SnapsToDevicePixels = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _timer.Tick += (_, _) => Step();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
        for (int r = 0; r < 24; r++)
            for (int c = 0; c < 24; c++)
                _cells[r, c] = _rng.NextDouble();
    }

    private void Step()
    {
        for (int r = 0; r < 24; r++)
            for (int c = 0; c < 24; c++)
            {
                _cells[r, c] -= 0.12;
                if (_cells[r, c] <= 0) _cells[r, c] = _rng.NextDouble() < 0.12 ? 1.0 : 0.0;
            }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x06, 0x08, 0x0E)), null, new Rect(0, 0, w, h));

        double cw = w / 24, ch = h / 24;
        for (int r = 0; r < 24; r++)
        {
            for (int c = 0; c < 24; c++)
            {
                double v = _cells[r, c];
                if (v <= 0.01) continue;
                var color = Palette[(r + c) % Palette.Length];
                var b = new SolidColorBrush(Color.FromArgb((byte)(40 + 215 * v), color.R, color.G, color.B));
                dc.DrawRectangle(b, null, new Rect(c * cw + 1, r * ch + 1, cw - 2, ch - 2));
            }
        }
    }
}
