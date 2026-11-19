using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NetwatchPro.Models;

namespace NetwatchPro.Converters;

internal static class TelemetryBrushes
{
    public static readonly Brush Confirmed = Frozen(Color.FromRgb(0x5C, 0xFF, 0x6A));
    public static readonly Brush Inferred = Frozen(Color.FromRgb(0xFF, 0xB0, 0x00));
    public static readonly Brush Unknown = Frozen(Color.FromRgb(0x6B, 0x7A, 0x8F));
    public static readonly Brush Private = Frozen(Color.FromRgb(0xA7, 0x8B, 0xFA));
    public static readonly Brush Proxy = Frozen(Color.FromRgb(0xFF, 0xB0, 0x00));
    public static readonly Brush Direct = Frozen(Color.FromRgb(0x26, 0xD3, 0xE8));
    public static readonly Brush Other = Frozen(Color.FromRgb(0x6B, 0x7A, 0x8F));
    public static readonly Brush Info = Frozen(Color.FromRgb(0x26, 0xD3, 0xE8));
    public static readonly Brush Warning = Frozen(Color.FromRgb(0xFF, 0xB0, 0x00));
    public static readonly Brush Critical = Frozen(Color.FromRgb(0xFF, 0x3B, 0x4E));

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}

public sealed class ConfidenceToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ConfidenceLevel cl ? cl switch
        {
            ConfidenceLevel.Confirmed => TelemetryBrushes.Confirmed,
            ConfidenceLevel.Inferred => TelemetryBrushes.Inferred,
            _ => TelemetryBrushes.Unknown,
        } : TelemetryBrushes.Unknown;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class CategoryToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TrafficCategory c ? c switch
        {
            TrafficCategory.PrivateTunnel => TelemetryBrushes.Private,
            TrafficCategory.WebProxySwg => TelemetryBrushes.Proxy,
            TrafficCategory.DirectInternet => TelemetryBrushes.Direct,
            _ => TelemetryBrushes.Other,
        } : TelemetryBrushes.Other;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Severity s ? s switch
        {
            Severity.Info => TelemetryBrushes.Info,
            Severity.Warning => TelemetryBrushes.Warning,
            _ => TelemetryBrushes.Critical,
        } : TelemetryBrushes.Info;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class RateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double d ? Models.Flow.FormatBytes(d) + "/s" : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Equality converter for radio-style bindings (enum or int ↔ bool).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null && string.Equals(value.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter == null) return Binding.DoNothing;
        if (targetType.IsEnum) return Enum.Parse(targetType, parameter.ToString()!);
        return System.Convert.ChangeType(parameter, targetType, CultureInfo.InvariantCulture);
    }
}

/// <summary>Returns an accent brush when the current section matches the parameter.</summary>
public sealed class SectionBrushConverter : IValueConverter
{
    private static readonly Brush Selected = Frozen(Color.FromRgb(0x26, 0xD3, 0xE8));
    private static readonly Brush Normal = Frozen(Color.FromRgb(0x8A, 0x95, 0xA7));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null && string.Equals(value.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Selected : Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}

/// <summary>Visible when the current section matches the parameter, otherwise collapsed.</summary>
public sealed class SectionToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null && string.Equals(value.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
