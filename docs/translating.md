# Translating DispHub

## On Crowdin (no coding needed)

Translate in your browser at **[crowdin.com/project/disphub](https://crowdin.com/project/disphub)**. Pick your
language, translate or vote on suggestions, and your work reaches DispHub in the next update. Crowdin shows
machine suggestions and Microsoft's terminology; please check them against the style notes below.

Missing your language? Ask for it on the Crowdin project page or in a GitHub issue.

## In the repository

All text lives in `src/DispHub/Localization/Strings.resx` (English). Each language is a copy with the same keys,
named `Strings.<culture>.resx` (for example `Strings.pl.resx`, `Strings.zh-Hans.resx`). Missing keys fall back to
English, and every file present is offered in Settings automatically.

1. Copy `Strings.resx` to `Strings.<culture>.resx` and translate every `<value>`. Keep the `name` attributes unchanged.
2. Keep placeholders such as `{0}` and `{1}`, `\n` line breaks, `•` bullets, and `disphub://preset` exactly as they are.
3. Run `dotnet test --project tests/DispHub.Tests/DispHub.Tests.csproj`; the localisation tests check keys and placeholders.

Crowdin works from the same files (see `crowdin.yml`), so edits in either place end up together.

## Style

DispHub should read like part of Windows. Use the terms Windows itself uses in your language
(see Microsoft's [localization style guides](https://learn.microsoft.com/globalization/reference/microsoft-style-guides)
and the Microsoft Terminology Collection):

| English | Use the Windows term for |
| --- | --- |
| notification area | the taskbar area with tray icons (e.g. de "Infobereich", fr "zone de notification") |
| Night light | Windows' Night light feature (de "Nachtmodus", fr "Éclairage nocturne", es "Luz nocturna") |
| Settings > Apps > Startup | the exact Settings page path in your language |
| Task Manager | Windows' Task Manager name |
| hotkey | keyboard shortcut as Windows calls it |

- Use the informal "you" where Windows does (German "du", Dutch "je", French and Italian follow Windows).
- Keep "DispHub", "DispHub Pro", "Pro", "HDR", "NVIDIA", "Intel", "AMD", "GitHub" and "f.lux" untranslated.
- `Upgrade_*` feature names are inserted into `Upgrade_Body` as `{0}`, so they must read naturally as its subject.
- `About_Note1`, `About_Note2` and `About_NoteSignature` are a personal note from the developer: keep the warm,
  first-person tone.
- `Preview_*` are the names of the preview photos (wildflowers, coral reef, autumn forest, hot spring).
