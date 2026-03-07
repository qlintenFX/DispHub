## [3.0.0] - 2026-03-08

### Changed
- Complete rewrite from WinForms to WPF with Fluent 2 design (WPF-UI 4.2)
- Renamed: KeyedColors -> DisplayHub
- System theme detection, Mica backdrop, WPF-UI FluentWindow dialogs
- Navigation using NavigationView content area (no margin hack)

### Added
- Dynamic Controls value persistence across restarts
- Human-readable hotkey labels (Ctrl+A instead of Modifier+VK65)
- Explicit Apply button on Profiles page
- Profile list template with hotkey badge
- About page with version, credits, and GitHub links

### Fixed
- Orphaned OS hotkeys when Dynamic Controls toggled
- Profile auto-applied on page load

### Removed
- [Serializable] from Profile (BinaryFormatter remnant)
- Unused CommunityToolkit.Mvvm package

---

# Changelog

All notable changes to KeyedColors will be documented in this file.

## [2.0.0] - 2026-02-06

### Changed
- Major project restructuring: organized source into `Services/`, `Models/`, `UI/`, `Constants/`, `NVIDIA/` directories
- Renamed `Form1` to `MainForm` for clarity
- Extracted NVIDIA types and API bindings into dedicated `NVIDIA/` module
- Introduced `Logger`, `SettingsManager`, `AppConstants` for cleaner separation of concerns
- Added `ProfileNameDialog` as standalone dialog

### Added
- Full xUnit test suite (`KeyedColors.Tests/`) with Moq and FluentAssertions
- CI pipeline via GitHub Actions (build, test, publish verification)
- NVIDIA vibrance service implementation (`NvidiaVibranceService`)
- Vibrance slider in UI and Dynamic Controls (Shift+PageUp/PageDown)
- Extensible vibrance service abstraction for future vendor APIs

### Fixed
- Fixed CultureNotFoundException when switching keyboard input languages
- Changed InvariantGlobalization setting to support different cultures

## [1.3.1] - 2025-04-20

### Fixed
- Fixed CultureNotFoundException that occurred when switching keyboard input languages
- Changed InvariantGlobalization setting from true to false to support different cultures

## [1.3.0] - 4-19-2025

### Added
- Added Dynamic Controls feature for real-time adjustments with hotkeys:
  - Shift+Up/Down arrows for gamma adjustment
  - Shift+Left/Right arrows for contrast adjustment
  - Toggle to enable/disable dynamic controls
  - Option to save current dynamic settings as a new profile

### Fixed
- Fixed bug where profile hotkeys would still work while Dynamic Controls are enabled
- Improved handling of hotkey conflicts between Dynamic Controls and profile hotkeys

## [1.2.0] - 4-19-2025

### Added
- Added a dedicated Settings tab for application preferences
- Added "Start with Windows" option to automatically launch the app at system startup
- Added "Minimize to tray when closed" option to control app behavior when closing
- Settings are saved to Registry and persist between application restarts

## [1.1.0] - 4-18-2025

### Added
- Added PowerShell build script (`build-exe.ps1`) for developers to easily create self-contained Windows executable
- Added project logo and Ko-fi support button to README.md
- Added donation section with Ko-fi integration in README.md
- Added PayPal donation option to README.md

### Bug Fixes
- Fixed UI issue where the text input field in the "Add Profile" dialog was partially cut off, making the first characters difficult to read 
