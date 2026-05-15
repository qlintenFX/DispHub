# DispHub

DispHub is an open-source Windows app for saving and switching display profiles. It helps you quickly adjust monitor settings for work, gaming, and daily use.

## Features

- Save and manage multiple display profiles
- Adjust gamma, contrast, vibrance, and color temperature
- Switch profiles quickly with global hotkeys
- Use Dynamic Controls for fast tuning workflows
- Get profile switch notifications

## Install and build

### Build from source

```powershell
git clone https://github.com/qlintenFX/DispHub.git
cd DispHub
dotnet build DispHub.sln
dotnet run --project DispHub.csproj
```

### Microsoft Store (optional)

If available, the Microsoft Store listing is an optional convenience purchase. Building from source remains free.

## Requirements

- Windows 10/11
- .NET 10 SDK (LTS; `global.json` pins a stable 10.0 feature band)
- x64 system

## Contributing

Contributions are welcome.

- Read `CONTRIBUTING.md`
- Follow `CODE_OF_CONDUCT.md`
- Use Issues for bugs/features and Discussions for usage questions

## Support

- Usage help: GitHub Discussions
- Bugs and feature requests: GitHub Issues
- Security reports: follow `SECURITY.md`
- Privacy: see `PRIVACY.md`
- Optional support: GitHub Sponsors or a Store purchase

## License

Licensed under **GPL-3.0-or-later**.

- `LICENSE`
- `THIRD_PARTY_NOTICES.md`

## Privacy

DispHub does not include custom telemetry or account tracking, and stores settings/profiles locally.

- `PRIVACY.md`
