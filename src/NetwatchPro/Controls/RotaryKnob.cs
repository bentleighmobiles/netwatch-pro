using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NetwatchPro.Controls;

/// <summary>
/// Draggable rotary knob with wheel support, configurable range, optional discrete
/// snapping (Steps &gt; 0) and a centre value readout. The defining control of the console.
/// </summary>
public sealed class RotaryKnob : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RotaryKnob),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(RotaryKnob), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(RotaryKnob), new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StepsProperty = DependencyProperty.Register(
        nameof(Steps), typeof(int), typeof(RotaryKnob), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color), typeof(RotaryKnob),
        new FrameworkPropertyMetadata(Color.FromRgb(0x26, 0xD3, 0xE8), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(RotaryKnob), new PropertyMetadata(""));

    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
        nameof(DisplayText), typeof(string), typeof(RotaryKnob),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int Steps { get => (int)GetValue(StepsProperty); set => SetValue(StepsProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string? DisplayText { get => (string?)GetValue(DisplayTextProperty); set => SetValue(DisplayTextProperty, value); }

    public event EventHandler? ValueChanged;

    private const double StartAngle = 135;
    private const double Sweep = 270;
    private Point _dragStart;
    private double _dragStartValue;
    private bool _dragging;

    public RotaryKnob()
    {
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
        Focusable = true;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RotaryKnob k) k.ValueChanged?.Invoke(k, EventArgs.Empty);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;

        var center = new Point(w / 2, h / 2);
        double r = Math.Min(w, h) / 2 - 3;

        var accent = new SolidColorBrush(Accent);
        var accentDim = new SolidColorBrush(Color.FromArgb(80, Accent.R, Accent.G, Accent.B));
        var ring = new Pen(new SolidColorBrush(Color.FromRgb(0x8A, 0x90, 0x9B)), 2);

        // metallic body
        var bodyBrush = new RadialGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(0xD5, 0xD9, 0xE2), 0.0),
                new(Color.FromRgb(0x9A, 0xA0, 0xAC), 0.55),
                new(Color.FromRgb(0x55, 0x5B, 0x64), 0.80),
                new(Color.FromRgb(0x6A, 0x70, 0x78), 1.0),
            })
        { Center = new Point(0.42, 0.38), GradientOrigin = new Point(0.42, 0.38), RadiusX = 0.95, RadiusY = 0.95 };

        dc.DrawEllipse(bodyBrush, ring, center, r, r);

        // tick marks
        for (int i = 0; i <= 5; i++)
        {
            double a = StartAngle + Sweep * i / 5;
            var p1 = Polar(center, r - 3, a);
            var p2 = Polar(center, r - 7, a);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0xC9, 0xCE, 0xD8)), 1), p1, p2);
        }

        // pointer
        double frac = Maximum == Minimum ? 0 : (Value - Minimum) / (Maximum - Minimum);
        frac = Math.Clamp(frac, 0, 1);
        double angle = StartAngle + Sweep * frac;
        var tip = Polar(center, r - 11, angle);
        dc.DrawLine(new Pen(accent, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, center, tip);
        dc.DrawEllipse(accent, null, center, 4, 4);

        // centre value readout
        var label = !string.IsNullOrEmpty(DisplayText) ? DisplayText : Value.ToString(Steps > 0 ? "0" : "0.#") + Unit;
        var ft = new FormattedText(label!, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Consolas"), Math.Max(7, r * 0.30), accentDim,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y + r * 0.30));
    }

    private static Point Polar(Point c, double radius, double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180;
        return new Point(c.X + radius * Math.Cos(rad), c.Y - radius * Math.Sin(rad));
    }

    private double AngleOf(Point p)
    {
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        double rad = Math.Atan2(c.Y - p.Y, p.X - c.X);
        double deg = rad * 180 / Math.PI;
        return deg < 0 ? deg + 360 : deg;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _dragging = true;
        _dragStart = e.GetPosition(this);
        _dragStartValue = Value;
        CaptureMouse();
        Focus();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        double delta = AngleOf(e.GetPosition(this)) - AngleOf(_dragStart);
        if (delta > 180) delta -= 360;
        if (delta < -180) delta += 360;
        SetValueFromDrag(_dragStartValue + delta / Sweep * (Maximum - Minimum));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragging = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        double step = Steps > 0 ? (Maximum - Minimum) / Math.Max(1, Steps - 1) : (Maximum - Minimum) / 100.0;
        SetValueFromDrag(Value + (e.Delta > 0 ? step : -step));
    }

    private void SetValueFromDrag(double v)
    {
        if (Steps > 0)
        {
            double span = Maximum - Minimum;
            int idx = (int)Math.Round((v - Minimum) / span * (Steps - 1));
            idx = Math.Clamp(idx, 0, Steps - 1);
            Value = Minimum + idx * span / (Steps - 1);
        }
        else
        {
            Value = Math.Clamp(v, Minimum, Maximum);
        }
    }
}
