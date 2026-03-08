---
goal: DisplayHub Round 4 - Scrolling fix, Power toggle fix, Tray left-click, Taskbar widget
version: 1.0
date_created: 2026-03-08
last_updated: 2026-03-08
owner: qlintenFX
status: 'In progress'
tags: [feature, bugfix, ui, enhancement]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20progress-yellow)

Fix critical scrolling issues, refine power toggle behavior, add FluentFlyout-style left-click tray options, and plan profile switch popups + taskbar widget.

## 1. Requirements & Constraints

- **REQ-001**: Mouse wheel scrolling must work anywhere content is scrollable (like FluentFlyout)
- **REQ-002**: Power button OFF must reset to default; ON must restore last selected profile
- **REQ-003**: Power button icon must be visually centered and uniform within TitleBar
- **REQ-004**: Left-click tray behavior must be configurable (Open Settings / Do Nothing) like FluentFlyout's ComboBox pattern
- **REQ-005**: Remove status section from HomePage
- **REQ-006**: Profile switch popup (flyout) must appear when switching profiles via hotkey
- **REQ-007**: Taskbar widget must show current active profile, docked above system taskbar
- **CON-001**: FluentFlyout pages do NOT use ScrollViewer wrappers — NavigationView provides its own internal ScrollViewer. Our pages must follow the same pattern.
- **CON-002**: Must maintain WPF-UI 4.2 + .NET 8 compatibility
- **GUD-001**: Follow FluentFlyout code patterns exactly for proven behavior
- **PAT-001**: FluentFlyout uses `FindScrollableScrollViewer()` to locate the NavigationView's internal ScrollViewer for scroll-reset — reuse this pattern for smooth scrolling hookup

## 2. Implementation Steps

### Phase 1: Fix Scrolling (Critical Bug)

- GOAL-001: Make mouse wheel scrolling work on all pages by removing nested ScrollViewer conflict

**Root cause**: Our pages wrap content in `<ScrollViewer>` but the WPF-UI NavigationView already provides an internal ScrollViewer for its content Frame. This creates nested ScrollViewers that fight over mouse wheel events. FluentFlyout's pages use bare `<StackPanel>` at root — no ScrollViewer wrapper at all.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Remove `<ScrollViewer>` wrapper from `Pages/ProfilesPage.xaml` — use bare `<StackPanel>` as root like FluentFlyout | | |
| TASK-002 | Remove `<ScrollViewer>` wrapper from `Pages/DynamicControlsPage.xaml` — use bare `<StackPanel>` as root | | |
| TASK-003 | Remove `<ScrollViewer>` wrapper from `Pages/SettingsPage.xaml` — use bare `<StackPanel>` as root | | |
| TASK-004 | Remove `<ScrollViewer>` wrapper from `Pages/AboutPage.xaml` — use bare `<StackPanel>` as root | | |
| TASK-005 | Remove `<ScrollViewer>` wrapper from `Pages/HomePage.xaml` — use bare `<StackPanel>` as root | | |
| TASK-006 | Remove `helpers:SmoothScrollBehavior.IsEnabled` namespace/attribute from all 5 pages (no longer needed on page ScrollViewers) | | |
| TASK-007 | In `SettingsWindow.xaml.cs`, add `FindScrollableScrollViewer()` helper (copy FluentFlyout pattern) to locate NavigationView's internal ScrollViewer | | |
| TASK-008 | In `SettingsWindow.xaml.cs`, add `ResetScrollPosition()` on navigation (copy FluentFlyout pattern) | | |
| TASK-009 | In `SettingsWindow.xaml.cs`, attach `SmoothScrollBehavior` to the NavigationView's internal ScrollViewer programmatically after first navigation | | |
| TASK-010 | Verify mouse wheel scrolling works on all 5 pages | | |

### Phase 2: Fix Power Button Toggle

- GOAL-002: Power toggle must properly restore last selected profile and icon must be visually correct

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-011 | Fix `MainWindow.ToggleDisplayPower()` — on power-on, restore `_activeProfileIndex` profile values (currently broken: `_staticInstance` may be null or index stale) | | |
| TASK-012 | Track `_lastActiveProfileIndex` separately so power-on always restores correct profile even if `_activeProfileIndex` was reset | | |
| TASK-013 | Fix power button XAML in `SettingsWindow.xaml` — replace `ui:Button` with properly sized/padded button inside TitleBar.Header so icon is centered. Use `Width="36" Height="36" Padding="0"` with `HorizontalContentAlignment="Center" VerticalContentAlignment="Center"` | | |
| TASK-014 | Test: power off resets display → power on restores last selected profile → sliders in ProfilesPage reflect correct values | | |

### Phase 3: Remove HomePage Status Section

- GOAL-003: Clean up HomePage by removing status section per user request

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Remove "Status" TextBlock, status Card (power + active profile), and associated bindings from `Pages/HomePage.xaml` | | |
| TASK-016 | Remove `PowerStatusText`, `ActiveProfileText`, `UpdatePowerStatus()`, `OnPowerChanged`, `OnActiveProfileChanged` event subscriptions from `Pages/HomePage.xaml.cs` | | |
| TASK-017 | Remove unused `System.Windows.Media` using if no longer needed | | |

### Phase 4: Left-Click Tray Behavior (FluentFlyout Pattern)

- GOAL-004: Add configurable left-click tray behavior with setting in Settings page

FluentFlyout pattern: `NIconLeftClick` int setting (0=Open Settings, 1=Show Media Flyout) with ComboBox on SystemPage. For DisplayHub: 0=Open Settings (default), 1=Do Nothing.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-018 | Add `TrayLeftClickBehavior` int property to `SettingsManager` (default=0, persisted in settings.json). 0=Open Settings, 1=Do Nothing | | |
| TASK-019 | Update `MainWindow.TrayIcon_LeftClick` to check `SettingsManager.TrayLeftClickBehavior` before opening settings (match FluentFlyout's `nIcon_LeftClick` pattern) | | |
| TASK-020 | Add "Tray Icon" section to `Pages/SettingsPage.xaml` with CardControl containing ComboBox for left-click behavior (matching FluentFlyout's SystemPage layout) | | |
| TASK-021 | Add `TrayLeftClickComboBox_SelectionChanged` handler in `Pages/SettingsPage.xaml.cs` | | |

### Phase 5: Profile Switch Popup (Flyout Refinement)

- GOAL-005: Ensure profile switch flyout works correctly and matches FluentFlyout's LockWindow pattern

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-022 | Test `Windows/ProfileFlyoutWindow` — verify slide-up animation triggers on hotkey and tray profile switch | | |
| TASK-023 | Ensure flyout shows correct profile name and auto-hides after 1.8s | | |
| TASK-024 | Add power-toggle flyout: show "Display Off" / "Display On" text when power button is toggled (reuse same ProfileFlyoutWindow with different text) | | |
| TASK-025 | Handle multi-monitor: position flyout on primary monitor's work area bottom center | | |

### Phase 6: Taskbar Widget

- GOAL-006: Create a small taskbar-docked widget showing the current active profile

FluentFlyout's `TaskbarWidgetControl` is a borderless 40x100 UserControl docked above the taskbar, showing song info with blur background. For DisplayHub: simplified version showing current profile name.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-026 | Create `Windows/TaskbarWidgetWindow.xaml/.cs` — borderless Window, Topmost, ShowInTaskbar=False, positioned above system taskbar, rounded corners, semi-transparent background | | |
| TASK-027 | Widget content: profile name TextBlock + power status dot indicator | | |
| TASK-028 | Widget positioning: dock bottom-right of screen, above taskbar (calculate from `SystemParameters.WorkArea`) | | |
| TASK-029 | Create `Pages/TaskbarWidgetPage.xaml/.cs` — settings page for widget (Enable/Disable toggle, position options) | | |
| TASK-030 | Add `TaskbarWidgetEnabled` bool property to `SettingsManager` (default=false, persisted) | | |
| TASK-031 | Add TaskbarWidgetPage to NavigationView in `SettingsWindow.xaml` (after Dynamic Controls, icon `TextboxAlignBottom24` matching FluentFlyout) | | |
| TASK-032 | Wire widget updates: subscribe to `ActiveProfileChanged`, `DisplayPowerChanged`, `DynamicControlsModeChanged` events in MainWindow to update widget content | | |
| TASK-033 | Add hover effect on widget (opacity/color transition) and click to open SettingsWindow | | |
| TASK-034 | Handle widget show/hide based on `TaskbarWidgetEnabled` setting | | |

### Phase 7: Build & Smoke Test

- GOAL-007: Verify all changes compile and app runs without crashes

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-035 | Build with 0 errors, 0 warnings | | |
| TASK-036 | Verify scrolling works on all pages via mouse wheel | | |
| TASK-037 | Verify power toggle restores profile correctly | | |
| TASK-038 | Verify tray left-click and right-click both work | | |
| TASK-039 | Verify profile flyout appears on profile switch | | |

## 3. Alternatives

- **ALT-001**: Keep ScrollViewer wrappers and use `PreviewMouseWheel` event tunneling to force scroll — rejected because it fights the framework; FluentFlyout's bare-StackPanel approach is proven and simpler
- **ALT-002**: Use Popup instead of Window for profile flyout — rejected because Popup has z-order and positioning quirks across monitors
- **ALT-003**: Use Windows notification toast instead of custom flyout — rejected to match FluentFlyout's visual style
- **ALT-004**: Embed taskbar widget as WPF Popup — rejected; separate Window gives better control over positioning and transparency

## 4. Dependencies

- **DEP-001**: WPF-UI 4.2 (NavigationView internal ScrollViewer, NotifyIcon, SymbolIcon)
- **DEP-002**: .NET 8 WPF (SystemParameters.WorkArea, DoubleAnimation)
- **DEP-003**: `Helpers/SmoothScrollBehavior.cs` — repurposed: attached to NavigationView's internal ScrollViewer instead of page-level ScrollViewers

## 5. Files

**Modified:**
- **FILE-001**: `Pages/HomePage.xaml` — Remove ScrollViewer wrapper + status section
- **FILE-002**: `Pages/HomePage.xaml.cs` — Remove status event subscriptions and handlers
- **FILE-003**: `Pages/ProfilesPage.xaml` — Remove ScrollViewer wrapper
- **FILE-004**: `Pages/DynamicControlsPage.xaml` — Remove ScrollViewer wrapper
- **FILE-005**: `Pages/SettingsPage.xaml` — Remove ScrollViewer wrapper, add tray left-click setting
- **FILE-006**: `Pages/SettingsPage.xaml.cs` — Add tray left-click handler
- **FILE-007**: `Pages/AboutPage.xaml` — Remove ScrollViewer wrapper
- **FILE-008**: `SettingsWindow.xaml` — Fix power button sizing, add TaskbarWidget nav item
- **FILE-009**: `SettingsWindow.xaml.cs` — Add FindScrollableScrollViewer, ResetScrollPosition, attach smooth scroll to NavigationView's internal ScrollViewer
- **FILE-010**: `MainWindow.xaml.cs` — Fix ToggleDisplayPower profile restore, configurable tray left-click, wire taskbar widget events
- **FILE-011**: `Services/Settings/SettingsManager.cs` — Add TrayLeftClickBehavior, TaskbarWidgetEnabled

**New:**
- **FILE-012**: `Windows/TaskbarWidgetWindow.xaml` — Borderless taskbar-docked widget
- **FILE-013**: `Windows/TaskbarWidgetWindow.xaml.cs` — Widget logic, positioning, event subscriptions
- **FILE-014**: `Pages/TaskbarWidgetPage.xaml` — Widget settings page
- **FILE-015**: `Pages/TaskbarWidgetPage.xaml.cs` — Widget settings code-behind

## 6. Testing

- **TEST-001**: Mouse wheel scroll works on all 5 pages without grabbing scrollbar
- **TEST-002**: Smooth scroll animation is visible (not jerky)
- **TEST-003**: Power off → display resets to default values
- **TEST-004**: Power on → display restores to last selected profile's gamma/contrast/vibrance
- **TEST-005**: Power button icon is centered within button bounds in TitleBar
- **TEST-006**: Tray left-click opens settings (default behavior)
- **TEST-007**: Tray left-click does nothing when set to option 1
- **TEST-008**: Profile flyout slides up and auto-hides on profile switch
- **TEST-009**: Taskbar widget shows current profile name and updates on switch
- **TEST-010**: Taskbar widget can be enabled/disabled from settings
- **TEST-011**: Build succeeds with 0 errors

## 7. Risks & Assumptions

- **RISK-001**: Removing ScrollViewer from pages may cause layout issues if page content exceeds frame area — mitigated by NavigationView's internal ScrollViewer handling overflow
- **RISK-002**: `FindScrollableScrollViewer()` may not find the ScrollViewer if NavigationView hasn't rendered yet — mitigated by calling after first navigation with `DispatcherPriority.Loaded`
- **RISK-003**: Taskbar widget positioning may be wrong on multi-monitor setups — mitigated by using `SystemParameters.WorkArea` (respects primary monitor taskbar)
- **ASSUMPTION-001**: NavigationView in WPF-UI 4.2 provides an internal ScrollViewer for its content Frame (confirmed by FluentFlyout's `FindScrollableScrollViewer` usage)
- **ASSUMPTION-002**: `SmoothScrollBehavior` attached property works when applied programmatically to discovered ScrollViewer (standard WPF attached property behavior)

## 8. Related Specifications / Further Reading

- FluentFlyout SettingsWindow.xaml.cs: `FindScrollableScrollViewer()`, `ResetScrollPosition()` — scroll handling reference
- FluentFlyout SystemPage.xaml: `NIconLeftClick` ComboBox — tray left-click configuration reference
- FluentFlyout MainWindow.xaml.cs: `nIcon_LeftClick()` — tray behavior dispatch reference
- FluentFlyout TaskbarWidgetControl — taskbar widget positioning and rendering reference
