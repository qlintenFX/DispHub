---
goal: DispHub UI/UX Enhancement, Hotkey Architecture Refactor, and GPL-3.0 Compliance
version: 1.0
date_created: 2026-03-08
last_updated: 2026-03-08
owner: qlintenFX
status: 'In progress'
tags: feature, refactor, architecture, ui
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20progress-yellow)

Comprehensive enhancement of DispHub covering: fine-grained slider control, close-button behavior fix, dynamic system tray with profile switching, mode-based hotkey architecture (no conflicts), configurable Dynamic Controls keybinds with toggle hotkey, DC page redesign, HomePage addition, FluentFlyout-style About page, NavigationView styling, and GPL-3.0 license compliance with proper attribution.

## 1. Requirements & Constraints

- **REQ-001**: Slider steps must use finest granularity (Gamma: 0.01, Contrast: 0.01, Vibrance: 1)
- **REQ-002**: Close button must respect `CloseToTray` setting — when false, X button must fully exit the app (not just hide the SettingsWindow while tray persists)
- **REQ-003**: System tray right-click menu must list profiles for quick switching; max 10 shown inline, overflow in submenu
- **REQ-004**: Tray profile items must be grayed out when Dynamic Controls mode is active
- **REQ-005**: Configurable global hotkey to toggle DC mode on/off
- **REQ-006**: All 6 DC adjustment keybinds must be user-configurable via HotkeyDialog
- **REQ-007**: Mode-based hotkey registration — only active mode''s hotkeys are registered with Windows. Switching modes unregisters old and registers new. No conflicts possible.
- **REQ-008**: DynamicControlsPage must be redesigned with proper card-based layout, editable keybind cards, and real-time value display
- **REQ-009**: Add HomePage with logo, version, dashboard cards (like FluentFlyout)
- **REQ-010**: About page must match FluentFlyout style — CardExpanders for Developers (qlintenFX) and Translators (grayed out), open source licenses, GitHub link
- **REQ-011**: Credits: qlintenFX as creator of KeyedColors, Hugo Li (unchihugo) as UI/code inspiration, note about free buildable version
- **REQ-012**: GPL-3.0 license file required; proper SPDX headers
- **REQ-013**: Left-click on tray icon opens settings window (already implemented, verify)
- **REQ-014**: NavigationView pane must have rounded top-left corner merging with titlebar area
- **CON-001**: Must remain compatible with WPF-UI 4.2, MicaWPF 6.3.2, net8.0-windows10.0.22000.0
- **CON-002**: No DI container — services remain static properties on MainWindow
- **CON-003**: FluentFlyout is GPL-3.0 — DispHub must be GPL-3.0 since it derives UI code from it
- **GUD-001**: Follow FluentFlyout UI patterns exactly for consistency
- **PAT-001**: _isLoaded guard pattern on all event handlers to prevent XAML-init firing

## 2. Implementation Steps

### Phase 1: Quick Fixes (Sliders, Close Button, Tray Left-Click)

- GOAL-001: Fix immediate UX bugs — slider granularity, close behavior, tray click

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | `Pages/ProfilesPage.xaml`: Change GammaSlider TickFrequency to 0.01, ContrastSlider to 0.01, VibranceSlider to 1. Remove `IsSnapToTickEnabled` from all three. | | |
| TASK-002 | `Pages/ProfilesPage.xaml.cs`: Update GammaValueText format to show 2 decimals, ContrastValueText to show 0 decimals percent, VibranceValueText as int. Already correct — verify only. | | |
| TASK-003 | `SettingsWindow.xaml.cs` OnClosing: When `CloseToTray` is false, call `MainWindow.DisplayManager?.ResetToDefault()`, `MainWindow.HotkeyManager?.Dispose()`, then `Application.Current.Shutdown()` instead of just `base.OnClosing(e)`. | | |
| TASK-004 | Verify tray left-click opens settings window (already implemented in `TrayIcon_LeftClick`). | | |

### Phase 2: Hotkey Architecture Refactor (Mode-Based Registration)

- GOAL-002: Eliminate hotkey conflicts by only registering the active mode''s hotkeys with Windows at any time

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-005 | `MainWindow.xaml.cs`: Add `RegisterProfileHotkeys()` and `UnregisterProfileHotkeys()` methods. `RegisterProfileHotkeys` iterates `ProfileManager.Profiles` and calls `HotkeyManager.RegisterHotkey(profile)`. `UnregisterProfileHotkeys` iterates profiles and unregisters any with `HotkeyId > 0`. | | |
| TASK-006 | `MainWindow.xaml.cs`: Add `SwitchToProfileMode()` — calls `DynamicControls.UnregisterHotkeys(HotkeyManager)`, then `RegisterProfileHotkeys()`. Add `SwitchToDcMode()` — calls `UnregisterProfileHotkeys()`, then `DynamicControls.RegisterHotkeys(HotkeyManager)`. | | |
| TASK-007 | `MainWindow.xaml.cs` MainWindow_Loaded: Replace current dual-registration with mode-based: if `DynamicControlsEnabled`, call `SwitchToDcMode()`, else call `RegisterProfileHotkeys()` only. | | |
| TASK-008 | `MainWindow.xaml.cs` WndProc: Simplify — remove the `DynamicControls.IsEnabled` gate. Since only one mode''s hotkeys are registered at a time, just try DC first, then profile. No conflicts possible. | | |
| TASK-009 | `Pages/DynamicControlsPage.xaml.cs` DynamicControlsToggle_Changed: Replace direct `RegisterHotkeys`/`UnregisterHotkeys` calls with `MainWindow.SwitchToDcMode()` / `MainWindow.SwitchToProfileMode()`. | | |
| TASK-010 | `Pages/ProfilesPage.xaml.cs` SetHotkey_Click: After registering a new profile hotkey, only register it if DC mode is NOT active (profiles mode owns hotkeys). | | |

### Phase 3: Configurable DC Keybinds + Toggle Hotkey

- GOAL-003: Make all DC keybinds and toggle hotkey user-configurable, persisted in settings

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-011 | `Services/Settings/SettingsManager.cs`: Add `DcKeybinds` property — a `DcKeybindSettings` class with 6 pairs: `GammaUpKey/GammaUpMod`, `GammaDownKey/GammaDownMod`, `ContrastUpKey/ContrastUpMod`, `ContrastDownKey/ContrastDownMod`, `VibranceUpKey/VibranceUpMod`, `VibranceDownKey/VibranceDownMod`. Defaults: Shift+Up/Down/Right/Left/PgUp/PgDn. Add `DcToggleKey`/`DcToggleMod` for toggle hotkey (default: Ctrl+Shift+D). | | |
| TASK-012 | `Services/Hotkeys/DynamicControls.cs`: Remove hardcoded VK constants. Change `RegisterHotkeys` to accept keybind config from `SettingsManager.DcKeybinds`. Store the configured keys. | | |
| TASK-013 | `MainWindow.xaml.cs`: Register DC toggle hotkey at startup (always active regardless of mode). Handle in WndProc — toggles between modes via `SwitchToDcMode()`/`SwitchToProfileMode()`. Update `SettingsManager.DynamicControlsEnabled`. Fire event so DC page can update UI. | | |
| TASK-014 | `MainWindow.xaml.cs`: Add static `DynamicControlsModeChanged` event (Action<bool>) so pages can react to hotkey-driven mode switches. | | |

### Phase 4: System Tray Enhancements

- GOAL-004: Dynamic tray context menu with profile switching, DC-aware graying, overflow handling

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | `MainWindow.xaml`: Remove static ContextMenu from XAML. Build it dynamically in code-behind. | | |
| TASK-016 | `MainWindow.xaml.cs`: Add `BuildTrayContextMenu()` method. Structure: "Open DispHub" → Separator → Profile items (max 10, with checkmark on active) → "More profiles..." submenu if >10 → Separator → "Dynamic Controls: On/Off" toggle item → Separator → "Exit". | | |
| TASK-017 | `MainWindow.xaml.cs`: Call `BuildTrayContextMenu()` on startup, on profile add/delete/rename, on mode switch, on profile hotkey apply. Profile items click handler: apply profile, update active indicator, fire `ActiveProfileChanged`. | | |
| TASK-018 | `MainWindow.xaml.cs`: When DC is active, set `IsEnabled = false` on all profile menu items and add "(DC Active)" suffix to the DC toggle item. | | |

### Phase 5: DynamicControlsPage Redesign

- GOAL-005: Modern card-based DC page with editable keybind cards, real-time values, toggle card

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-019 | `Pages/DynamicControlsPage.xaml`: Complete redesign. Top: title. InfoBar for DC active. Toggle card (CardControl with ToggleSwitch). Current Values card with 3 rows showing gamma/contrast/vibrance with colored icons. Keybinds section: 6 CardControl rows, each with label ("Gamma Up"), current keybind display, and "Change" button that opens HotkeyDialog. DC Toggle Hotkey card at bottom. | | |
| TASK-020 | `Pages/DynamicControlsPage.xaml.cs`: Add keybind change handlers. Each "Change" button opens HotkeyDialog, updates `SettingsManager.DcKeybinds`, re-registers DC hotkeys if DC is active. Subscribe to `DynamicControlsModeChanged` to update toggle state when switched via hotkey. | | |

### Phase 6: HomePage + About Page Redesign

- GOAL-006: Add FluentFlyout-style HomePage dashboard and redesign About page with proper credits

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-021 | `Pages/HomePage.xaml`: Create new page. Top: logo + "DispHub" + version. Dashboard section title. 2x2 grid of CardAction tiles: Profiles (Home24 icon, shows active profile name), Dynamic Controls (Gauge20 icon, shows Enabled/Disabled), Settings (Settings24, "Configure"), About (Info20, "Credits & licenses"). Bottom row: GitHub link button, Report Bug button. | | |
| TASK-022 | `Pages/HomePage.xaml.cs`: Create code-behind. Click handlers navigate to respective pages via `SettingsWindow.RootNavigation.Navigate(typeof(...))`. Show active profile name and DC status dynamically. | | |
| TASK-023 | `SettingsWindow.xaml`: Add HomePage as first NavigationViewItem (Icon: Home24, Content: "Home"). Change Profiles icon to something else (e.g., `SlideMultiple24` or `Board24`). | | |
| TASK-024 | `SettingsWindow.xaml.cs` SettingsWindow_Loaded: Navigate to HomePage by default instead of ProfilesPage. | | |
| TASK-025 | `Pages/AboutPage.xaml`: Redesign to match FluentFlyout. Sections: "Contributors" title + description. CardExpander for "Developers" — content: "qlintenFX — Creator of KeyedColors, DispHub developer". CardExpander for "Translators" — grayed out, content: "No translations yet". "Support" section with note: "DispHub is free and open source. You can build it yourself or download it from GitHub." GitHub sponsor/ko-fi links if desired. "Open Source Licenses" section title + description. License cards: FluentFlyout (GPL-3.0, Hugo Li, UI architecture & code inspiration), WPF-UI (MIT), KeyedColors (qlintenFX, original display services), MicaWPF (MIT). Bottom: star icon + "Enjoying DispHub?" + GitHub repo link. | | |
| TASK-026 | `Pages/AboutPage.xaml.cs`: Update to support CardExpander interactions and hyperlink navigation. | | |

### Phase 7: NavigationView Styling

- GOAL-007: Achieve FluentFlyout''s nav pane visual with rounded top-left corner

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-027 | `SettingsWindow.xaml`: Verify NavigationView uses same layout as FluentFlyout (Margin="0,40,0,0", TitleBar as sibling). The rounded corner effect comes from WPF-UI''s NavigationView default style + Mica background. Ensure `NavigationViewContentBackground` brush is Transparent so the content area blends with the Mica backdrop while the nav pane has its own semi-opaque background. | | |
| TASK-028 | `App.xaml`: Add brush overrides if needed: `NavigationViewPaneBackground` to a subtle semi-transparent brush to differentiate the pane from content. Verify the rounded corner rendering by testing with different window sizes. | | |

### Phase 8: GPL-3.0 License & Attribution

- GOAL-008: Full GPL-3.0 compliance since DispHub derives UI code from FluentFlyout (GPL-3.0)

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-029 | Create `LICENSE` file in repo root with full GPL-3.0 text. Copyright line: "Copyright (C) 2026 qlintenFX". Add note: "This software includes code inspired by FluentFlyout by Hugo Li (unchihugo), licensed under GPL-3.0." | | |
| TASK-030 | Add SPDX header comment to key source files: `// SPDX-License-Identifier: GPL-3.0-or-later` in App.xaml.cs, MainWindow.xaml.cs, SettingsWindow.xaml.cs. | | |

### Phase 9: Build, Test, Commit

- GOAL-009: Verify all changes compile and run correctly, then commit

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-031 | Kill any running DispHub.exe, run `dotnet build`, verify 0 errors 0 warnings. | | |
| TASK-032 | Run the app, verify: sliders have fine steps, close button exits when CloseToTray=false, tray menu shows profiles, DC keybinds are editable, HomePage loads, About page has proper credits. | | |
| TASK-033 | Commit all changes with descriptive message and Co-authored-by trailer. | | |

## 3. Alternatives

- **ALT-001**: Could use DI container (Microsoft.Extensions.DependencyInjection) instead of static properties. Rejected: overkill for this app size, would require major refactor.
- **ALT-002**: Could register all hotkeys always and route in WndProc. Rejected: causes Windows RegisterHotKey failures when same keybind used in both modes.
- **ALT-003**: Could use MVVM with ViewModels for all pages. Rejected: code-behind pattern matches FluentFlyout reference and is simpler for this app.

## 4. Dependencies

- **DEP-001**: WPF-UI 4.2 (NavigationView, CardExpander, CardControl, CardAction, InfoBar, TitleBar, SymbolIcon, Anchor)
- **DEP-002**: MicaWPF 6.3.2 (ThemeDictionary for Mica backdrop)
- **DEP-003**: System.Text.Json (settings/profile persistence)
- **DEP-004**: FluentFlyout source (reference only, for UI patterns — GPL-3.0)

## 5. Files

- **FILE-001**: `Pages/ProfilesPage.xaml` — Slider TickFrequency changes
- **FILE-002**: `SettingsWindow.xaml.cs` — Close button behavior fix
- **FILE-003**: `MainWindow.xaml` — Dynamic tray context menu
- **FILE-004**: `MainWindow.xaml.cs` — Mode-based hotkey registration, tray menu, DC toggle hotkey, events
- **FILE-005**: `Services/Settings/SettingsManager.cs` — DcKeybindSettings, DcToggle hotkey config
- **FILE-006**: `Services/Hotkeys/DynamicControls.cs` — Configurable keybinds
- **FILE-007**: `Pages/DynamicControlsPage.xaml` — Complete redesign
- **FILE-008**: `Pages/DynamicControlsPage.xaml.cs` — Keybind editing, mode change subscription
- **FILE-009**: `Pages/HomePage.xaml` — New file (dashboard page)
- **FILE-010**: `Pages/HomePage.xaml.cs` — New file (dashboard code-behind)
- **FILE-011**: `Pages/AboutPage.xaml` — Redesign with CardExpanders, credits
- **FILE-012**: `Pages/AboutPage.xaml.cs` — Updated code-behind
- **FILE-013**: `SettingsWindow.xaml` — Add HomePage nav item
- **FILE-014**: `App.xaml` — Possible brush overrides for nav pane
- **FILE-015**: `LICENSE` — GPL-3.0 license file

## 6. Testing

- **TEST-001**: Slider granularity — drag gamma slider, verify value changes by 0.01 increments
- **TEST-002**: Close behavior — set CloseToTray=false, click X, verify app fully exits (no tray icon remains)
- **TEST-003**: Close behavior — set CloseToTray=true, click X, verify window hides but tray persists
- **TEST-004**: Tray profiles — right-click tray, verify profile list appears, click one, verify display changes
- **TEST-005**: Tray DC gray-out — enable DC mode, right-click tray, verify profile items are grayed out
- **TEST-006**: Keybind conflict — set profile hotkey to Shift+P, set DC gamma-up to Shift+P, switch modes, verify each works in its own mode without errors
- **TEST-007**: DC toggle hotkey — press Ctrl+Shift+D, verify DC toggles on/off and UI updates
- **TEST-008**: DC keybind editing — click Change on a DC keybind card, set new key, verify it works
- **TEST-009**: HomePage — launch app, verify HomePage loads first with correct dashboard cards
- **TEST-010**: About page — verify credits show qlintenFX, Hugo Li attribution, grayed translators

## 7. Risks & Assumptions

- **RISK-001**: CardExpander may not be available in WPF-UI 4.2 — need to verify. Fallback: use regular Card with Expander inside.
- **RISK-002**: Removing static tray ContextMenu from XAML and building dynamically may cause tray icon issues if menu reference is lost.
- **RISK-003**: Mode-based hotkey re-registration may cause brief period where no hotkeys are active during switch.
- **ASSUMPTION-001**: FluentFlyout''s GPL-3.0 requires DispHub to also be GPL-3.0 since we adapted their UI code.
- **ASSUMPTION-002**: WPF-UI''s NavigationView built-in styling provides the rounded corner effect without custom templates.
- **ASSUMPTION-003**: The user''s settings.json on disk may have stale values — all new settings fields must have sensible defaults.

## 8. Related Specifications / Further Reading

- [FluentFlyout GitHub](https://github.com/unchihugo/FluentFlyout) — UI reference (GPL-3.0)
- [KeyedColors GitHub](https://github.com/qlintenFX/KeyedColors) — Original display services
- [WPF-UI Documentation](https://wpfui.lepo.co/) — Control reference
- [GPL-3.0 License Text](https://www.gnu.org/licenses/gpl-3.0.en.html)
