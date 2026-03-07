# DisplayHub

<p align="center">
  <img src="logo.png" width="96" alt="DisplayHub logo"/>
</p>

<p align="center">
  <strong>Modern display profile manager for Windows</strong><br/>
  Gamma · Contrast · NVIDIA Vibrance · Global Hotkeys · Dynamic Controls
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-blue" alt="GPL-3.0"/></a>
  <img src="https://img.shields.io/badge/.NET-8.0-purple" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/WPF--UI-4.2-blueviolet" alt="WPF-UI"/>
</p>

---

## Features

- 🎨 **Display Profiles** — Save and switch between gamma, contrast, and NVIDIA vibrance presets
- ⌨️ **Global Hotkeys** — Register per-profile system-wide keyboard shortcuts
- 🎛️ **Dynamic Controls** — Fine-tune display settings in real time with Shift+Arrow keys
- 🌙 **System Theme** — Follows Windows light/dark mode automatically
- 🔔 **System Tray** — Runs in the background, apply profiles from the tray menu

## Requirements

- Windows 10 / 11 (x86 or x64)
- .NET 8 Desktop Runtime
- NVIDIA GPU (for vibrance support; gamma/contrast work on all GPUs)

## Installation

Download the latest release from [Releases](https://github.com/qlintenFX/DisplayHub/releases) and run the executable.

## Building from Source

```bash
git clone https://github.com/qlintenFX/DisplayHub.git
cd DisplayHub
dotnet build DisplayHub.sln
```

Run tests:

```bash
dotnet test DisplayHub.Tests/DisplayHub.Tests.csproj
```

## Usage

1. **Profiles tab** — Adjust the sliders to your desired display settings, enter a name, and click **Add**.
2. **Apply** — Select a profile and click **Apply** (or double-click it).
3. **Hotkeys** — Select a profile and click **Set Hotkey** to assign a global shortcut.
4. **Dynamic Controls** — Enable on the Dynamic tab; use Shift+Arrow keys to fine-tune on the fly.

## Acknowledgements

- **UI design** inspired by [FluentFlyout](https://github.com/unchihugo/FluentFlyout) by **Hugo Li** (GPL-3.0)
- **WPF-UI** Fluent 2 component library by [Leszek Pomianowski](https://github.com/lepoco/wpfui) (MIT)
- Based on the original [KeyedColors](https://github.com/qlintenFX/KeyedColors) WinForms app

## License

DisplayHub is licensed under the [GNU General Public License v3.0](LICENSE).
