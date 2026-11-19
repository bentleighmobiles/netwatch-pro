using NetwatchPro.Models;

namespace NetwatchPro.Services;

/// <summary>
/// Contract for a telemetry source. The UI (via <see cref="ViewModels.MainViewModel"/>)
/// never knows whether it is reading simulated or real data — a provider mutates the
/// shared model and reports its provenance through <see cref="Source"/>.
/// </summary>
public interface ITelemetryProvider : IDisposable
{
    /// <summary>Origin of the data this provider produces (SIMULATED / LIVE / UNAVAILABLE).</summary>
    SourceState Source { get; }

    /// <summary>Seed inventories synchronously, then begin the refresh loop.</summary>
    void Start();

    /// <summary>Stop the refresh loop (values freeze at their last sample).</summary>
    void Stop();

    /// <summary>One immediate refresh when the console is in MANUAL refresh mode.</summary>
    void ManualRefresh();
}
