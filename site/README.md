# Website

The DispHub website: plain HTML, CSS and JavaScript with no build step, styled after Fluent 2 (Windows 11) on the
brand's navy, blue and orange. `.github/workflows/pages.yml` publishes this folder to GitHub Pages on every push to
`main` that changes it.

Preview it locally:

```
python -m http.server 8765 --directory site
```

- `index.html`: home, with a working quick panel. Its photos (`assets/img/reef-*.webp`) are the app's preview photo
  with the looks DispHub's own colour ramps produce, so the demo shows what the app really does.
- `download/`: the Microsoft Store first; building from source sits under "More download options".
- `privacy/`: the privacy policy from `PRIVACY.md`, plus a note about the website itself. Keep the two in step.
- `assets/img`: web versions of the Store art captures, the icon and the social card (the teaser's end card).
- `assets/media/teaser.mp4`: the teaser at 720p.

Photos: U.S. National Park Service, public domain (see `THIRD_PARTY_NOTICES.md`). Teaser music: "GAMECUBE" by
yung brownie (prod. brownie.), free for profit.
