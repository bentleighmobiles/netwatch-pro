using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using NetwatchPro.Models;

namespace NetwatchPro.Controls;

/// <summary>
/// Dark equirectangular world map with graticule, continent silhouettes, local
/// machine marker, remote destination dots and animated connection arcs.
/// Purely a visual aid — CPU/GPU load is throttled.
/// </summary>
public sealed class WorldMap : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(WorldMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(WorldMap),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RotationProperty = DependencyProperty.Register(
        nameof(Rotation), typeof(double), typeof(WorldMap),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public double Rotation { get => (double)GetValue(RotationProperty); set => SetValue(RotationProperty, value); }

    private readonly List<List<Point>> _continents = new(); // lon/lat points
    private readonly DispatcherTimer _anim;
    private double _phase;
    private bool _geoLoaded;

    // local machine (Rowville, VIC, AU)
    private const double LocalLon = 145.25;
    private const double LocalLat = -37.93;

    public WorldMap()
    {
        SnapsToDevicePixels = true;
        ClipToBounds = true;
        LoadGeo();

        _anim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        _anim.Tick += (_, _) => { _phase += 0.012; if (_phase > 1) _phase -= 1; InvalidateVisual(); };
        Loaded += (_, _) => _anim.Start();
        Unloaded += (_, _) => _anim.Stop();
    }

    private void LoadGeo()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/Geo/world-110m.json", UriKind.Absolute);
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri == null) return;
            using var stream = sri.Stream;
            using var doc = JsonDocument.Parse(stream);
            var features = doc.RootElement.GetProperty("features");
            foreach (var f in features.EnumerateArray())
            {
                var geo = f.GetProperty("geometry");
                var type = geo.GetProperty("type").GetString();
                var coords = geo.GetProperty("coordinates");
                if (type == "Polygon") AddOuterRing(coords);
                else if (type == "MultiPolygon")
                    foreach (var poly in coords.EnumerateArray()) AddOuterRing(poly);
            }
            _geoLoaded = _continents.Count > 0;
        }
        catch { _geoLoaded = false; }
    }

    private void AddOuterRing(JsonElement polygon)
    {
        // polygon = array of rings; ring[0] is outer boundary
        if (polygon.ValueKind != JsonValueKind.Array) return;
        var ring = polygon[0];
        var pts = new List<Point>();
        foreach (var pair in ring.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Array) continue;
            double lon = pair[0].GetDouble();
            double lat = pair[1].GetDouble();
            pts.Add(new Point(lon, lat));
        }
        if (pts.Count > 2) _continents.Add(pts);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;

        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x06, 0x08, 0x0E)), null, new Rect(0, 0, w, h));

        double zoom = Math.Clamp(Zoom, 0.5, 4.0);
        var cw = w * zoom;
        var ch = h * zoom;
        double offX = (w - cw) / 2, offY = (h - ch) / 2;

        Point Project(double lon, double lat)
        {
            double x = (lon + 180 + Rotation) / 360 * cw + offX;
            double y = (85 - lat) / 170 * ch + offY;
            return new Point(x, y);
        }

        // graticule
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0x14, 0x1A, 0x26)), 1);
        for (int lon = -180; lon <= 180; lon += 30)
        {
            var p1 = Project(lon, -85); var p2 = Project(lon, 85);
            dc.DrawLine(gridPen, p1, p2);
        }
        for (int lat = -60; lat <= 60; lat += 30)
        {
            var p1 = Project(-180, lat); var p2 = Project(180, lat);
            dc.DrawLine(gridPen, p1, p2);
        }

        // continents
        var landBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x26));
        var landPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x32, 0x42)), 1);
        foreach (var ring in _continents)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                bool first = true;
                foreach (var p in ring)
                {
                    var proj = Project(p.X, p.Y);
                    if (first) { ctx.BeginFigure(proj, true, true); first = false; }
                    else ctx.LineTo(proj, true, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(landBrush, landPen, geo);
        }

        // destinations + arcs from flows
        var flows = new List<Flow>();
        if (ItemsSource != null)
            foreach (var o in ItemsSource)
                if (o is Flow f) flows.Add(f);

        var local = Project(LocalLon, LocalLat);
        var arcPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 0x26, 0xD3, 0xE8)), 1);

        var drawn = new Dictionary<string, Point>();
        int cap = 0;
        foreach (var f in flows)
        {
            if (cap++ > 60) break;
            var key = f.DestinationHost;
            if (!drawn.TryGetValue(key, out var dest))
            {
                dest = Project(PseudoLon(key), PseudoLat(key));
                drawn[key] = dest;
            }

            var color = CategoryColor(f.Category);
            // arc: quadratic bezier with control point raised by distance
            double dx = dest.X - local.X, dy = dest.Y - local.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            var ctrl = new Point((local.X + dest.X) / 2 + dy * 0.12, (local.Y + dest.Y) / 2 - dx * 0.12 - dist * 0.08);

            dc.DrawLine(arcPen, local, dest); // faint direct hint
            DrawBezier(dc, new Pen(new SolidColorBrush(Color.FromArgb(70, color.R, color.G, color.B)), 1.2), local, ctrl, dest);

            // moving pulse
            double t = (_phase + Math.Abs(key.GetHashCode()) % 100 / 100.0) % 1.0;
            var pulse = BezierPoint(local, ctrl, dest, t);
            dc.DrawEllipse(new SolidColorBrush(color), null, pulse, 2.2, 2.2);

            // destination dot
            double size = Math.Clamp(1.5 + Math.Log10(1 + (f.RxRate + f.TxRate)) * 0.55, 1.5, 4.5);
            dc.DrawEllipse(new SolidColorBrush(color), null, dest, size, size);
        }

        // local machine marker
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x5C, 0xFF, 0x6A)), null, local, 4.5, 4.5);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(0x5C, 0xFF, 0x6A)), 1), local, 7 + Math.Sin(_phase * Math.PI * 2) * 2, 7 + Math.Sin(_phase * Math.PI * 2) * 2);
        DrawLabel(dc, "LOCAL", local.X + 8, local.Y - 4, new SolidColorBrush(Color.FromRgb(0x5C, 0xFF, 0x6A)));

        if (!_geoLoaded)
            DrawLabel(dc, "GEO DATA UNAVAILABLE — GRID ONLY", w / 2, h / 2, new SolidColorBrush(Color.FromRgb(0x4A, 0x54, 0x66)));
    }

    private static Color CategoryColor(TrafficCategory c) => c switch
    {
        TrafficCategory.PrivateTunnel => Color.FromRgb(0xA7, 0x8B, 0xFA),
        TrafficCategory.WebProxySwg => Color.FromRgb(0xFF, 0xB0, 0x00),
        TrafficCategory.DirectInternet => Color.FromRgb(0x26, 0xD3, 0xE8),
        _ => Color.FromRgb(0x6B, 0x7A, 0x8F),
    };

    private static void DrawBezier(DrawingContext dc, Pen pen, Point a, Point c, Point b)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(a, false, false);
            ctx.QuadraticBezierTo(c, b, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static Point BezierPoint(Point a, Point c, Point b, double t)
    {
        double u = 1 - t;
        return new Point(u * u * a.X + 2 * u * t * c.X + t * t * b.X,
                         u * u * a.Y + 2 * u * t * c.Y + t * t * b.Y);
    }

    private static double PseudoLon(string host)
    {
        unchecked
        {
            int h = host.GetHashCode();
            return ((h & 0xFFFF) % 360) - 180;
        }
    }

    private static double PseudoLat(string host)
    {
        unchecked
        {
            int h = host.GetHashCode() * 31 + 7;
            return ((h & 0xFFFF) % 140) - 70;
        }
    }

    private void DrawLabel(DrawingContext dc, string text, double x, double y, Brush b)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Consolas"), 8, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(x, y));
    }
}
