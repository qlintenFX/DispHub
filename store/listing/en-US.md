# Microsoft Store listing (English)

Paste these into Partner Center > DispHub > Store listings > English (United States).

Partner Center: app Store ID `9N5183BRLX34` (https://apps.microsoft.com/detail/9N5183BRLX34), package identity in
`packaging/identity.json`; the `pro` add-on is Store ID `9NMJ1WGTW0B8`.
Field limits are noted in brackets.

## Product name

DispHub

## Short description [up to 270 characters shown in search]

Display profiles for Windows. Tune gamma, contrast, brightness, colour temperature and vibrance for every display,
and switch with a hotkey, per game or on a schedule like Night light.

## Description [10,000 characters]

DispHub puts your display's look one hotkey away.

Make a profile for each situation (brighter shadows and stronger colours for competitive games, warm and dim for
late-night reading, neutral for photo work) and switch between them instantly. A small on-screen message confirms
the change without stealing focus from your game.

PROFILES THAT FIT HOW YOU USE YOUR PC
• Brightness, contrast, gamma and colour temperature for every connected display
• Your screen's own brightness, on laptops and most monitors, without reaching for its buttons
• Vibrance (colour saturation) on NVIDIA, Intel and AMD graphics, including the built-in screen of most laptops
• Stream-friendly: OBS, Discord and screenshots capture your normal colours (a capture card shows your profile)
• Global hotkeys for each profile, plus on or off, next and previous
• Quick switching from the notification area

TEMPLATES AND SHARING
• Start from ready-made profiles for games, films and night reading
• Share your own profile as a link; anyone with DispHub can add it in one click

AUTOMATION (DISPHUB PRO, FREE FOR THE FIRST WEEK)
• Per-app profiles: your game or app gets its look while it's in front, and your desktop goes back to normal when
  you switch away (one app is included free)
• Schedules like Windows' Night light: from sunset to sunrise or set hours, with a smooth fade
• Different settings for each display
• Profiles that set your monitor's brightness, refresh rate and HDR
• A lower refresh rate on battery for laptop screens
• Unlimited profiles

SAFE AND LIGHT
• Your original colours are saved before anything changes and restored when you turn DispHub off or quit, even after a crash
• Runs quietly in the notification area and uses very little memory
• No account, no ads, no telemetry

Try every Pro feature free for 7 days. After that DispHub stays free with three profiles and one per-app profile;
the one-time Pro upgrade keeps profiles for every app, schedules, per-display settings and unlimited profiles, and
supports development. DispHub is open source: https://github.com/qlintenFX/DispHub

Notes: gamma-based adjustments aren't available while Windows HDR is on. Windows limits how far colours can change
by default; DispHub can lift that limit with your permission for very warm night profiles.

## What's new in this version [1,500 characters]

First release.

## Product features [up to 20, 200 characters each]

1. Profiles for brightness, contrast, gamma, colour temperature and vibrance
2. Global hotkeys to switch profiles, even in games
3. Vibrance on NVIDIA and Intel graphics, including laptop screens
4. Works on every connected display
5. Profile templates and shareable links
6. Per-app profiles that switch automatically (one free, every app with Pro)
7. Schedules with smooth fades to warmer evening colours (Pro)
8. Different settings per display (Pro)
9. Original colours restored on pause, exit or after a crash
10. Light and dark theme, starts with Windows

## Search terms [7 terms]

vibrance, gamma, screen dimmer, night light, color temperature, display profiles, monitor settings

## Screenshots

Use `store/screenshots/*.png` in numbered order (1920 x 1080 in git; Partner Center has 1 and 5 at 3840 x 2160 and
2 to 4, refreshed on 2026-10-31, at 1920 x 1080). Captions:

1. Switch profiles with a keyboard shortcut
2. A profile for every moment
3. Warmer evenings, automatically (Pro)
4. Profiles for every app, perfect for games (Pro)
5. Switch and adjust from the quick panel

## Store art and trailer

- 16:9 Super hero art: `store/art/super-hero-1920x1080.png` (no text; the Store puts the title over its left side)
- 1:1 Box art: `store/art/box-art-2160.png`
- 9:16 Poster art: `store/art/poster-1440x2160.png`
- Trailer: 30.5 s, 1920 x 1080, 30 fps, H.264 with AAC stereo, built from the same UI captures; thumbnail
  `store/art/trailer-thumbnail.png`. The video (96 MB) is uploaded to Partner Center and not kept in git.
- Credits for the trailer and screenshot 4: the game scene is a screenshot of Veloren (https://veloren.net, art under
  GPL-3.0) by Antimundo, released under CC0 on Wikimedia Commons. Music "Calm Ambient Dreamscape" by MondaMusic;
  sound effects "Minimalist Light Whoosh FX" by Chrysalyn, "Soft luxury air whoosh #8" by Sdanezis and "Subtle UI
  Button Click" by Turning_Pages; all from Pixabay under the Pixabay Content License.

## Store logo

`store/icon-1024.png` (1:1 box art, also used for the 300x300 logo).

## Other fields

- Privacy policy URL: https://github.com/qlintenFX/DispHub/blob/main/PRIVACY.md
- Website: https://github.com/qlintenFX/DispHub
- Support contact: https://github.com/qlintenFX/DispHub/issues
- Copyright: © 2026 qlintenFX
- Category: Utilities & tools
- Additional system requirements: Windows 10 version 2004 or later. Vibrance needs an NVIDIA, Intel or AMD (beta) GPU
  driving the display.

## Notes for certification (restricted capability: runFullTrust)

DispHub is a desktop (WPF) app that adjusts display gamma ramps and GPU colour settings, registers global hotkeys and
runs in the notification area. These need the full-trust desktop runtime. DispHub doesn't install drivers or services
and makes no changes outside its own data folder, except, when the user explicitly asks for it, a Windows gamma-range
registry value set through a UAC prompt.

## Pro add-on

- Product type: Durable, no expiry
- Product ID (in-app offer token): `pro` (the app looks for this token)
- Title: DispHub Pro
- Description: Per-game profiles, schedules with smooth fades, different settings per display and unlimited profiles.
  A one-time purchase that also supports development.
- Suggested price: USD 4.99, with Microsoft's automatic regional pricing
