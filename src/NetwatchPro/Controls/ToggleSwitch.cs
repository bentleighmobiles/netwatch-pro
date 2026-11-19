using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NetwatchPro.Controls;

/// <summary>Tactile toggle switch with illuminated track. Click flips state.</summary>
public sealed class ToggleSwitch : FrameworkElement
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool), typeof(ToggleSwitch),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color), typeof(ToggleSwitch),
        new FrameworkPropertyMetadata(Color.FromRgb(0x26, 0xD3, 0xE8), FrameworkPropertyMetadataOptions.AffectsRender));

    public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    public event EventHandler? Checked;

    public ToggleSwitch()
    {
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        IsChecked = !IsChecked;
        Checked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;

        var accent = new SolidColorBrush(Accent);
        var offTrack = new SolidColorBrush(Color.FromRgb(0x2A, 0x32, 0x42));
        var onTrack = new SolidColorBrush(Color.FromArgb(120, Accent.R, Accent.G, Accent.B));

        var track = new Rect(0, h * 0.2, w, h * 0.6);
        double trackRadius = h * 0.3;
        dc.DrawRoundedRectangle(IsChecked ? onTrack : offTrack, new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x44, 0x58)), 1), track, trackRadius, trackRadius);

        double thumbR = h * 0.34;
        double thumbX = IsChecked ? w - thumbR - 2 : thumbR + 2;
        var center = new Point(thumbX, h / 2);

        var thumbBrush = new RadialGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(0xE2, 0xE6, 0xEE), 0.0),
                new(Color.FromRgb(0xA8, 0xAE, 0xBA), 0.7),
                new(Color.FromRgb(0x77, 0x7D, 0x88), 1.0),
            }) { GradientOrigin = new Point(0.4, 0.35), RadiusX = 0.9, RadiusY = 0.9 };

        if (IsChecked)
            dc.DrawEllipse(accent, null, center, thumbR + 2.5, thumbR + 2.5); // glow ring
        dc.DrawEllipse(thumbBrush, new Pen(new SolidColorBrush(Color.FromRgb(0x55, 0x5B, 0x64)), 1), center, thumbR, thumbR);
    }
}
