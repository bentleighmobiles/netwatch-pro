using System.Collections;
using System.Windows;
using System.Windows.Media;
using NetwatchPro.Models;

namespace NetwatchPro.Controls;

/// <summary>Compact single-series sparkline. Accepts doubles or LatencySample items.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color), typeof(Sparkline),
        new FrameworkPropertyMetadata(Color.FromRgb(0x26, 0xD3, 0xE8), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValuePathProperty = DependencyProperty.Register(
        nameof(ValuePath), typeof(string), typeof(Sparkline),
        new FrameworkPropertyMetadata("LatencyMs", FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public string ValuePath { get => (string)GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }

    public Sparkline() { SnapsToDevicePixels = true; ClipToBounds = true; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4 || ItemsSource == null) return;

        var values = new List<double>();
        foreach (var o in ItemsSource)
        {
            if (o is double d) values.Add(d);
            else if (o is LatencySample l) values.Add(ValuePath switch
            {
                "PacketLoss" => l.PacketLoss,
                "JitterMs" => l.JitterMs,
                _ => l.LatencyMs,
            });
        }
        if (values.Count < 2) return;

        double min = values.Min(), max = values.Max();
        if (max - min < 1e-6) { max = min + 1; }

        var pen = new Pen(new SolidColorBrush(Accent), 1.4);
        var fill = new SolidColorBrush(Color.FromArgb(40, Accent.R, Accent.G, Accent.B));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            double x0 = 0, y0 = h - (values[0] - min) / (max - min) * (h - 2) - 1;
            ctx.BeginFigure(new Point(x0, y0), true, false);
            for (int i = 1; i < values.Count; i++)
            {
                double x = (double)i / (values.Count - 1) * w;
                double y = h - (values[i] - min) / (max - min) * (h - 2) - 1;
                ctx.LineTo(new Point(x, y), true, false);
            }
            ctx.LineTo(new Point(w, h), true, false);
            ctx.LineTo(new Point(0, h), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(fill, null, geo);
        dc.DrawGeometry(null, pen, GetLineGeometry(values, w, h, min, max));
    }

    private static Geometry GetLineGeometry(List<double> values, double w, double h, double min, double max)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, h - (values[0] - min) / (max - min) * (h - 2) - 1), false, false);
            for (int i = 1; i < values.Count; i++)
            {
                double x = (double)i / (values.Count - 1) * w;
                double y = h - (values[i] - min) / (max - min) * (h - 2) - 1;
                ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geo.Freeze();
        return geo;
    }
}
