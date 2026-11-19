using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NetwatchPro.Models;

namespace NetwatchPro.Controls;

/// <summary>
/// High-performance multi-series time chart with hover inspection, selectable
/// series and a sliding time window. Redraws only on data/property changes.
/// </summary>
public sealed class TrendChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(TrendChart),
        new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty ShowPrivateProperty = DependencyProperty.Register(
        nameof(ShowPrivate), typeof(bool), typeof(TrendChart), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowProxyProperty = DependencyProperty.Register(
        nameof(ShowProxy), typeof(bool), typeof(TrendChart), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowDirectProperty = DependencyProperty.Register(
        nameof(ShowDirect), typeof(bool), typeof(TrendChart), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowOtherProperty = DependencyProperty.Register(
        nameof(ShowOther), typeof(bool), typeof(TrendChart), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WindowSecondsProperty = DependencyProperty.Register(
        nameof(WindowSeconds), typeof(double), typeof(TrendChart), new FrameworkPropertyMetadata(900.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public bool ShowPrivate { get => (bool)GetValue(ShowPrivateProperty); set => SetValue(ShowPrivateProperty, value); }
    public bool ShowProxy { get => (bool)GetValue(ShowProxyProperty); set => SetValue(ShowProxyProperty, value); }
    public bool ShowDirect { get => (bool)GetValue(ShowDirectProperty); set => SetValue(ShowDirectProperty, value); }
    public bool ShowOther { get => (bool)GetValue(ShowOtherProperty); set => SetValue(ShowOtherProperty, value); }
    public double WindowSeconds { get => (double)GetValue(WindowSecondsProperty); set => SetValue(WindowSecondsProperty, value); }

    private readonly List<TrafficSample> _cache = new();
    private INotifyCollectionChanged? _notifier;
    private Point _hover;
    private bool _hasHover;

    private static readonly Color[] SeriesColors =
    {
        Color.FromRgb(0xA7, 0x8B, 0xFA), // private
        Color.FromRgb(0xFF, 0xB0, 0x00), // proxy
        Color.FromRgb(0x26, 0xD3, 0xE8), // direct
        Color.FromRgb(0x6B, 0x7A, 0x8F), // other
    };

    private static readonly string[] SeriesNames =
    {
        "PRIVATE/TUNNEL", "WEB PROXY/SWG", "DIRECT INTERNET", "OTHER/UNKNOWN",
    };

    public TrendChart()
    {
        SnapsToDevicePixels = true;
        ClipToBounds = true;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TrendChart c) return;
        if (c._notifier != null) c._notifier.CollectionChanged -= c.OnCollectionChanged;
        c._notifier = e.NewValue as INotifyCollectionChanged;
        if (c._notifier != null) c._notifier.CollectionChanged += c.OnCollectionChanged;
        c.RebuildCache();
        c.InvalidateVisual();
    }

    private void OnCollectionChanged(object? s, NotifyCollectionChangedEventArgs e)
    {
        RebuildCache();
        InvalidateVisual();
    }

    private void RebuildCache()
    {
        _cache.Clear();
        if (ItemsSource == null) return;
        foreach (var o in ItemsSource)
            if (o is TrafficSample ts) _cache.Add(ts);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _hover = e.GetPosition(this);
        _hasHover = true;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hasHover = false;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x08, 0x0A, 0x10)), null, new Rect(0, 0, w, h));
        if (w < 30 || h < 30) return;

        const double padL = 52, padR = 10, padT = 24, padB = 20;
        var plot = new Rect(padL, padT, w - padL - padR, h - padT - padB);
        if (plot.Width < 10 || plot.Height < 10) return;

        bool[] visible = { ShowPrivate, ShowProxy, ShowDirect, ShowOther };
        bool anyVisible = visible.Any(v => v);

        // time window ending at last sample (freezes naturally when paused)
        DateTime end = _cache.Count > 0 ? _cache[^1].Timestamp : DateTime.Now;
        DateTime start = end.AddSeconds(-WindowSeconds);

        // y-scale
        double maxVal = 0;
        if (anyVisible)
        {
            foreach (var s in _cache)
            {
                if (s.Timestamp < start) continue;
                if (visible[0]) maxVal = Math.Max(maxVal, s.PrivateTunnel);
                if (visible[1]) maxVal = Math.Max(maxVal, s.WebProxySwg);
                if (visible[2]) maxVal = Math.Max(maxVal, s.DirectInternet);
                if (visible[3]) maxVal = Math.Max(maxVal, s.OtherUnknown);
            }
        }
        double yCeil = NiceCeiling(maxVal);

        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x1D, 0x24, 0x30)), 1);
        var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x44, 0x58)), 1);
        var labelBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x95, 0xA7));

        // horizontal grid + y labels
        for (int i = 0; i <= 4; i++)
        {
            double v = yCeil * i / 4;
            double y = plot.Bottom - plot.Height * i / 4;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, FormatRate(v), padL - 6, y - 6, labelBrush, 8, TextAlignment.Right);
        }

        // vertical grid + time labels
        for (int i = 0; i <= 4; i++)
        {
            double x = plot.Left + plot.Width * i / 4;
            var t = start.AddSeconds((end - start).TotalSeconds * i / 4);
            dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            DrawText(dc, t.ToString("HH:mm:ss"), x, plot.Bottom + 4, labelBrush, 8, i == 0 ? TextAlignment.Left : i == 4 ? TextAlignment.Right : TextAlignment.Center);
        }
        dc.DrawRectangle(null, axisPen, plot);

        if (_cache.Count == 0 || !anyVisible)
        {
            DrawText(dc, "— NO TELEMETRY —", plot.Left + plot.Width / 2, plot.Top + plot.Height / 2 - 6,
                new SolidColorBrush(Color.FromRgb(0x4A, 0x54, 0x66)), 11, TextAlignment.Center);
        }
        else
        {
            double seconds = (end - start).TotalSeconds;
            double X(TrafficSample s) => plot.Left + (s.Timestamp - start).TotalSeconds / seconds * plot.Width;
            double Y(double v) => plot.Bottom - v / yCeil * plot.Height;

            // polylines
            for (int series = 0; series < 4; series++)
            {
                if (!visible[series]) continue;
                var pen = new Pen(new SolidColorBrush(SeriesColors[series]), 1.6);
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    bool begun = false;
                    foreach (var s in _cache)
                    {
                        if (s.Timestamp < start) continue;
                        double v = series switch
                        {
                            0 => s.PrivateTunnel, 1 => s.WebProxySwg, 2 => s.DirectInternet, _ => s.OtherUnknown,
                        };
                        var p = new Point(X(s), Y(v));
                        if (!begun) { ctx.BeginFigure(p, false, false); begun = true; }
                        else ctx.LineTo(p, true, false);
                    }
                }
                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }

            DrawLegend(dc, plot, visible);
        }

        if (_hasHover && plot.Contains(_hover))
        {
            var idx = FindIndexForX(_hover.X, plot, start, end);
            if (idx >= 0 && idx < _cache.Count)
            {
                var s = _cache[idx];
                double x = plot.Left + (s.Timestamp - start).TotalSeconds / (end - start).TotalSeconds * plot.Width;
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(140, 0xD8, 0xDF, 0xEA)), 1), new Point(x, plot.Top), new Point(x, plot.Bottom));
                DrawTooltip(dc, plot, s);
            }
        }
    }

    private int FindIndexForX(double x, Rect plot, DateTime start, DateTime end)
    {
        if (_cache.Count == 0) return -1;
        double total = (end - start).TotalSeconds;
        if (total <= 0) return _cache.Count - 1;
        double frac = (x - plot.Left) / plot.Width;
        var target = start.AddSeconds(total * frac);
        // binary search nearest by timestamp
        int lo = 0, hi = _cache.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_cache[mid].Timestamp < target) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private void DrawLegend(DrawingContext dc, Rect plot, bool[] visible)
    {
        double x = plot.Left + 8, y = plot.Top + 6;
        foreach (int i in new[] { 2, 1, 0, 3 })
        {
            var b = new SolidColorBrush(SeriesColors[i]);
            dc.DrawLine(new Pen(b, 2), new Point(x, y), new Point(x + 14, y));
            var c = b.Color;
            DrawText(dc, SeriesNames[i], x + 18, y - 5, new SolidColorBrush(visible[i] ? c : Color.FromArgb(70, c.R, c.G, c.B)), 8, TextAlignment.Left);
            x += 18 + SeriesNames[i].Length * 5.2 + 12;
        }
    }

    private void DrawTooltip(DrawingContext dc, Rect plot, TrafficSample s)
    {
        var lines = new[]
        {
            s.Timestamp.ToString("HH:mm:ss"),
            $"PRIVATE  {FormatRate(s.PrivateTunnel)}",
            $"PROXY    {FormatRate(s.WebProxySwg)}",
            $"DIRECT   {FormatRate(s.DirectInternet)}",
            $"OTHER    {FormatRate(s.OtherUnknown)}",
        };
        double tw = 150, th = lines.Length * 12 + 10;
        double x = _hover.X + 12, y = _hover.Y + 12;
        if (x + tw > plot.Right) x = _hover.X - tw - 12;
        if (y + th > plot.Bottom) y = plot.Bottom - th;
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225, 0x11, 0x14, 0x1C)),
            new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x44, 0x58)), 1), new Rect(x, y, tw, th), 3, 3);
        for (int i = 0; i < lines.Length; i++)
            DrawText(dc, lines[i], x + 6, y + 5 + i * 12, new SolidColorBrush(Color.FromRgb(0xD8, 0xDF, 0xEA)), 9, TextAlignment.Left);
    }

    private void DrawText(DrawingContext dc, string text, double x, double y, Brush b, double size, TextAlignment align)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Consolas"), size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        double dx = align switch
        {
            TextAlignment.Right => -ft.Width,
            TextAlignment.Center => -ft.Width / 2,
            _ => 0,
        };
        dc.DrawText(ft, new Point(x + dx, y));
    }

    private static string FormatRate(double bps) => bps switch
    {
        >= 1_000_000_000 => $"{bps / 1_000_000_000:F1}G",
        >= 1_000_000 => $"{bps / 1_000_000:F0}M",
        >= 1_000 => $"{bps / 1_000:F0}K",
        _ => $"{bps:F0}",
    };

    private static double NiceCeiling(double v)
    {
        if (v <= 0) return 1000;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (double m in new[] { 1, 2, 2.5, 5, 10 })
        {
            if (v <= m * mag) return m * mag;
        }
        return 10 * mag;
    }
}
