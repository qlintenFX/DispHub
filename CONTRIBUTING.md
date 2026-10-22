# Contributing

Thanks for helping. Bug reports and small, focused pull requests are the most useful contributions.

## Reporting bugs

Open an issue and paste the output of **About > Copy diagnostics**. It lists your displays, GPU support and
DispHub's state, which is usually what we need to reproduce a colour problem.

## Translations and presets

- Translate DispHub on [Weblate](https://hosted.weblate.org/engage/disphub/); no coding needed. See
  [docs/translating.md](docs/translating.md) for the style notes.
- Share a profile with the community from DispHub itself: **Share**, then **Submit it to the gallery**.
  See [presets/README.md](presets/README.md).

## Making changes

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Build and test:

   ```powershell
   dotnet build DispHub.slnx -c Release
   dotnet test --project tests/DispHub.Tests/DispHub.Tests.csproj
   dotnet format DispHub.slnx
   ```

   The build treats warnings as errors and CI checks formatting.
3. If you change the UI, run the app (`dotnet run --project src/DispHub`) and check it in light and dark mode.

Where things live is described in [CLAUDE.md](CLAUDE.md), which doubles as the project guide for AI coding agents.

AI tools are welcome, as long as you design the change yourself and review every line they write. A pull request
you can't explain won't be merged.

## Commits and pull requests

- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org):
  `fix: keep hotkeys registered after renaming a profile`. Imperative mood, 72 characters at most.
- Keep the pull request description short: one to three sentences on what changed and why, then a
  `Testing:` line saying how you checked it. Add a screenshot for visible changes. No headings or long lists.
- One topic per pull request. They are squash-merged.

## Licensing of contributions

DispHub is licensed under GPL-3.0-or-later and is also sold through the Microsoft Store. Before your first pull
request is merged, a bot asks you to agree to the [contributor licence agreement](docs/CLA.md) by leaving a
comment; translators accept it in Weblate. You keep the copyright in your work; the agreement lets DispHub ship
it in the Store too.
