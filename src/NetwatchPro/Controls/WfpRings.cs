using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NetwatchPro.Models;

namespace NetwatchPro.Controls;

/// <summary>Concentric WFP-layer rings for the screensaver.</summary>
public sealed class WfpRings : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(WfpRings),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    private readonly DispatcherTimer _timer;
    private double _phase;

    public WfpRings()
    {
        SnapsToDevicePixels = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => { _phase += 0.01; InvalidateVisual(); };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x0A, 0x10)), null, new Rect(0, 0, w, h));

        var center = new Point(w / 2, h / 2);
        double maxR = Math.Min(w, h) / 2 - 20;

        var names = new List<string>();
        if (ItemsSource != null)
            foreach (var o in ItemsSource)
                if (o is WfpObject wo) names.Add(wo.Name);

        int n = Math.Max(3, names.Count);
        var colors = new[] { Color.FromRgb(0x26, 0xD3, 0xE8), Color.FromRgb(0x5C, 0xFF, 0x6A), Color.FromRgb(0xFF, 0xB0, 0x00), Color.FromRgb(0xA7, 0x8B, 0xFA), Color.FromRgb(0xFF, 0x3B, 0x4E) };

        for (int i = 0; i < n; i++)
        {
            double r = maxR * (i + 1) / n;
            var color = colors[i % colors.Length];
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(110, color.R, color.G, color.B)), 2);
            dc.DrawEllipse(null, pen, center, r, r);

            // rotating pulse dot
            double a = _phase * Math.PI * 2 * (1 + i * 0.13);
            var dot = new Point(center.X + r * Math.Cos(a), center.Y + r * Math.Sin(a));
            dc.DrawEllipse(new SolidColorBrush(color), null, dot, 3.5, 3.5);

            if (i < names.Count)
            {
                var ft = new FormattedText(names[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Consolas"), 9, new SolidColorBrush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(center.X - r - 8, center.Y - r + 2));
            }
        }

        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x5C, 0xFF, 0x6A)), null, center, 5, 5);
    }
}
