# Changelog

All notable changes are listed here. The format follows [Keep a Changelog](https://keepachangelog.com) and
versions follow [Semantic Versioning](https://semver.org).

## [1.0.0] - Unreleased

First release of DispHub.

### Added

- Profiles with brightness, contrast, gamma, colour temperature and vibrance, applied to every connected display.
- Global hotkeys per profile, plus on or off, next and previous profile, and one for the quick panel.
- Tray menu and an on-screen message when the profile changes.
- Per-app profiles, and schedules like Windows' Night light (sunset to sunrise, set hours or custom) with fades (Pro).
- A quick panel from the notification area for profiles, brightness, warmth and screen effects.
- Vibrance on NVIDIA displays and on Intel graphics, which covers the built-in screen of most laptops.
- Original colours are journalled before the first change and restored on pause, exit, sign-out or after a crash.
- Share profiles as disphub:// links and start new profiles from templates.
- Screen effects drawn on the GPU: 27 looks (film, camera, retro computers and CRTs, colour, comfort and colour
  vision help), a before and after preview, every setting adjustable, saved looks, look links and a hotkey.
  Profiles can pick a look, so schedules and per-app rules can switch effects too. Showing effects on screen
  needs Pro; everyone can try them in the preview. Hidden until turned on in Settings.
- Option to lift Windows' gamma limit for very warm night profiles.
- Start with Windows, light and dark theme, accent colours, Mica, smooth scrolling.
