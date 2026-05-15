---
goal: Round 8 — Language cleanup, master toggle hotkey, profile card contrast, widget left-position fix
version: 1.0
date_created: 2025-01-27
last_updated: 2025-01-27
owner: qlintenFX
status: 'Planned'
tags: [feature, bug, ui-polish, refactor]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Round 8 stabilization addressing four user-reported issues: (1) Replace gimmicky `power` language with natural `DispHub enabled/disabled` terminology, (2) add a master toggle hotkey in Settings page, (3) fix active profile card visual contrast when DispHub is disabled, and (4) fix taskbar widget disappearing at Left position without auto-padding.

## 1. Requirements & Constraints

- **REQ-001**: Replace all `Display Power Off/On` language with `DispHub Disabled/Enabled` or equivalent natural language throughout all UI, tooltips, tray menu, log messages, and widget settings
- **REQ-002**: Add a master toggle hotkey setter in the Settings page that registers a global hotkey calling `MainWindow.ToggleDisplayPower()`
- **REQ-003**: When DispHub is disabled, the active profile card must visually contrast with inactive cards — not use full Primary (accent) appearance, but still be distinguishable as the `will-resume` profile
- **REQ-004**: Taskbar widget at Left position must be visible regardless of auto-padding setting — always detect system button area as minimum offset
- **CON-001**: No breaking changes to existing `settings.json` schema — new fields use sensible defaults and missing fields are handled gracefully by `JsonSerializer.Deserialize`
- **CON-002**: Master toggle hotkey must coexist with DC toggle hotkey and profile hotkeys without conflicts
- **GUD-001**: Follow existing code patterns — `SettingsManager` property + `SettingsData` field pattern for new settings, `HotkeyDialog` for hotkey capture, `RegisterRawHotkey`/`UnregisterRawHotkey` for global hotkeys
- **PAT-001**: WndProc dispatch: new hotkey ID checked before DC/profile dispatch block

## 2. Implementation Steps

### Phase 1 — Language Rename: `Power` → Natural `DispHub` Terminology

- GOAL-001: Replace all gimmicky `power` language with natural, professional terminology across the entire codebase

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | `Pages/ProfilesPage.xaml`: Change `PowerOffBar` Title from `Display Power Off` to `DispHub Disabled`, Message from `Turn display power back on to manage profiles.` to `Enable DispHub to manage profiles.` | | |
| TASK-002 | `Pages/DynamicControlsPage.xaml`: Change `PowerOffBar` Title from `Display Power Off` to `DispHub Disabled`, Message from `Turn display power back on to use Dynamic Controls.` to `Enable DispHub to use Dynamic Controls.` | | |
| TASK-003 | `Pages/TaskbarWidgetPage.xaml`: Change `Hide When Powered Off` to `Hide When Disabled`, subtitle from `Completely hide the widget when display power is off` to `Hide the widget when DispHub is disabled` | | |
| TASK-004 | `SettingsWindow.xaml`: Change `PowerButton` ToolTip from `Toggle Display Power (On/Off)` to `Toggle DispHub` | | |
| TASK-005 | `SettingsWindow.xaml.cs`: Update `UpdatePowerButtonVisual()` tooltips: active → `DispHub: Active — click to disable`, inactive → `DispHub: Inactive — click to enable` | | |
| TASK-006 | `MainWindow.xaml.cs`: Update tray menu `powerItem.Header` from `⏻ Power: Off/On` to `DispHub: Off/On`. Update Logger messages from `Display powered OFF/ON` to `DispHub disabled/enabled` | | |

### Phase 2 — Master Toggle Hotkey

- GOAL-002: Add a global hotkey to toggle DispHub on/off, configurable from the Settings page

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | `Services/Settings/SettingsManager.cs`: Add `MasterToggleKey` (uint, default 0) and `MasterToggleMod` (uint, default 0) to `SettingsData` and corresponding properties in `SettingsManager` | | |
| TASK-008 | `MainWindow.xaml.cs`: Add `_masterToggleHotkeyId` field (int, init -1). Add `RegisterMasterToggleHotkey()` method (pattern: unregister old, register new if key != 0). Call from `MainWindow_Loaded` after `RegisterDcToggleHotkey()`. Add public `UpdateMasterToggleHotkey()` for Settings page to call after changing the hotkey | | |
| TASK-009 | `MainWindow.xaml.cs`: In `WndProc`, add check for `_masterToggleHotkeyId` BEFORE the `IsDisplayActive` early-return guard (master toggle must work even when disabled). Call `ToggleDisplayPower()` on match | | |
| TASK-010 | `MainWindow.xaml.cs`: In `UnregisterAllHotkeys()`, unregister `_masterToggleHotkeyId` if > 0 | | |
| TASK-011 | `Pages/SettingsPage.xaml`: Add `Hotkeys` section after `Tray Icon` section with a `CardControl` for `Master Toggle Hotkey` — header: `Toggle DispHub`, subtitle: `Global hotkey to enable/disable DispHub`, content: `ui:Button` with `Consolas` font showing current keybind | | |
| TASK-012 | `Pages/SettingsPage.xaml.cs`: Add `MasterToggleKeybind_Click` handler: open `HotkeyDialog`, save to `SettingsManager.MasterToggleKey/Mod`, update label, call `MainWindow.UpdateMasterToggleHotkey()`. Load current value in `SettingsPage_Loaded` | | |

### Phase 3 — Active Profile Card Contrast When Disabled

- GOAL-003: Make the active profile card visually distinct but subdued when DispHub is disabled, so it's clear which profile will resume on re-enable

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-013 | `Pages/ProfilesPage.xaml.cs`: Modify `RefreshProfileCards()` — when `!MainWindow.IsDisplayActive`, use `ControlAppearance.Secondary` for ALL cards (including active) but add a visible left border accent (3px, accent color) and slightly bolder text to the active card to distinguish it. The active card text foreground should use `TextFillColorSecondaryBrush` (dimmed) while inactive cards use default | | |
| TASK-014 | `Pages/ProfilesPage.xaml.cs`: In `SyncPowerState()` and `OnPowerChanged()`, call `RefreshProfileCards()` to update card visuals when power state changes | | |

### Phase 4 — Widget Left-Position Fix

- GOAL-004: Ensure taskbar widget is always visible at Left position by always detecting system button area as minimum offset, regardless of auto-padding setting

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | `Windows/TaskbarWidgetWindow.xaml.cs`: In `CalculateHorizontalPosition` case 0 (Left): ALWAYS detect `DesktopWindowContentBridge` as the system button area baseline. Use its right edge + 4px as the minimum offset (fallback to 60px if not found). Auto-padding toggle only controls whether to add further smart avoidance. Manual padding always added on top | | |

## 3. Alternatives

- **ALT-001**: Master toggle hotkey could be placed on a dedicated `Hotkeys` page — rejected as overkill for a single hotkey; Settings page is the logical home alongside other global app behaviors
- **ALT-002**: Profile card active-when-disabled could use opacity reduction instead of border accent — rejected as it would make the card hard to read; border accent is more visible and follows Fluent design patterns
- **ALT-003**: Widget left-position could use a hardcoded large offset (200px) when auto-padding is off — rejected as it's fragile and varies by system; always detecting system elements is more robust

## 4. Dependencies

- **DEP-001**: `HotkeyDialog` — existing dialog for capturing hotkey input (used by TASK-011/012)
- **DEP-002**: `HotkeyManager.RegisterRawHotkey/UnregisterRawHotkey` — existing methods for raw hotkey registration (used by TASK-008/009/010)
- **DEP-003**: `NativeMethods.FindWindowEx` — existing P/Invoke for `DesktopWindowContentBridge` detection (used by TASK-015)

## 5. Files

- **FILE-001**: `Pages/ProfilesPage.xaml` — InfoBar text rename (TASK-001)
- **FILE-002**: `Pages/ProfilesPage.xaml.cs` — Profile card contrast logic (TASK-013, TASK-014)
- **FILE-003**: `Pages/DynamicControlsPage.xaml` — InfoBar text rename (TASK-002)
- **FILE-004**: `Pages/TaskbarWidgetPage.xaml` — Widget setting text rename (TASK-003)
- **FILE-005**: `SettingsWindow.xaml` — Power button tooltip rename (TASK-004)
- **FILE-006**: `SettingsWindow.xaml.cs` — Tooltip text update (TASK-005)
- **FILE-007**: `MainWindow.xaml.cs` — Tray menu text, master toggle hotkey registration/dispatch, log messages (TASK-006, TASK-008, TASK-009, TASK-010)
- **FILE-008**: `Services/Settings/SettingsManager.cs` — New MasterToggleKey/Mod settings (TASK-007)
- **FILE-009**: `Pages/SettingsPage.xaml` — Master toggle hotkey UI (TASK-011)
- **FILE-010**: `Pages/SettingsPage.xaml.cs` — Master toggle hotkey handler (TASK-012)
- **FILE-011**: `Windows/TaskbarWidgetWindow.xaml.cs` — Left-position fix (TASK-015)

## 6. Testing

- **TEST-001**: Build succeeds with `dotnet build` — no compilation errors
- **TEST-002**: Launch app, verify all InfoBar messages show `DispHub Disabled` (not `Display Power Off`)
- **TEST-003**: Toggle power button, verify tray menu shows `DispHub: Off/On`
- **TEST-004**: Set master toggle hotkey in Settings, press hotkey, verify DispHub toggles on/off
- **TEST-005**: When DispHub is disabled, verify active profile card has accent border but subdued appearance, inactive cards have no border
- **TEST-006**: Re-enable DispHub, verify active profile card returns to full Primary appearance
- **TEST-007**: Set widget position to Left with auto-padding OFF, verify widget is visible (not hidden behind Start button)
- **TEST-008**: Set widget position to Left with auto-padding ON, verify widget positions after system elements

## 7. Risks & Assumptions

- **RISK-001**: `DesktopWindowContentBridge` window may not exist on Windows 10 or some Windows 11 configurations — mitigated with fallback to 60px minimum offset
- **RISK-002**: Master toggle hotkey ID could theoretically collide with DC toggle hotkey ID if `RegisterRawHotkey` uses overlapping ID space — mitigated by using unique counter in `HotkeyManager`
- **ASSUMPTION-001**: `HotkeyManager.RegisterRawHotkey` returns unique IDs that don't collide with profile or DC hotkey IDs
- **ASSUMPTION-002**: The accent brush `SystemAccentColorPrimaryBrush` is available in WPF-UI's resource dictionaries for the profile card border

## 8. Related Specifications / Further Reading

- [feature-stabilization-round7-1.md](feature-stabilization-round7-1.md) — Prior round 7 plan
- [FluentFlyout GitHub](https://github.com/unchihugo/FluentFlyout) — UI reference, GPL-3.0
- [KeyedColors GitHub](https://github.com/qlintenFX/KeyedColors) — Original app logic
