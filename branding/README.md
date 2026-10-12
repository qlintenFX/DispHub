# Branding

The DispHub name and logo identify the official app. They aren't covered by the GPL, so forks need their own
name and logo (see the README's licence section).

- `icon.png` is the master logo: the monitor on a transparent canvas.
- `icon-shadow.png` is its soft shadow on the same canvas; large sizes draw it underneath.

Every icon in the app, the MSIX package, the Store listing and the README wordmark is generated from these:

```
dotnet run build/gen-assets.cs
```

The masters were cut out of the original artwork, which was rendered on a light background, with
`python -I branding/cutout.py <artwork> branding` (needs Pillow).
