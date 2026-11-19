using System.Windows;
using System.Windows.Media;

namespace NetwatchPro.Controls;

/// <summary>Seven-segment LED numeric display (digits, A–F, dash, decimal point).</summary>
public sealed class SegmentDisplay : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SegmentDisplay),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color), typeof(SegmentDisplay),
        new FrameworkPropertyMetadata(Color.FromRgb(0xFF, 0x8C, 0x2E), FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    public SegmentDisplay() { SnapsToDevicePixels = true; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;

        var on = new SolidColorBrush(Accent);
        var onDim = new SolidColorBrush(Color.FromArgb(140, Accent.R, Accent.G, Accent.B));
        var off = new SolidColorBrush(Color.FromArgb(38, Accent.R, Accent.G, Accent.B));

        string text = Text ?? "";
        double cw = h * 0.56;           // digit cell width
        double gap = cw * 0.28;
        double total = text.Length * cw + (text.Length - 1) * gap;
        double x = (w - total) / 2;

        foreach (char ch in text.ToUpperInvariant())
        {
            if (ch == '.')
            {
                dc.DrawEllipse(on, null, new Point(x + cw * 0.6, h - cw * 0.1), cw * 0.07, cw * 0.07);
            }
            else
            {
                DrawDigit(dc, ch, x, 0, cw, h, on, onDim, off);
            }
            x += cw + gap;
        }
    }

    private void DrawDigit(DrawingContext dc, char ch, double x, double y, double w, double h,
        Brush on, Brush onDim, Brush off)
    {
        int mask = SegmentsFor(ch);
        double t = w * 0.16;

        // horizontal segments (A top, G middle, D bottom)
        DrawHoriz(dc, (mask & 1) != 0 ? on : off, x, y, w, t);                       // A
        DrawHoriz(dc, (mask & 64) != 0 ? on : off, x, y + h / 2 - t / 2, w, t);      // G
        DrawHoriz(dc, (mask & 8) != 0 ? on : off, x, y + h - t, w, t);               // D

        // vertical segments
        DrawVert(dc, (mask & 32) != 0 ? on : off, x, y, w, t, h);                    // F (top-left)
        DrawVert(dc, (mask & 2) != 0 ? on : off, x + w - t, y, w, t, h);             // B (top-right)
        DrawVert(dc, (mask & 16) != 0 ? on : off, x, y + h / 2, w, t, h);            // E (bottom-left)
        DrawVert(dc, (mask & 4) != 0 ? on : off, x + w - t, y + h / 2, w, t, h);     // C (bottom-right)
    }

    // segment bitmask: A=1 B=2 C=4 D=8 E=16 F=32 G=64
    private static int SegmentsFor(char ch) => ch switch
    {
        '0' => 0b0111111,
        '1' => 0b0000110,
        '2' => 0b1011011,
        '3' => 0b1001111,
        '4' => 0b1100110,
        '5' => 0b1101101,
        '6' => 0b1111101,
        '7' => 0b0000111,
        '8' => 0b1111111,
        '9' => 0b1101111,
        'A' => 0b1110111,
        'B' => 0b1111100,
        'C' => 0b0111001,
        'D' => 0b1011110,
        'E' => 0b1111001,
        'F' => 0b1110001,
        'H' => 0b1110110,
        'L' => 0b0111000,
        'P' => 0b1110011,
        'U' => 0b0111110,
        '-' => 0b1000000,
        '_' => 0b0001000,
        _ => 0,
    };

    private void DrawHoriz(DrawingContext dc, Brush b, double x, double y, double w, double t)
    {
        var pts = new[]
        {
            new Point(x + t * 0.7, y),
            new Point(x + w - t * 0.7, y),
            new Point(x + w, y + t / 2),
            new Point(x + w - t * 0.7, y + t),
            new Point(x + t * 0.7, y + t),
            new Point(x, y + t / 2),
        };
        dc.DrawGeometry(b, null, Polygon(pts));
    }

    private void DrawVert(DrawingContext dc, Brush b, double x, double y, double w, double t, double h)
    {
        double half = h / 2 - t;
        var pts = new[]
        {
            new Point(x, y + t * 0.7),
            new Point(x + t / 2, y),
            new Point(x + t, y + t * 0.7),
            new Point(x + t, y + half),
            new Point(x + t / 2, y + half + t * 0.7),
            new Point(x, y + half),
        };
        dc.DrawGeometry(b, null, Polygon(pts));
    }

    private static StreamGeometry Polygon(Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], true, true);
            for (int i = 1; i < pts.Length; i++) ctx.LineTo(pts[i], true, false);
        }
        geo.Freeze();
        return geo;
    }
}
