# DisplayHub

DisplayHub is an open-source Windows display profile manager focused on fast switching and practical daily control over monitor behavior.

## Features

- Per-profile controls for gamma, contrast, vibrance, and color temperature
- Fast profile switching with global hotkeys
- Dynamic Controls mode for adjustment workflows
- Taskbar widget integration
- Profile flyout notifications
- Windows theme + accent integration

## Quality and CI

- GitHub Actions CI runs build + tests on `main` and pull requests
- Baseline tests cover color temperature mapping and profile value clamping

## Installation

### Build from source

```powershell
git clone https://github.com/qlintenFX/DisplayHub.git
cd DisplayHub
dotnet build
dotnet run
```

### Microsoft Store (optional)

DisplayHub may also be offered on the Microsoft Store as an optional convenience/support purchase (easy install/updates and a way to support ongoing work). Building from source remains free.

## Requirements

- Windows 10/11
- .NET SDK 8+
- x64

## Project status

DisplayHub is actively developed. For roadmap and discussions, use GitHub Issues/Discussions.

## Open source and licensing

DisplayHub is licensed under **GPL-3.0-or-later**.

- Main license: `LICENSE`
- Third-party notices: `THIRD_PARTY_NOTICES.md`

## Contributing

- Read `CONTRIBUTING.md`
- Follow `CODE_OF_CONDUCT.md`
- Use issue templates for bugs/features

## Security

Please report vulnerabilities privately first. See `SECURITY.md`.

## Support

If DisplayHub helps you, optional support is available through GitHub Sponsors and (when available) a Microsoft Store purchase. Support funding helps with ongoing maintenance, issue triage, compatibility testing, and release upkeep.

## Monetization model

DisplayHub follows a Files-style open-source-first model:

- Source builds remain free under **GPL-3.0-or-later**.
- The Microsoft Store listing is optional and intended as a convenience/support channel.
- A paid Store listing does not change your GPL rights to build, run, study, modify, or share the source under the license terms.
- No features are locked behind payment unless explicitly stated in release notes and this README.
- Store purchases and sponsorships help fund maintenance and long-term support.
