using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace NetwatchPro;

/// <summary>
/// Loads the bundled Share Tech Mono TTF (a WPF Resource compiled into the assembly)
/// into the process font table so it can be referenced by family name in XAML
/// regardless of what is installed on the machine.
/// </summary>
public static class FontLoader
{
    [DllImport("gdi32.dll")]
    private static extern int AddFontResourceEx(string lpszFilename, uint fl, nint pdv);

    private const uint FR_PRIVATE = 0x10;

    public static void InstallBundledFont()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/Fonts/ShareTechMono-Regular.ttf", UriKind.Absolute);
            var sri = Application.GetResourceStream(uri);
            if (sri == null) return;

            var tmp = Path.Combine(Path.GetTempPath(), "netwatchpro-sharetechmono.ttf");
            using (var stream = sri.Stream)
            using (var fs = File.Create(tmp))
                stream.CopyTo(fs);

            AddFontResourceEx(tmp, FR_PRIVATE, 0);
        }
        catch
        {
            // Non-fatal: the theme falls back to Consolas / Cascadia Mono.
        }
    }
}
