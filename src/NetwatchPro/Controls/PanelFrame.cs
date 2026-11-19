using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NetwatchPro.Controls;

/// <summary>
/// Instrument panel chrome: metallic bezel, header with accent LED + title +
/// optional right content, and a CRT well that hosts the body content.
/// </summary>
public sealed class PanelFrame : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(PanelFrame), new PropertyMetadata("PANEL"));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Color), typeof(PanelFrame),
        new FrameworkPropertyMetadata(Color.FromRgb(0x26, 0xD3, 0xE8), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderRightProperty = DependencyProperty.Register(
        nameof(HeaderRight), typeof(object), typeof(PanelFrame), new PropertyMetadata(null));

    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public object? HeaderRight { get => (object?)GetValue(HeaderRightProperty); set => SetValue(HeaderRightProperty, value); }
}
