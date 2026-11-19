using System.Windows;
using System.Windows.Media;

namespace NetwatchPro.Controls;

/// <summary>Segmented LED bar meter with colour gradient (green→amber→red).</summary>
public sealed class BarMeter : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(BarMeter),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(BarMeter), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(BarMeter), new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(int), typeof(BarMeter), new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int Segments { get => (int)GetValue(SegmentsProperty); set => SetValue(SegmentsProperty, value); }

    public BarMeter() { SnapsToDevicePixels = true; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        int n = Math.Max(2, Segments);
        if (w < 4 || h < 4) return;

        double gap = 1.5;
        double segW = (w - gap * (n - 1)) / n;

        double frac = Maximum == Minimum ? 0 : (Value - Minimum) / (Maximum - Minimum);
        frac = Math.Clamp(frac, 0, 1);
        int lit = (int)Math.Round(frac * n);

        var off = new SolidColorBrush(Color.FromRgb(0x20, 0x26, 0x32));
        var frame = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x44, 0x58)), 1);

        for (int i = 0; i < n; i++)
        {
            var rect = new Rect(i * (segW + gap), 0, segW, h);
            Brush b;
            if (i < lit)
            {
                double t = n == 1 ? 0 : (double)i / (n - 1);
                var c = t < 0.5
                    ? Lerp(Color.FromRgb(0x5C, 0xFF, 0x6A), Color.FromRgb(0xFF, 0xB0, 0x00), t / 0.5)
                    : Lerp(Color.FromRgb(0xFF, 0xB0, 0x00), Color.FromRgb(0xFF, 0x3B, 0x4E), (t - 0.5) / 0.5);
                b = new SolidColorBrush(c);
            }
            else b = off;
            dc.DrawRoundedRectangle(b, frame, rect, 1.5, 1.5);
        }
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }
}
