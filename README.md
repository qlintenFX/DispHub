# KeyedColors

![KeyedColors Logo](logo.png)

A simple Windows application that allows users to create custom display profiles with gamma, contrast, and (experimental) vibrance adjustments. Each profile can be assigned a custom hotkey for quick access.

## Features

- Create and manage multiple display profiles
- Adjust gamma, contrast, and (preview) vibrance settings
- Assign custom hotkeys to profiles for quick access
- System tray integration for easy access
- Dedicated Settings tab for application preferences
- Option to start automatically with Windows
- Configurable behavior when closing the application
- Profiles are saved automatically and persist between sessions
- Comes with three default presets:
   - **Default**: Standard display (Gamma: 1.0, Contrast: 50%, Vibrance: 50%)
   - **Dark**: Reduced brightness for low-light environments (Gamma: 0.8, Contrast: 50%, Vibrance: 50%)
   - **Night Vision**: Enhanced visibility in dark environments (Gamma: 2.8, Contrast: 60%, Vibrance: 65%)

## Usage

1. **Create a Profile**:
   - Adjust the gamma, contrast, and vibrance sliders to your preferred settings
   - Click "Add" to create a new profile
   - Enter a name for the profile

2. **Manage Profiles**:
   - Select a profile from the list to activate it
   - Click "Update" to save changes to an existing profile
   - Click "Del" to delete a profile

3. **Set Hotkeys**:
   - Select a profile
   - Click "Set Hotkey"
   - Press a key combination (e.g., Ctrl+Alt+1)
   - Click OK to save the hotkey

4. **Configure Settings / Dynamic Controls**:
   - Navigate to the Settings tab
   - Toggle "Start with Windows" to launch the app automatically with Windows
   - Toggle "Minimize to tray when closed" to control whether the app minimizes to the system tray or fully closes when clicking the X button
   - Use the Dynamic Controls tab for real-time gamma/contrast/vibrance tweaks driven by hotkeys (Shift+Arrows/PageUp/PageDown)

5. **System Tray**:
   - The application can minimize to the system tray when closed (configurable in Settings)
   - Double-click the tray icon to restore the window
   - Right-click the tray icon for a menu of profiles and options

## Requirements

- Windows 10 or later
- .NET 8.0 Desktop Runtime or later

## Vibrance Preview (Experimental)

The UI now captures a vibrance percentage per profile and within Dynamic Controls. KeyedColors routes those values through a pluggable vibrance service so upcoming builds can integrate NVIDIA/AMD/Intel APIs. Until those vendor-specific services are implemented, changing the vibrance slider only updates profile metadata; it does not yet modify GPU saturation.

## Building from Source

1. Clone the repository
2. Open the solution in Visual Studio 2022 or later
3. Build the solution
4. Run the application

## License

This software is proprietary. All rights reserved.

Copyright © 2025-2026 qlintenFX

## Acknowledgments

- This application uses Windows API for display settings manipulation