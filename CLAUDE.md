# DispHub

Windows tray app (WPF, .NET 10) that saves and switches display colour profiles: gamma, contrast, brightness,
colour temperature (gamma ramps) and vibrance (GPU driver APIs), with global hotkeys, per-app rules and schedules.
Sold on the Microsoft Store as free + one-time "Pro" add-on; source is GPL-3.0.

## Commands

```
dotnet build DispHub.slnx -c Release          # must finish with 0 warnings (warnings are errors)
dotnet test --project tests/DispHub.Tests/DispHub.Tests.csproj
dotnet format DispHub.slnx                    # CI runs --verify-no-changes
dotnet run --project src/DispHub -- --fake-store=free   # the free tier and upgrade flow (also: pro, trial, ending, ended)
dotnet run --project src/DispHub -- --fresh             # first run as a new user; your settings stay untouched
pwsh build/package-msix.ps1                   # MSIX for the Store; see the script header
```

To try what this PC doesn't have: `--simulate-windows10` (no window materials, Windows 10's HDR calls),
`--simulate-monitor` (a second, external monitor with DDC/CI brightness, 60/100/144 Hz and HDR, which only exists in
DispHub) and `--simulate-battery` (runs as if on battery). They combine with each other and with `--fresh`.
`.github/workflows/smoke.yml` starts the app on Windows Server 2022 (Windows 10's code base) and keeps screenshots.

`DispHub.exe --diagnose` sweeps gamma ramps on every monitor and writes a hardware report to the logs folder.
`DispHub.exe --exit` asks the running instance to restore colours and quit. `--walkthrough` shows the first-run
walkthrough again and `--tour` starts the guided tour, both keeping your settings. `--fresh` quits the running DispHub
and starts with empty settings in `%TEMP%\DispHub-fresh`, without touching autostart or link registration.

## Layout

- `src/DispHub.Core` (net10.0, no Windows APIs): colour maths (`RampBuilder`, `RampLimits`, `Blackbody`,
  `VibranceMapping`), config model and `ConfigStore`, `RuleEngine` (pure: who wins between pause, manual pick,
  app rule, schedule), `HotkeyGesture`, `FeatureGate` (free vs Pro limits).
- `src/DispHub.Platform` (Windows): monitors via DisplayConfig, `GdiGamma`, vendor vibrance (`NvApi`),
  `ColorApplier` (single colour thread, restore journal), `MonitorBrightness` (laptop screens over WMI, monitors over DDC/CI) and `MonitorModes` (refresh
  rate, HDR), each on its own thread and putting back what it changed, hotkeys, foreground watcher, autostart,
  Store licensing.
  Win32 calls go through CsWin32: add the API name to `NativeMethods.txt`, never hand-write `DllImport`. The one
  exception is `SetWindowCompositionAttribute` (quick panel acrylic), which isn't in the Win32 metadata.
- `src/DispHub` (WPF + WPF-UI): `AppController` owns state and is the only thing views talk to;
  view models in `ViewModels/`, XAML in `Views/`, pages scroll in WPF-UI's `DynamicScrollViewer` (smooth scrolling from FluentFlyout's fork).
- `tests/DispHub.Tests`: xUnit v3. Fakes for the colour applier, hotkeys and display backend live in the test files.
- `packaging/`, `build/`, `store/`: MSIX manifest and assets, scripts, Store listing text.
- `site/`: the website (static HTML, CSS and JS in Fluent 2 style, no build step), published to GitHub Pages by
  `.github/workflows/pages.yml`. Its privacy page mirrors `PRIVACY.md`; change both together.

## Rules

- Never say something works without building and running the tests. For UI changes, also launch the app and look.
- Tests must use temp directories (`TempDirectory`); nothing may touch the real `%LocalAppData%\DispHub`.
- Never send a gamma ramp that `RampLimits` rejects; Windows fails or silently ignores it. Always journal the
  original colours before the first change and restore them on exit.
- Keep Core free of Windows APIs so its logic stays unit-testable.
- Free vs Pro decisions live only in `FeatureGate`. Builds that aren't from the Store are always Pro.
- User-visible text: short, plain English, British spelling ("colour"), no exclamation marks.
- Don't add packages without a clear need; the app must stay light in the background.

## Commits and pull requests

- Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `build:`, `ci:`, `chore:`.
  Subject in the imperative, at most 72 characters.
- PR description: one to three sentences on what changed and why, then a `Testing:` line. No headings,
  no emoji, no file-by-file lists, no "this PR" filler. Screenshots for visible UI changes.
- One topic per PR. Squash-merge into `main`.
