using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace NetwatchPro.Controls;

/// <summary>Animated oscilloscope trace (phosphor green) for the screensaver.</summary>
public sealed class Oscilloscope : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private double _phase;
    private readonly List<double> _trace = new();

    public Oscilloscope()
    {
        SnapsToDevicePixels = true;
        ClipToBounds = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _timer.Tick += (_, _) => { _phase += 0.18; InvalidateVisual(); };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x04, 0x08, 0x06)), null, new Rect(0, 0, w, h));

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0x5C, 0xFF, 0x6A)), 1);
        for (int i = 1; i <= 8; i++)
        {
            double x = w * i / 8;
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
        }
        for (int i = 1; i <= 4; i++)
        {
            double y = h * i / 4;
            dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }

        // rolling waveform
        double mid = h / 2;
        double amp = h * 0.32;
        var pts = new List<Point>();
        for (int x = 0; x < w; x += 3)
        {
            double t = x / w * 12 + _phase;
            double v = Math.Sin(t) * 0.6 + Math.Sin(t * 2.7) * 0.25 + Math.Sin(t * 0.37 + _phase) * 0.15;
            pts.Add(new Point(x, mid - v * amp));
        }
        if (pts.Count > 1)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(pts[0], false, false);
                for (int i = 1; i < pts.Count; i++) ctx.LineTo(pts[i], true, false);
            }
            geo.Freeze();
            var glow = new Pen(new SolidColorBrush(Color.FromArgb(140, 0x5C, 0xFF, 0x6A)), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var line = new Pen(new SolidColorBrush(Color.FromRgb(0x5C, 0xFF, 0x6A)), 1.6);
            dc.DrawGeometry(null, glow, geo);
            dc.DrawGeometry(null, line, geo);
        }
    }
}
