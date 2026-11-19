using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace NetwatchPro.Controls;

/// <summary>Analog arc gauge with needle and digital readout.</summary>
public sealed class AnalogGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(AnalogGauge),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(AnalogGauge), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(AnalogGauge), new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(AnalogGauge), new PropertyMetadata(""));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(AnalogGauge), new PropertyMetadata(""));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color), typeof(AnalogGauge),
        new FrameworkPropertyMetadata(Color.FromRgb(0x26, 0xD3, 0xE8), FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    public AnalogGauge() { SnapsToDevicePixels = true; }

    private const double Start = 135;
    private const double Sweep = 270;

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 6 || h < 6) return;

        var accent = new SolidColorBrush(Accent);
        var trackPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x32, 0x42)), 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var valuePen = new Pen(accent, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        double radius = Math.Min(w / 2, h / 2) - 4;
        var center = new Point(w / 2, h / 2 + 2);

        // track + value arc
        double frac = Maximum == Minimum ? 0 : (Value - Minimum) / (Maximum - Minimum);
        frac = Math.Clamp(frac, 0, 1);

        dc.DrawArc(trackPen, center, radius, Start, Start + Sweep);
        if (frac > 0)
            dc.DrawArc(valuePen, center, radius, Start, Start + Sweep * frac);

        // ticks
        for (int i = 0; i <= 10; i++)
        {
            double a = Start + Sweep * i / 10;
            var p1 = Polar(center, radius - 8, a);
            var p2 = Polar(center, radius - 13, a);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x8A, 0x95, 0xA7)), 1), p1, p2);
        }

        // needle
        var tip = Polar(center, radius - 16, Start + Sweep * frac);
        dc.DrawLine(new Pen(accent, 2), center, tip);
        dc.DrawEllipse(accent, null, center, 3.5, 3.5);

        // digital readout
        var text = $"{Value:0}{Unit}";
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Consolas"), 10, new SolidColorBrush(Color.FromRgb(0xD8, 0xDF, 0xEA)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y + radius * 0.42));

        if (!string.IsNullOrEmpty(Title))
        {
            var tft = new FormattedText(Title, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Consolas"), 8, new SolidColorBrush(Color.FromRgb(0x8A, 0x95, 0xA7)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(tft, new Point(center.X - tft.Width / 2, center.Y - radius * 0.55));
        }
    }

    private static Point Polar(Point c, double r, double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(rad), c.Y - r * Math.Sin(rad));
    }
}

internal static class DrawingContextExtensions
{
    public static void DrawArc(this DrawingContext dc, Pen pen, Point center, double radius, double startDeg, double endDeg)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var p0 = Polar(center, radius, startDeg);
            ctx.BeginFigure(p0, false, false);
            bool largeArc = (endDeg - startDeg) > 180;
            var p1 = Polar(center, radius, endDeg);
            ctx.ArcTo(p1, new Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static Point Polar(Point c, double r, double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180;
        return new Point(c.X + r * Math.Cos(rad), c.Y - r * Math.Sin(rad));
    }
}
