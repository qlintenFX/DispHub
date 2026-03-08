---
goal: DisplayHub UI Enhancement Round 3 - Scrolling, Flyout, Power Toggle, Visual Improvements
version: 1.0
date_created: 2025-06-20
last_updated: 2025-06-20
owner: qlintenFX
status: 'In progress'
tags: [feature, ui, enhancement]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20progress-yellow)

Comprehensive UI/UX enhancement for DisplayHub including smooth scrolling, profile switch flyout, power button toggle, improved DC keybinds layout, and page preview placeholders.

## 1. Requirements & Constraints

- **REQ-001**: All pages must support mouse wheel scrolling with smooth animation
- **REQ-002**: Power button must toggle display on/off with visual color indicator
- **REQ-003**: Profile switch flyout must appear at bottom of screen when switching profiles
- **REQ-004**: DC keybinds must use paired 2-column layout grouped by attribute
- **REQ-005**: All scrollable pages must have preview image placeholders at top
- **REQ-006**: Tray menu must show active profile with bold text and status header
- **CON-001**: Must maintain WPF-UI 4.2 compatibility
- **CON-002**: Must not break existing hotkey architecture
- **GUD-001**: Follow FluentFlyout UI patterns for consistency
- **PAT-001**: Use attached behaviors for reusable scroll functionality

## 2. Implementation Steps

### Phase 1: Smooth Scrolling

- GOAL-001: Fix mouse wheel scrolling and add smooth scroll animation to all pages

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Create `Helpers/SmoothScrollBehavior.cs` attached behavior with VerticalOffset animation | ✅ | 2025-06-20 |
| TASK-002 | Add `helpers:SmoothScrollBehavior.IsEnabled="True"` to ScrollViewers on all 5 pages | ✅ | 2025-06-20 |
| TASK-003 | Wrap HomePage and SettingsPage in ScrollViewer (they were missing one) | ✅ | 2025-06-20 |

### Phase 2: Power Button Toggle

- GOAL-002: Convert reset-display button to on/off toggle with visual state indicator

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-004 | Add `IsDisplayActive`, `DisplayPowerChanged` event, `ToggleDisplayPower()` to MainWindow | ✅ | 2025-06-20 |
| TASK-005 | Update SettingsWindow power button to toggle on/off with color (OrangeRed when off) | ✅ | 2025-06-20 |
| TASK-006 | Add power status to HomePage dashboard | ✅ | 2025-06-20 |

### Phase 3: Profile Switch Flyout

- GOAL-003: Show animated flyout at bottom of screen when switching profiles

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-007 | Create `Windows/ProfileFlyoutWindow.xaml/.cs` with slide-up animation | ✅ | 2025-06-20 |
| TASK-008 | Integrate flyout trigger in MainWindow for hotkey and tray profile switches | ✅ | 2025-06-20 |
| TASK-009 | Add accent bar and auto-hide after 1.8s with CancellationToken | ✅ | 2025-06-20 |

### Phase 4: DC Keybinds Visual Improvement

- GOAL-004: Redesign dynamic controls keybinds to paired 2-column layout

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | Group keybinds by Gamma/Contrast/Vibrance with section labels | ✅ | 2025-06-20 |
| TASK-011 | Use 2-column Grid with increase/decrease side by side per attribute | ✅ | 2025-06-20 |

### Phase 5: Page Preview Placeholders

- GOAL-005: Add FluentFlyout-style preview image placeholders to pages

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-012 | Add preview Border with icon + description to ProfilesPage | ✅ | 2025-06-20 |
| TASK-013 | Add preview Border with icon + description to DynamicControlsPage | ✅ | 2025-06-20 |
| TASK-014 | Add preview Border with icon + description to SettingsPage | ✅ | 2025-06-20 |

### Phase 6: Tray Menu Improvements

- GOAL-006: Improve active profile visibility in system tray context menu

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-015 | Add "Active: {name}" header item at top of profile list | ✅ | 2025-06-20 |
| TASK-016 | Bold active profile name in profile list | ✅ | 2025-06-20 |

### Phase 7: Build Verification

- GOAL-007: Verify all changes compile and app launches cleanly

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-017 | Build with 0 errors, 0 warnings | ✅ | 2025-06-20 |
| TASK-018 | App launches and runs without crashes | ✅ | 2025-06-20 |

## 3. Alternatives

- **ALT-001**: Custom ScrollViewer subclass instead of attached behavior — rejected for reusability
- **ALT-002**: Separate power off/on buttons — rejected for cleaner UX with single toggle
- **ALT-003**: Toast notification instead of flyout — rejected to match FluentFlyout style

## 4. Dependencies

- **DEP-001**: WPF-UI 4.2 (SymbolIcon, CardControl, CardAction)
- **DEP-002**: .NET 8 WPF (DoubleAnimation, attached DependencyProperty)

## 5. Files

- **FILE-001**: `Helpers/SmoothScrollBehavior.cs` — New: attached behavior for smooth scrolling
- **FILE-002**: `Windows/ProfileFlyoutWindow.xaml` — New: profile switch flyout XAML
- **FILE-003**: `Windows/ProfileFlyoutWindow.xaml.cs` — New: flyout code-behind with animation
- **FILE-004**: `MainWindow.xaml.cs` — Modified: power toggle, flyout, tray improvements
- **FILE-005**: `SettingsWindow.xaml` — Modified: power toggle button naming
- **FILE-006**: `SettingsWindow.xaml.cs` — Modified: power toggle logic and visual
- **FILE-007**: `Pages/HomePage.xaml` — Modified: scroll, status section
- **FILE-008**: `Pages/HomePage.xaml.cs` — Modified: power/profile status updates
- **FILE-009**: `Pages/ProfilesPage.xaml` — Modified: scroll, preview placeholder
- **FILE-010**: `Pages/DynamicControlsPage.xaml` — Modified: scroll, preview, 2-column keybinds
- **FILE-011**: `Pages/SettingsPage.xaml` — Modified: scroll, preview placeholder
- **FILE-012**: `Pages/AboutPage.xaml` — Modified: scroll

## 6. Testing

- **TEST-001**: Build succeeds with 0 errors
- **TEST-002**: App launches and shows HomePage without crashes
- **TEST-003**: Mouse wheel scrolling works on all pages
- **TEST-004**: Power button toggles display and changes color
- **TEST-005**: Profile switch via hotkey/tray shows flyout animation

## 7. Risks & Assumptions

- **RISK-001**: `SystemFillColorSuccessBrush` may not exist in all theme configs — mitigated with TryFindResource fallback
- **ASSUMPTION-001**: WPF DoubleAnimation works on attached DependencyProperty (verified via WPF docs)
- **ASSUMPTION-002**: FluentWindow supports AllowsTransparency=True on child windows (used for flyout)
