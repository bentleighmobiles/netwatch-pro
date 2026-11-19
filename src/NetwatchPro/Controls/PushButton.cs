using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NetwatchPro.Controls;

/// <summary>
/// Illuminated push button with pressed/raised states, active illumination and
/// optional disabled state. Fires Click on release.
/// </summary>
public sealed class PushButton : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(PushButton),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(PushButton),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color), typeof(PushButton),
        new FrameworkPropertyMetadata(Color.FromRgb(0xFF, 0x8C, 0x2E), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(PushButton), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter), typeof(object), typeof(PushButton), new PropertyMetadata(null));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => (object?)GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public event EventHandler? Click;

    private bool _pressed;

    public PushButton()
    {
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsEnabled) return;
        _pressed = true;
        CaptureMouse();
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_pressed)
        {
            _pressed = false;
            ReleaseMouseCapture();
            InvalidateVisual();
            if (IsEnabled && IsMouseOver)
            {
                Click?.Invoke(this, EventArgs.Empty);
                if (Command?.CanExecute(CommandParameter) == true) Command.Execute(CommandParameter);
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;

        var accent = new SolidColorBrush(Accent);
        var textBrush = new SolidColorBrush(IsEnabled ? Color.FromRgb(0xE8, 0xEC, 0xF4) : Color.FromRgb(0x55, 0x5B, 0x64));

        // glow when active
        if (IsActive && IsEnabled)
        {
            var glow = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(130, Accent.R, Accent.G, Accent.B), 0.0),
                    new(Color.FromArgb(0, Accent.R, Accent.G, Accent.B), 1.0),
                }) { RadiusX = 1, RadiusY = 1 };
            dc.DrawRectangle(glow, null, new Rect(-4, -4, w + 8, h + 8));
        }

        var body = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Color.FromRgb(0xC9, 0xCE, 0xD8), 0.0),
                new(Color.FromRgb(0x8A, 0x90, 0x9B), 0.5),
                new(Color.FromRgb(0x6A, 0x70, 0x78), 0.51),
                new(Color.FromRgb(0x9A, 0xA0, 0xAC), 1.0),
            }) { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };

        if (_pressed)
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x45, 0x4A, 0x52)), new Pen(new SolidColorBrush(Color.FromRgb(0x35, 0x3A, 0x42)), 1), new Rect(1, 2, w - 2, h - 4), 4, 4);
        }
        else
        {
            dc.DrawRoundedRectangle(body, new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0xE4, 0xEC)), 1), new Rect(0, 0, w, h - 2), 4, 4);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x40, 0x48)), 1), new Point(1, h - 1.5), new Point(w - 1, h - 1.5));
        }

        var ft = new FormattedText(Text, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Consolas"), Math.Min(11, h * 0.42), textBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2 - (_pressed ? 1 : 0)));
    }
}
