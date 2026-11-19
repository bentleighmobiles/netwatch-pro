# NETWATCH PRO

**REAL-TIME NETWORK DIAGNOSTICS** — a vendor-agnostic Windows endpoint observability
and diagnostics console with the visual language of 1980s/90s telecommunications
test equipment.

Milestone 2 delivers the instrument console driven by **real Windows telemetry**
(CPU/RAM/disk, adapters, throughput, processes, TCP/UDP connections, routes, DNS and
active latency probes), with a one-click fallback to **clearly-labelled simulated data**.
Collection never lives in the UI — a pluggable `ITelemetryProvider` feeds the shared
`MainViewModel`, so SIMULATED ↔ LIVE swaps happen without touching a single view.

---

## 1. Quick start

### Run the prebuilt executable (Windows 10/11 x64)

The self-contained build needs **no .NET runtime installed**:

1. Copy the `publish/win-x64/` folder to the Windows machine.
2. Double-click `NetwatchPro.exe`.

No administrator rights are required for the live collectors in this milestone.

### Build from source

Requirements: **.NET 8 SDK** (or newer) — available from
https://dotnet.microsoft.com/download/dotnet/8.0

```powershell
# Windows (native build)
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64
```

```bash
# Linux/macOS cross-build (compile-only; the app must run on Windows)
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64
```

The project sets `EnableWindowsTargeting=true`, so the WPF project cross-compiles
cleanly from non-Windows hosts (verified on Arch Linux / .NET 8.0.428). The XAML
compiler produces the BAML resources and the C# compiles to IL, but **the WPF app
can only be launched on Windows**.

---

## 2. Project structure

```
src/NetwatchPro/
├── App.xaml / App.xaml.cs          Application entry; installs bundled font
├── MainWindow.xaml / .cs           Shell: header, nav, dashboard, control strip
├── ScreensaverWindow.xaml / .cs    Full-screen presentation mode (5 layouts)
├── Themes/Theme.xaml               Full visual system (brushes, type, styles, chrome)
├── Models/
│   ├── Enums.cs                    OperatingMode, TrafficCategory, Confidence, ...
│   └── TelemetryContracts.cs       UI-facing data entities + label helpers
├── Services/
│   ├── ITelemetryProvider.cs       Provider seam (SIMULATED / LIVE / UNAVAILABLE)
│   ├── WindowsTelemetryProvider.cs Real collectors (Milestone 2)
│   ├── WindowsNative.cs            Kernel/iphlpapi P/Invoke (CPU/RAM/disk, TCP/UDP)
│   ├── SimulatedTelemetryProvider  Simulated collector (random-walk dynamics)
│   └── SettingsService.cs          JSON settings persistence
├── ViewModels/
│   ├── MainViewModel.cs            Single source of truth for the console
│   └── RelayCommand.cs
├── Controls/                       Reusable instrument controls (see §4)
├── Views/                          One UserControl per panel (see §5)
├── Converters.cs                   Value converters (brush/visibility/equality)
├── FontLoader.cs                   Installs bundled Share Tech Mono
└── Assets/
    ├── Fonts/ShareTechMono-Regular.ttf   (OFL licence, bundled)
    └── Geo/world-110m.json               (Natural Earth 110m land, public domain)
```

**Architecture rule (assignment §7):** the UI consumes a stable shared telemetry
model and never contains collection logic. Collection → normalisation →
classification → storage → presentation are separate responsibilities. The
`ITelemetryProvider` seam is what lets a real collector replace the simulator
without rewriting any view.

---

## 3. Data sources

A **DATA SOURCE** selector (Settings) switches the active provider live, and the
header badge + every panel reflect the current source state:

| Source | Behaviour |
|---|---|
| **LIVE** (default) | Real Windows telemetry via kernel/IP-Helper APIs. No admin required. |
| **SIMULATED** | Random-walk demo data, labelled `SIMULATED DATA` in amber. |

### LIVE collectors (Milestone 2)

- **CPU** — `GetSystemTimes` idle/kernel/user deltas.
- **RAM** — `GlobalMemoryStatusEx` (used/total + % load).
- **Disk** — `GetDiskFreeSpaceExW` on the system volume.
- **Adapters** — `System.Net.NetworkInformation` (name, MAC, IPv4/IPv6, gateway,
  DNS, link speed, state), classified physical/virtual/tunnel/loopback.
- **Throughput** — per-interface `GetIPStatistics` byte deltas (IPv4+IPv6), summed
  and bucketed by interface type.
- **Processes** — `Process.GetProcesses()` (PID, name, executable, start time).
- **Connections** — `GetExtendedTcpTable` / `GetExtendedUdpTable` with owner PIDs;
  each flow carries process attribution, endpoint, interface, route and category.
- **Latency / jitter / loss / DNS / connect time** — active probes: ICMP ping to the
  default gateway (fallback 1.1.1.1), DNS resolution timing, TCP connect timing.
- **CPU temperature** — not read in this milestone; displayed as `N/A` (honest).

Every collector degrades to a diagnostic event on failure rather than fabricating
data. A fatal provider failure at startup falls back to SIMULATED automatically.

### Known limitations (Milestone 2)

- TCP/UDP enumeration is **IPv4 only** (IPv6 connection tables are a later milestone).
- Per-flow byte rates are not measured (0 B/s) — that requires TCP estats/ETW (M3).
- Reverse-DNS hostnames are not resolved; the "destination host" column shows the IP.
- Traffic category is derived from interface type + private-address heuristics
  (INFERRED) — no WFP/component evidence yet, so WEB PROXY/SWG is always empty.
- WFP objects, component/driver inventory and packet capture remain **UNAVAILABLE**
  (empty) until Milestones 3–4. Adapter inventory is not hot-reloaded mid-run.

---

## 4. Reusable controls (`Controls/`)

- `RotaryKnob` — draggable, mouse-wheel, configurable min/max, discrete snap.
- `ToggleSwitch` — click flips, illuminated track.
- `PushButton` — pressed/raised states, active illumination, `Command`, disabled.
- `LedIndicator` — glowing LED.
- `AnalogGauge` — arc gauge with needle + digital readout.
- `BarMeter` — segmented LED bar.
- `SegmentDisplay` — seven-segment digits.
- `Sparkline` — compact single-series trend.
- `TrendChart` — multi-series time chart: hover, series toggles, sliding window.
- `WorldMap` — graticule + continent silhouettes + animated connection arcs.
- `PanelFrame` — metallic bezel + header LED + CRT well with scanline overlay.
- `Oscilloscope` / `TrafficMatrix` / `WfpRings` — screensaver visuals.

All instrument controls are drawn with `DrawingContext` (`OnRender`) — no images, no
per-frame template rebuilds, redraw on state change only (assignment §9).

---

## 5. Panels and navigation

Top nav: **OVERVIEW · TRAFFIC · DRIVERS · WFP · CONNECTIONS · ISSUES · ANALYSIS · SETTINGS**.

- **OVERVIEW** — System Overview · Network Traffic · WFP Breakdown · Drivers &
  Destinations · Global Network map · Live Metrics · Flow Attribution · Issues & Events.
- **TRAFFIC** — throughput chart (private/proxy/direct/other), hover, time range, series toggles.
- **DRIVERS** — Drivers / Destinations / Applications tabs.
- **WFP** — event-by-layer bars + WFP object inventory.
- **CONNECTIONS** — flow attribution table with per-flow path inspector.
- **ISSUES** — event log with severity colouring + detail pane.
- **ANALYSIS** — filter/driver stack inspector + network interfaces.
- **SETTINGS** — data source, mode, refresh, thresholds, series, behaviour, classification.

### Bottom hardware control strip

MODE SELECT · TIME RANGE · REFRESH RATE · TRAFFIC FILTER · ZOOM · THRESHOLDS ·
CAPTURE CONTROL · ALERTS. Every control changes real application state; the
capture controls are still **simulated** (size counter only) until Milestone 4.

### Operating modes

- **Light Monitor** (default) — overview, low overhead.
- **Full Diagnostics** — deeper panels enabled.
- **Capture Mode** — capture controls armed; capture is *simulated* (no packets written).
- **Screensaver** — full-screen presentation, auto-rotating every 18s; keys `1`–`5`
  select layout, `↑/↓` brightness, `Esc` exit. Never starts capture.

### Settings persistence

JSON at `%APPDATA%\NetwatchPro\settings.json`, re-applied on launch (including the
data-source selection).

---

## 6. Planned collectors & privilege requirements

| Milestone | Collector | Elevation |
|---|---|---|
| 2 — Windows telemetry ✅ | CPU/RAM/disk, adapters/throughput, processes, connections (TCP/UDP), routes, DNS, active probes | none |
| 3 — WFP & drivers | WFP inventory (`FwpmEngineOpen`), driver inventory (SetupAPI), ETW correlation | **admin** in many cases |
| 4 — Advanced diagnostics | PktMon, ETL→PCAPNG, drop analysis | **admin** |

The UI already surfaces `UNAVAILABLE` / `Permission Denied` honestly and never
silently downgrades a claim.

---

## 7. Verification status

- ✅ `dotnet build -c Release` — **0 warnings, 0 errors** (x64, `net8.0-windows`).
- ✅ Self-contained `win-x64` publish produces `NetwatchPro.exe`.
- ⚠️ Compile-verified on Linux; live collectors **require a Windows launch** to
  confirm runtime behaviour (P/Invoke marshalling, probe cadence, layout).

Roadmap (assignment §13): **2** real system/network telemetry ✅ → **3** WFP +
driver intelligence → **4** PktMon/packet capture + ETL/PCAPNG → **5** baselines,
anomaly detection, explainable-confidence recommendations.

---

## 8. Attribution confidence (assignment §14)

Every attribution is labelled `CONFIRMED` / `INFERRED` / `UNKNOWN` and colour-coded
green/amber/grey. Milestone 2 classifies flows from interface type and private-address
evidence only, so route/category is `INFERRED`; anything without evidence (component,
WFP layer, driver) stays blank. A single flow can traverse multiple components and
layers — the UI shows the observed chain rather than forcing one driver answer.
