# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com) and
versions follow [Semantic Versioning](https://semver.org).

## [1.0.7] - 2026-11-25

### Added

- Polish, Turkish, Japanese, Korean and Simplified Chinese.
- Shared profiles are web links that anyone can open: the page shows the profile, opens it in DispHub, and shows
  people who don't have DispHub where to get it. Older disphub:// links still work.

## [1.0.6] - 2026-11-23

### Fixed

- The minimise, maximise and close buttons show their icons again instead of plain grey boxes, a fault since 1.0.4.

## [1.0.5] - 2026-11-21

### Added

- When the Pro trial ends, a single Windows notification says so, for people who use DispHub only from the tray and
  with hotkeys. Selecting it shows what Pro keeps.

### Fixed

- With DispHub off, or a display left alone, other programs such as f.lux, games or calibration tools can change the
  colours again: DispHub no longer puts the original ones back over them.
- When another program keeps resetting the colours, DispHub's pause from fighting it lasts, instead of ending at the
  next window switch.
- A warm profile that Windows refuses after Allow stronger changes is shown in a milder version instead of not at all.
- Reinstalling DispHub doesn't start a new Pro trial, and a clock that was ahead at the first start no longer stops
  DispHub from starting.
- If the Store's licence check fails at sign-in, DispHub tries again, so Pro is recognised; the check can no longer
  crash DispHub.
- DispHub starts again after a Store update.
- Quitting no longer leaves a profile's monitor brightness on.
- Schedules switch on time the night the clocks change, and after the clock or time zone is changed.

### Removed

- The unused preset gallery and, with it, the internet permission in the Store package.

## [1.0.4] - 2026-11-13

### Fixed

- During the Pro trial, the opacity setting changes the quick panel, messages, drop-downs and menus (it was stuck
  at 70%).
- Screen readers name every setting, every slider in the profile editor, the cards on Home and each display, and
  the minimise, maximise and close buttons of DispHub's windows.
- Home no longer shows "Edit schedule" next to the offer to set a schedule up.
- The Apps page only explains which rule wins once there are two rules.
- Percentages are written the same way everywhere, and the message settings have icons like the other settings.
- The walkthrough's shortcut picture shows your own shortcut and profile, so it matches every keyboard and language
  (it always showed Ctrl + Alt + 2, which is a different shortcut on keyboards with AltGr).

## [1.0.3] - 2026-11-03

### Fixed

- Tips show on the About page of the Microsoft Store version.
- Home says when a schedule waits for Pro, instead of offering to set it up again.
- The About page credits the WPF UI fork DispHub uses (unchihugo's, from FluentFlyout).
- Dutch: the Pro button says "Pro kopen".

## [1.0.2] - 2026-11-01

First release of DispHub on the Microsoft Store: free, with a week of Pro to try, and Pro as a one-time purchase.

### Added

- Profiles with brightness, contrast, gamma, colour temperature and vibrance, for every connected display or
  each display on its own (Pro). Shadow boost lifts only the darkest tones, to see into dark areas (Pro).
- Three profiles free, as many as you like with Pro; start from templates or from a link someone shared.
- Global shortcuts for each profile, for on or off, next and previous profile and the quick panel. The starter
  shortcuts avoid keys that type characters with AltGr on your keyboard layouts.
- The quick panel from the notification area: switch profiles and adjust brightness and warmth, on acrylic.
- The tray menu, and a short on-screen message with the profile's name when it changes.
- Per-app profiles: a profile while an app is in front, your normal one when you switch away (one app free,
  every app with Pro).
- Schedules like Windows' Night light: sunset to sunrise, set hours or your own times, with fades (Pro).
- Screen brightness for laptop screens and DDC/CI monitors, kept in step with Windows; profiles can set the
  brightness, refresh rate and HDR, and laptops can use a lower refresh rate on battery (Pro).
- Vibrance through NVIDIA, AMD and Intel graphics drivers, including most laptops' built-in screens.
- Original colours are saved before the first change and put back on pause, exit, sign-out or after a crash.
  When another app keeps resetting the colours, DispHub stops fighting it for a while and says so.
- Starts with Windows, which you can turn off in the walkthrough or in Settings.
- A first-run walkthrough and a guided tour, fourteen preview pictures, light and dark themes, accent colours,
  Mica or acrylic backgrounds, text size and a compact menu.
- English, German, Spanish, French, Italian, Dutch and Brazilian Portuguese.

[1.0.7]: https://github.com/qlintenFX/DispHub/releases/tag/v1.0.7
[1.0.6]: https://github.com/qlintenFX/DispHub/releases/tag/v1.0.6
[1.0.5]: https://github.com/qlintenFX/DispHub/releases/tag/v1.0.5
[1.0.4]: https://github.com/qlintenFX/DispHub/releases/tag/v1.0.4
[1.0.3]: https://github.com/qlintenFX/DispHub/releases/tag/v1.0.3
[1.0.2]: https://github.com/qlintenFX/DispHub/releases/tag/v1.0.2
