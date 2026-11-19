# NETWATCH PRO

**REAL-TIME NETWORK DIAGNOSTICS** — a vendor-agnostic Windows endpoint observability
and diagnostics console with the visual language of 1980s/90s telecommunications
test equipment.

Milestone 1 delivers a **complete, interactive native Windows desktop UI prototype**
driven entirely by **clearly-labelled simulated telemetry**. No real collection
happens yet — every panel, control, graph and readout is functional and interactive,
and the code is structured so real Windows collectors can drop in behind the same
data contracts later without touching the UI.

---

## 1. Quick start

### Run the prebuilt executable (Windows 10/11 x64)

The self-contained build needs **no .NET runtime installed**:

1. Copy the `publish/win-x64/` folder to the Windows machine.
2. Double-click `NetwatchPro.exe`.

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
can only be launched on Windows** — there is no headless runtime check for WPF.

> The bundled solution `NetwatchPro.sln` and project `src/NetwatchPro/NetwatchPro.csproj`
> target `net8.0-windows` (x64).

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
│   ├── SimulatedTelemetryProvider  Simulated collector (random-walk dynamics)
│   └── SettingsService             JSON settings persistence
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
model and never contains collection logic. `MainViewModel` is the contract; the
provider mutates it. Collection → normalisation → classification → storage →
presentation are separate responsibilities. `ITelemetryProvider`-style seams are
represented by the provider/service split so real collectors replace the simulator
without rewriting any view.

---

## 3. UI-facing data model (simulated)

Every value carries provenance. Source states are `SIMULATED`, `LIVE` or
`UNAVAILABLE`; attribution confidence is `CONFIRMED` (green), `INFERRED` (amber)
or `UNKNOWN` (grey) — never overclaimed.

| Entity | Fields (simulated) |
|---|---|
| SystemMetric | name, value, unit, source |
| NetworkInterface | name, type (physical/virtual/tunnel/loopback), MAC, IPv4, gateway, DNS, link speed, throughput, state, confidence |
| Flow | process, PID, local/remote endpoint, protocol, port, interface, route, category, component, WFP layer, driver, rx/tx rate, confidence |
| Component | name, vendor, type, driver, service, version, status, associated traffic, confidence |
| WfpObject | kind (provider/sublayer/filter/callout/layer), id, provider, layer, action, callout, event/permit/block/drop counts |
| DiagnosticEvent | timestamp, severity, source, description, related entity/process |
| CaptureSession | state, size, destination, export format, duration |
| TrafficSample / LatencySample | chart series (per category) and latency/jitter/loss |

`N/A`, `Unknown`, `Unsupported`, `Stale` and `Permission Denied` are represented
distinctly where useful; hardware that isn't simulated (e.g. Wi-Fi signal when
disconnected) renders as `—`/`N/A`, never fabricated.

---

## 4. Reusable controls (`Controls/`)

| Control | Behaviour |
|---|---|
| `RotaryKnob` | draggable, mouse-wheel, configurable min/max, discrete snap (`Steps`), centre readout |
| `ToggleSwitch` | click flips, illuminated track |
| `PushButton` | pressed/raised states, active illumination, `Command` support, disabled state |
| `LedIndicator` | glowing LED, on/off |
| `AnalogGauge` | arc gauge with needle + digital readout |
| `BarMeter` | segmented LED bar, green→amber→red scale |
| `SegmentDisplay` | seven-segment digits (0–9, A–F, dash, point) |
| `Sparkline` | compact single-series trend |
| `TrendChart` | multi-series time chart: hover tooltip, series toggles, sliding time window |
| `WorldMap` | graticule + continent silhouettes + animated connection arcs + destination dots |
| `PanelFrame` | metallic bezel + header LED + CRT well with scanline overlay |
| `Oscilloscope` / `TrafficMatrix` / `WfpRings` | screensaver visuals |

All instrument controls are drawn with `DrawingContext` (`OnRender`) — no images,
no per-frame template tree rebuilds, and they only redraw on state change, keeping
the UI light (assignment §9).

---

## 5. Panels and navigation

Top nav: **OVERVIEW · TRAFFIC · DRIVERS · WFP · CONNECTIONS · ISSUES · ANALYSIS · SETTINGS**.
The selected item is illuminated cyan.

- **OVERVIEW** — full instrument dashboard: System Overview · Network Traffic ·
  WFP Breakdown · Drivers & Destinations · Global Network map · Live Metrics ·
  Flow Attribution · Issues & Events.
- **TRAFFIC** — throughput chart (private/proxy/direct/other), hover, zoom via time
  range, series toggles.
- **DRIVERS** — Drivers / Destinations / Applications tabs.
- **WFP** — event-by-layer bars + full WFP object inventory.
- **CONNECTIONS** — flow attribution table with per-flow path inspector.
- **ISSUES** — event log with severity colouring and a detail pane.
- **ANALYSIS** — filter/driver stack inspector + network interfaces.
- **SETTINGS** — mode, refresh, thresholds, series, behaviour, classification.

### Bottom hardware control strip

| Group | Controls |
|---|---|
| MODE SELECT | Light Monitor · Full Diagnostics · Capture Mode · Screensaver |
| TIME RANGE | rotary: 1m 5m 15m 1h 6h 12h 24h |
| REFRESH RATE | rotary: 1s 5s 10s 30s Manual |
| TRAFFIC FILTER | rotary: All / Private / Proxy / Direct / Other |
| ZOOM | large rotary (0.5×–4×) |
| THRESHOLDS | three knobs: CPU %, latency ms, packet loss % |
| CAPTURE CONTROL | illuminated START · STOP · EXPORT (simulated) |
| ALERTS | warning LED + count + ACK |

Every control changes real application state. No control implies an operation that
did not occur. **A prominent amber `SIMULATED DATA` badge** is always visible, and
the three source states are surfaced throughout.

### Operating modes

- **Light Monitor** (default) — overview only, low overhead.
- **Full Diagnostics** — deeper panels enabled (WFP/driver/stack).
- **Capture Mode** — capture controls armed; capture is *simulated* (size counter
  increments; no packets are written).
- **Screensaver** — full-screen presentation: **Global Network · Network
  Oscilloscope · System Telemetry · Traffic Matrix · WFP Visualiser**, auto-rotating
  every 18s. Keys: `1`–`5` select layout, `↑/↓` brightness, `Esc` exit. Does **not**
  start any capture.

### Settings persistence

Settings are stored as JSON at
`%APPDATA%\NetwatchPro\settings.json` and re-applied on launch.

---

## 6. Simulated data sources & known limitations

Simulated by `SimulatedTelemetryProvider` (random-walk dynamics, seeded once at
startup): CPU/RAM/disk, CPU temperature, per-category throughput, latency/jitter/
loss/DNS/connection-establishment/availability, adapter throughput, flow
inventory + per-flow rates, WFP object inventory + event counts, component/driver
inventory, and an occasional diagnostic event.

Known limitations (Milestone 1):

- **All telemetry is fabricated.** Values are realistic but not from your machine.
- No real CPU/RAM/disk/network counters, no real process/socket correlation, no
  real WFP/ETW/PktMon interaction, no real capture or export.
- Vendor-flavoured component names (e.g. a GSA client, an Umbrella client) appear
  only as **simulated example inventory**, illustrating discovery/classification —
  they are not hardcoded product logic.
- World-map destination positions are pseudo-random but stable per hostname.
- History buffers are bounded (2880 samples) and trimmed FIFO.
- Built and compile-verified on Linux; **not yet run on Windows** — see §8.

---

## 7. Planned collectors & privilege requirements

| Milestone | Collector | Elevation |
|---|---|---|
| 2 — Windows telemetry | CPU/RAM/disk (PDH), adapters/throughput (GetIfTable2 / IP Helper), processes/connections (Toolhelp / GetExtendedTcpTable), routes (GetIpForwardTable2), DNS | none |
| 3 — WFP & drivers | WFP management APIs (`FwpmEngineOpen`), driver inventory (SetupAPI), ETW (Microsoft-Windows-TCPIP, DNS-Client, WFP) | **admin** for WFP/ETW in many cases; graceful degradation otherwise |
| 4 — Advanced diagnostics | PktMon (`pktmon start/stop`, ETL→PCAPNG via `etl2pcapng`), drop analysis | **admin** |

The UI is already structured to show `UNAVAILABLE` / `Permission Denied` states
honestly when elevation is missing, and never silently downgrades a claim.

---

## 8. Verification status & next milestones

- ✅ `dotnet build -c Debug` — **0 warnings, 0 errors** (x64, `net8.0-windows`).
- ✅ Self-contained `win-x64` publish produces `NetwatchPro.exe`.
- ⚠️ Not yet launched on Windows — layout, binding and rendering were validated by
  the XAML/C# compilers only. First Windows launch may surface minor layout tweaks.

Roadmap (assignment §13): **2** real system/network telemetry → **3** WFP + driver
intelligence → **4** PktMon/packet capture + ETL/PCAPNG → **5** baselines, anomaly
detection, explainable-confidence recommendations.

---

## 9. Attribution confidence (assignment §14)

Every attribution is labelled `CONFIRMED` / `INFERRED` / `UNKNOWN` and colour-coded
green/amber/grey. A single flow can traverse multiple components and filtering
layers; the UI shows the observed chain and evidence rather than forcing one
driver answer.
