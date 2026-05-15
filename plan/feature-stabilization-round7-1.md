---
goal: DispHub Post-Migration Stabilization and Feature Completion (Round 7)
version: 1.0
date_created: 2025-07-18
last_updated: 2025-07-18
owner: qlintenFX
status: 'Planned'
tags: [bug, feature, migration, ui, stabilization]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

Comprehensive stabilization plan for DispHub after migrating from KeyedColors (WinForms) to WPF-UI architecture inspired by FluentFlyout. This addresses all remaining bugs, missing features, UI/UX polish, and code quality issues identified through full codebase audit after Round 6 (commit `137a956`).

## 1. Requirements & Constraints

- **REQ-001**: All hotkeys must work correctly — startup, power toggle cycle, mode switch cycle
- **REQ-002**: Power toggle must fully disable/re-enable all features and restore state on power-on
- **REQ-003**: Taskbar widget must dock correctly without colliding with system tray icons
- **REQ-004**: Scrolling must work everywhere via mouse wheel (not just scrollbar drag)
- **REQ-005**: Close button behavior (exit vs minimize-to-tray) must respect user setting
- **REQ-006**: System tray left-click behavior must work per setting
- **REQ-007**: Profile flyout popup must appear cleanly on hotkey switch
- **REQ-008**: All settings must persist across app restarts via `%APPDATA%\DispHub\`
- **REQ-009**: GPL-3.0 license compliance with FluentFlyout credit
- **SEC-001**: No hardcoded credentials or sensitive data in source
- **CON-001**: Must target .NET 8 with WPF-UI 4.2.0, x64 only
- **CON-002**: NVIDIA vibrance requires nvapi64.dll at runtime (graceful fallback if missing)
- **GUD-001**: Follow FluentFlyout's architectural patterns for taskbar integration
- **GUD-002**: Maintain clean separation: MainWindow = tray host, SettingsWindow = UI
- **PAT-001**: Mode-based hotkey architecture — only active mode's keys registered
- **PAT-002**: Static service properties on MainWindow (no DI container)

## 2. Implementation Steps

### Implementation Phase 1: Critical Bug Fixes

- GOAL-001: Fix all critical bugs that prevent core functionality from working

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | **Fix scrolling**: SmoothScrollBehavior exists but is never attached to NavigationView's internal ScrollViewer. The NavigationView has its own ScrollViewer but SmoothScrollBehavior.IsEnabled is not set on it. Need to find the ScrollViewer in SettingsWindow after navigation and attach the behavior, OR handle PreviewMouseWheel at the page/NavigationView level to forward scroll events. Currently mouse wheel scrolling doesn't work because pages are bare StackPanels inside NavigationView which has a ScrollViewer that may not receive wheel events when focus is elsewhere. | | |
| TASK-002 | **Fix close button behavior**: `SettingsWindow.OnClosing()` checks `CloseToTray` but when `CloseToTray=false` it calls `Application.Current.Shutdown()` directly. However, the default `CloseToTray` value is `false` in `SettingsData`, meaning first-time users will fully quit when clicking X. More critically, the close behavior radio button state may not sync properly — verify the setting persists and the X button respects it in all scenarios. | | |
| TASK-003 | **Fix tray left-click**: `TrayIcon_LeftClick` only checks behavior 0 (Open Settings). Behavior 1 (Do Nothing) works by omission, but there's no explicit handling. Verify the WPF-UI tray icon doesn't have default click behavior that overrides this. Also the `FocusOnLeftClick="False"` may interfere. | | |
| TASK-004 | **Fix profile card click applying settings**: When clicking a profile card in ProfilesPage, `OnProfileCardClicked` calls `ApplyCurrentSliderValues()` which applies display settings. But it doesn't call `MainWindow.ApplyProfile(index)` — so `_activeProfileIndex` in MainWindow is never updated, the tray menu isn't rebuilt, the widget isn't updated, and the flyout doesn't show. Profile card clicks should route through `MainWindow.ApplyProfile()` for consistent state. | | |
| TASK-005 | **Fix slider TickFrequency**: GammaSlider has `TickFrequency="0.01"` but no `IsSnapToTickEnabled="True"`. Without snap-to-tick, the slider may not land on exact tick values. Add `IsSnapToTickEnabled="True"` to all profile sliders for precise control. Also verify the slider step is truly the smallest useful increment (0.01 for gamma/contrast, 1 for vibrance). | | |

### Implementation Phase 2: Power Toggle Hardening

- GOAL-002: Ensure power toggle works flawlessly in all edge cases

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-006 | **Verify PowerOn restores correct profile**: `PowerOn()` restores `_activeProfileIndex` but if user had DC mode active before power-off, PowerOn should restore DC mode with its current values, not try to restore a profile. Current code checks `DynamicControls.IsEnabled` which should still be true after power-off (only hotkeys are unregistered, not the mode flag). Verify this flow works. | | |
| TASK-007 | **Power toggle + mode switch interaction**: If user powers off, then navigates to DC page and toggles DC on, the `SwitchToDcMode()` guard `if (!IsDisplayActive) return;` prevents it. But the UI toggle still flips. Disable the DC toggle switch when power is off (already partially done via `SyncPowerState` in DynamicControlsPage, but verify it also disables the toggle switch itself). | | |
| TASK-008 | **Power button visual on SettingsWindow reopen**: If user closes SettingsWindow, toggles power via tray, then reopens SettingsWindow — the power button visual may not update because `UpdatePowerButtonVisual` is called in constructor via `DisplayPowerChanged` event, but the window is new. Verify the `Loaded` handler calls `UpdatePowerButtonVisual()` (it does — confirmed OK). | | |

### Implementation Phase 3: Taskbar Widget Stabilization

- GOAL-003: Make taskbar widget reliable and visually correct

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-009 | **Test widget positioning**: Verify the right-align position actually finds TrayNotifyWnd and positions correctly. Test with different DPI scales (100%, 125%, 150%). The `GetDpiForWindow` P/Invoke may not be available on all Windows versions — add fallback. | | |
| TASK-010 | **Widget visibility on first show**: `TaskbarWidgetWindow.xaml` has `Visibility="Collapsed"` but the `SetupWindow()` method uses `SetWindowPos` with `SWP_SHOWWINDOW` flag. Verify the window actually becomes visible after docking. May need explicit `Visibility = Visibility.Visible` after SetWindowPos. | | |
| TASK-011 | **Widget text truncation**: Widget width is 130px. Long profile names like "Ultra Vivid Night Mode" will truncate. The TextBlock has `TextTrimming="CharacterEllipsis"` and `MaxWidth="120"` which is correct, but verify it looks good visually. | | |
| TASK-012 | **Widget cleanup on app exit**: `ExitApplication()` calls `HideTaskbarWidget()` which calls `StopAndClose()`. Verify the widget is properly un-parented from the taskbar and the position timer is stopped. Potential issue: `Close()` on a child window that was SetParent'd may not work correctly — may need to call `SetParent(handle, IntPtr.Zero)` first (which `StopAndClose` does). | | |
| TASK-013 | **Widget position timer performance**: The 1500ms timer calls `CalculateAndSetPosition` which does P/Invoke calls every tick. This is acceptable for battery life but could be reduced to 3000ms for efficiency. Also the timer runs even if the widget position hasn't changed — add a check to skip if position hasn't moved. | | |

### Implementation Phase 4: UI/UX Polish

- GOAL-004: Polish the visual design and user experience

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-014 | **NavigationView title bar integration**: FluentFlyout has the NavigationView pane merging with the title bar area creating a cohesive rounded-corner design. Current DispHub has `Margin="0,40,0,0"` on NavigationView to avoid overlapping the TitleBar. Research if setting the NavigationView's `PaneHeader` or adjusting margins can achieve the integrated look. The key is the background behind the nav pane being a different color than the content area, with rounded inner corner. | | |
| TASK-015 | **Theme ComboBox dropdown always white**: The user reported the theme dropdown selector itself is always white regardless of dark mode. This is likely because the ComboBox popup uses system theme, not WPF-UI theme. WPF-UI ComboBox should handle this — verify the ComboBox is using `ui:ComboBox` from the WPF-UI namespace, or if it's using the stock WPF ComboBox which doesn't support dark mode. | | |
| TASK-016 | **Profile cards visual design**: Profile cards use `Wpf.Ui.Controls.Button` with Primary/Secondary appearance. The user complained about "blue color and hard corners nothing rounded or in style". The Button control should already have rounded corners from WPF-UI. Verify the cards look good in both light and dark mode. Consider using `ui:Card` or `ui:CardControl` instead of buttons for a more cohesive look with the rest of the page. | | |
| TASK-017 | **HomePage dashboard cards**: The CardAction items look good but `ProfileStatusText` and `DcStatusText` are only updated in `OnNavigatedToAsync`. If the user switches profiles or enables DC and comes back to Home, the text won't update. Subscribe to `ActiveProfileChanged` and `DynamicControlsModeChanged` events. | | |
| TASK-018 | **Preview image placeholders**: All pages have preview placeholders with generic icons. These need actual screenshots or illustrations added. Document what each placeholder should show for the user to add images later. | | |

### Implementation Phase 5: Code Quality & Best Practices

- GOAL-005: Clean up code quality issues and apply .NET best practices

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-019 | **Remove unused NLog dependency**: `DispHub.csproj` references `NLog 6.0.6` but `Services/Logging/Logger.cs` likely uses a custom file logger (not NLog). If NLog isn't used anywhere, remove the package reference to reduce binary size. | | |
| TASK-020 | **Remove unused CommunityToolkit.Mvvm**: The project references `CommunityToolkit.Mvvm 8.4.0` but no classes use `ObservableObject`, `RelayCommand`, or other MVVM toolkit types. If unused, remove. | | |
| TASK-021 | **Static service pattern cleanup**: All services are static properties on MainWindow. This works but creates tight coupling. For now, keep the pattern (matches FluentFlyout) but document it clearly. Ensure no page accesses services before `MainWindow_Loaded` completes. | | |
| TASK-022 | **IDisposable implementation audit**: `DisplayManager` implements `IDisposable` (disposes vibrance service). `HotkeyManager` implements `IDisposable` (unregisters all hotkeys). Verify both are properly disposed in `ExitApplication()`. Currently `HotkeyManager.Dispose()` is called but `DisplayManager.Dispose()` is not — add it. | | |
| TASK-023 | **Event handler leak audit**: Pages subscribe to static events (`ActiveProfileChanged`, `DynamicControlsModeChanged`, `DisplayPowerChanged`) in `OnPageLoaded` and unsubscribe in `OnPageUnloaded`. This is correct. But `SettingsWindow` subscribes to `DisplayPowerChanged` in constructor and never unsubscribes. Since SettingsWindow is recreated each time, the old handler should be GC'd with the window, but it's cleaner to unsubscribe in `OnClosing`. | | |
| TASK-024 | **Logger audit**: Verify `Logger.Initialize()` creates the log file in `%APPDATA%\DispHub\`. Check that `Logger.LogError()` doesn't throw if the file is locked or the directory doesn't exist. | | |
| TASK-025 | **Profile name editing**: Currently profiles can only be renamed by... they can't. The `AddProfile_Click` creates a profile with auto-generated name "Profile N" but there's no UI to rename. Need to add a rename button or inline editing (double-click the card to edit name, or a TextBox that appears). | | |

### Implementation Phase 6: Missing Features

- GOAL-006: Implement features that were planned but not yet done

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-026 | **System tray active profile indicator**: The tray context menu shows "Active: {name}" as a disabled menu item. This works but could be enhanced with a checkmark on the active profile's menu item. Currently `IsChecked = index == _activeProfileIndex` on profile items — verify this renders as a checkmark in the WPF context menu. | | |
| TASK-027 | **Profile grayed out in tray when DC active**: The tray menu disables profile items when DC is active (`IsEnabled = !profilesDisabled`). Verify the visual appears grayed out (it should with standard WPF MenuItem). | | |
| TASK-028 | **Flyout shows on tray profile switch**: `ApplyProfile()` calls `ShowProfileFlyout()` when `FlyoutEnabled`. This covers both tray clicks and hotkey switches. Verified — this is correct. | | |
| TASK-029 | **DC toggle hotkey in tray**: The DC toggle hotkey is registered but there's no way to trigger it from the tray menu other than clicking "Dynamic Controls: On/Off". This is fine as-is — the tray item toggles DC mode. | | |
| TASK-030 | **Hotkey conflict detection**: User mentioned that if the same key combo is used for both a profile hotkey and a DC hotkey, it causes bugs. Since only one mode's hotkeys are registered at a time, this shouldn't actually conflict at the OS level. But the UI should warn the user if they set a duplicate key. Add duplicate detection in `HotkeyDialog` or in the save logic. | | |
| TASK-031 | **Profile reorder**: No way to reorder profiles in the list. Consider drag-and-drop or up/down buttons. Low priority. | | |

### Implementation Phase 7: Testing & Verification

- GOAL-007: Verify all functionality works end-to-end

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-032 | **Manual test: Full startup flow** — Launch app, verify tray icon appears, settings window opens, HomePage shows correct profile count, DC status. | | |
| TASK-033 | **Manual test: Profile hotkey cycle** — Set hotkeys on 2 profiles, press each, verify display changes, UI updates (profile cards, tray menu, widget, flyout). | | |
| TASK-034 | **Manual test: Power toggle cycle** — Power off, verify hotkeys stop working, power on, verify hotkeys resume and profile is restored. | | |
| TASK-035 | **Manual test: DC mode cycle** — Enable DC, verify profile hotkeys stop, DC hotkeys work, disable DC, verify profile hotkeys resume. | | |
| TASK-036 | **Manual test: Close behavior** — Set close-to-tray, close window, verify it hides. Set close-to-exit, close window, verify app exits. | | |
| TASK-037 | **Manual test: Widget positions** — Test Left, Center, Right positions at 100% DPI scale. Verify right-aligned doesn't overlap system tray. | | |
| TASK-038 | **Manual test: Settings persistence** — Change all settings, close and relaunch, verify all settings restored. | | |

## 3. Alternatives

- **ALT-001**: Could use MVVM with dependency injection instead of static services. Not chosen because FluentFlyout uses the same static pattern and it works well for this app size.
- **ALT-002**: Could use ReactiveUI for event-driven state management. Not chosen — adds complexity for minimal benefit at this scale.
- **ALT-003**: Could use the Windows App SDK (WinUI 3) instead of WPF-UI. Not chosen — would require complete rewrite and loses FluentFlyout code compatibility.

## 4. Dependencies

- **DEP-001**: WPF-UI 4.2.0 — Fluent 2 design controls and tray icon
- **DEP-002**: WPF-UI.Tray 4.2.0 — System tray NotifyIcon
- **DEP-003**: MicaWPF 6.3.2 — Mica backdrop effect
- **DEP-004**: .NET 8 (net8.0-windows10.0.22000.0) — Target framework
- **DEP-005**: nvapi64.dll — NVIDIA digital vibrance (optional runtime dependency)
- **DEP-006**: Win32 APIs (user32, gdi32) — Gamma ramp, hotkeys, taskbar integration

## 5. Files

- **FILE-001**: `MainWindow.xaml.cs` — Core app logic: tray, hotkeys, power, mode switching, widget
- **FILE-002**: `SettingsWindow.xaml` / `.cs` — NavigationView, title bar, power button, theme
- **FILE-003**: `Pages/ProfilesPage.xaml` / `.cs` — Profile management, sliders, cards, actions
- **FILE-004**: `Pages/DynamicControlsPage.xaml` / `.cs` — DC toggle, keybind editing, values display
- **FILE-005**: `Pages/SettingsPage.xaml` / `.cs` — Theme, close behavior, startup, tray config
- **FILE-006**: `Pages/HomePage.xaml` / `.cs` — Dashboard with cards, quick links
- **FILE-007**: `Pages/AboutPage.xaml` / `.cs` — Credits, licenses, support links
- **FILE-008**: `Pages/FlyoutPage.xaml` / `.cs` — Flyout enable/duration settings
- **FILE-009**: `Pages/TaskbarWidgetPage.xaml` / `.cs` — Widget enable/position/padding settings
- **FILE-010**: `Windows/TaskbarWidgetWindow.xaml` / `.cs` — Win32 taskbar-docked widget
- **FILE-011**: `Windows/ProfileFlyoutWindow.xaml` / `.cs` — Profile switch notification popup
- **FILE-012**: `Controls/TaskbarWidgetControl.xaml` / `.cs` — Widget visual with text, hover
- **FILE-013**: `Services/Settings/SettingsManager.cs` — JSON settings persistence
- **FILE-014**: `Services/Profiles/ProfileManager.cs` — JSON profile persistence
- **FILE-015**: `Services/Hotkeys/HotkeyManager.cs` — Win32 hotkey registration
- **FILE-016**: `Services/Hotkeys/DynamicControls.cs` — Real-time display adjustment via hotkeys
- **FILE-017**: `Services/Display/DisplayManager.cs` — Gamma ramp + vibrance application
- **FILE-018**: `Helpers/NativeMethods.cs` — Win32 P/Invoke declarations
- **FILE-019**: `Helpers/SmoothScrollBehavior.cs` — Smooth animated scrolling behavior
- **FILE-020**: `Models/Profile.cs` — Profile data model
- **FILE-021**: `Constants/AppConstants.cs` — App-wide constants

## 6. Testing

- **TEST-001**: Build succeeds with 0 errors and 0 warnings
- **TEST-002**: App launches without crash, tray icon appears, settings window opens
- **TEST-003**: Profile hotkeys register and fire correctly in profile mode
- **TEST-004**: DC hotkeys register and fire correctly in DC mode
- **TEST-005**: Power off → on cycle preserves and restores hotkey registration
- **TEST-006**: Mode switch (profile ↔ DC) correctly un/registers hotkeys for each mode
- **TEST-007**: Settings persist to JSON and restore on app restart
- **TEST-008**: Close-to-tray vs close-to-exit works as configured
- **TEST-009**: Widget docks into taskbar at all 3 positions without overlap
- **TEST-010**: Flyout popup appears and animates correctly on profile switch

## 7. Risks & Assumptions

- **RISK-001**: Win32 taskbar integration (SetParent) may break on future Windows updates that change Shell_TrayWnd behavior
- **RISK-002**: GetDpiForWindow may not be available on Windows versions older than 10 1607
- **RISK-003**: WPF-UI 4.2.0 may have bugs with NavigationView ScrollViewer that prevent smooth scrolling from working
- **RISK-004**: NVIDIA vibrance API (nvapi64.dll) may fail on systems without NVIDIA GPUs — handled via NullVibranceService
- **ASSUMPTION-001**: User runs Windows 10/11 with x64 architecture
- **ASSUMPTION-002**: User has display API access (not Remote Desktop which may block SetDeviceGammaRamp)
- **ASSUMPTION-003**: Single monitor support only (gamma ramp is applied to primary display)

## 8. Related Specifications / Further Reading

- [FluentFlyout GitHub](https://github.com/unchihugo/FluentFlyout) — UI architecture reference (GPL-3.0)
- [KeyedColors GitHub](https://github.com/qlintenFX/KeyedColors) — Original app with display services
- [WPF-UI Documentation](https://wpfui.lepo.co/) — Fluent 2 WPF controls
- [Win32 SetDeviceGammaRamp](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setdevicegammaramp)
- [Win32 RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
