using System.Windows;
using System.Windows.Media;

namespace NetwatchPro.Controls;

/// <summary>Glowing LED indicator. Pure draw, no template.</summary>
public sealed class LedIndicator : FrameworkElement
{
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(LedIndicator),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(LedIndicator),
        new FrameworkPropertyMetadata(Color.FromRgb(0x5C, 0xFF, 0x6A), FrameworkPropertyMetadataOptions.AffectsRender));

    public bool IsOn { get => (bool)GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    public LedIndicator() { SnapsToDevicePixels = true; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 2 || h < 2) return;
        var c = new Point(w / 2, h / 2);
        double r = Math.Min(w, h) / 2 - 1;

        if (IsOn)
        {
            var glow = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(255, Color.R, Color.G, Color.B), 0.0),
                    new(Color.FromArgb(140, Color.R, Color.G, Color.B), 0.35),
                    new(Color.FromArgb(0, Color.R, Color.G, Color.B), 1.0),
                });
            dc.DrawEllipse(glow, null, c, r * 1.9, r * 1.9);
            dc.DrawEllipse(new SolidColorBrush(Color), null, c, r, r);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), null, new Point(c.X - r * 0.25, c.Y - r * 0.25), r * 0.28, r * 0.28);
        }
        else
        {
            var dim = new SolidColorBrush(Color.FromArgb(90, Color.R, Color.G, Color.B));
            dc.DrawEllipse(dim, new Pen(new SolidColorBrush(Color.FromArgb(140, Color.R, Color.G, Color.B)), 1), c, r, r);
        }
    }
}
