using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace NetwatchPro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Install the bundled retro-monospace font from embedded resources so the
        // visual system is identical on machines that lack Share Tech Mono.
        FontLoader.InstallBundledFont();

        DispatcherUnhandledException += (s, args) =>
        {
            // Keep the monitor alive on transient binding/render faults rather than crashing out.
            System.Diagnostics.Debug.WriteLine($"[NETWATCH PRO] Unhandled: {args.Exception}");
            args.Handled = true;
        };
    }
}
