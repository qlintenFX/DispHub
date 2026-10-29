# Website

The DispHub website: plain HTML, CSS and JavaScript with no build step, styled after Fluent 2 (Windows 11) on the
brand's navy, blue and orange. `.github/workflows/pages.yml` publishes this folder to GitHub Pages on every push to
`main` that changes it.

Preview it locally:

```
python -m http.server 8765 --directory site
```

- `index.html`: home, laid out like FluentFlyout's site: the name, one line, three calls to action (Microsoft Store,
  GitHub, WinGet), a working quick panel, four short feature sections with real app windows, a closing call to action
  and a short FAQ. The demo's photos (`assets/img/reef-*.webp`) are the app's preview photo with the looks DispHub's own
  colour ramps produce.
- `profiles/`: three profiles (Evening, Film, Night vision) before and after, each on three fitting
  pictures (`assets/img/show/`) or one the visitor picks. `site.js` draws the looks with a copy of DispHub's colour
  maths, in the browser, so a visitor's picture never leaves their PC. Picture credits are in the page footer and
  `THIRD_PARTY_NOTICES.md`.
- `download/`: the Microsoft Store first; WinGet and building from source sit under "More download options".
- `privacy/`: the privacy policy from `PRIVACY.md`, plus a note about the website itself. Keep the two in step.
- `assets/img`: the app's windows at 200 % (square; the page rounds them), the icon and the social card.

Keep the copy short: a heading and one sentence per section.

Photos: U.S. National Park Service, public domain (see `THIRD_PARTY_NOTICES.md`).
