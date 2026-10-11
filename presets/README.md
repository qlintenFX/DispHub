# Preset gallery

`gallery.json` is the list on DispHub's Presets page. It starts with a few general looks; the rest comes from
people who use DispHub.

## Sharing a preset

1. Make the profile in DispHub and click **Share**.
2. Click **Submit it to the gallery**. A GitHub form opens with your preset already filled in.
3. Add a one-line description and what you use it for, then submit.

You need a GitHub account, nothing else. Once a maintainer approves it (the `preset-approved` label), a pull
request adding it to `gallery.json` is opened automatically and the tests check that the preset works within
Windows' default colour limits.

You can also open a pull request yourself: add an entry with a `name` (40 characters at most), a one-sentence
`description`, a few `tags`, the `link` and your GitHub name as `author`.

## What makes a good preset

- A clear purpose: a kind of content, a time of day, a room, a monitor model.
- A plain name that says what it does. Game names are fine as tags.
- It shouldn't need the extended gamma range or HDR turned off; the tests reject presets Windows would refuse.
