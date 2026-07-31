# Contributing to OverTone

Thanks for your interest in improving OverTone! Contributions of all kinds are welcome —
new algorithms, exporters, bug fixes, performance work, and documentation.

## Development setup

OverTone targets **.NET 8.0** and **.NET 10.0**. Install the
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned via `global.json`); the
.NET 8 runtime is needed to run the `net8.0` test target locally.

```bash
dotnet restore
dotnet build -c Release
dotnet test  -c Release
```

Everything — both libraries, the console sample, the Blazor web app, and the tests — is
cross-platform and builds on Windows, macOS, and Linux.

## Project layout

- `OverTone/` — the core class library (published as the **OverTone** package).
- `OverTone.Extensions.DependencyInjection/` — DI glue (published as **OverTone.Extensions.DependencyInjection**).
- `OverTone.Sample/` — interactive console demo.
- `OverTone.Web/` — Blazor web demo + theme builder.
- `OverTone.Tests/` — xUnit test suite (runs on net8.0 and net10.0).

## Guidelines

- Public APIs are documented (`GenerateDocumentationFile` is on); keep new public members documented.
- New extractors implement `IColorPaletteExtractor`; new exporters implement `IPaletteExporter`.
- Add tests for new behavior.
- NuGet versions are centralized in `Directory.Packages.props` (Central Package Management) —
  add or change versions there, not in individual `.csproj` files.
- Versions follow [Semantic Versioning](https://semver.org/), derived automatically from commit
  messages — **don't** hand-edit `CHANGELOG.md` or `VersionPrefix`; release-please owns both. See
  [Commit messages](#commit-messages) and [Releasing](#releasing-maintainers).

## Commit messages

OverTone uses [Conventional Commits](https://www.conventionalcommits.org/). The release automation
([release-please](https://github.com/googleapis/release-please)) reads them to pick the next version
and to write `CHANGELOG.md`, so the prefix matters:

| Prefix | Example | Effect |
|---|---|---|
| `feat:` | `feat: add Tailwind v4 exporter` | minor bump |
| `fix:` | `fix: clamp K-Means seeds to image bounds` | patch bump |
| `feat!:` / `fix!:`, or a `BREAKING CHANGE:` footer | `feat!: drop the net6.0 target` | major bump |
| `docs:` `test:` `refactor:` `perf:` `build:` `ci:` `chore:` | `chore: tidy usings` | no release |

Only `feat`, `fix`, and breaking changes cut a release; other types just record history. A commit whose
subject doesn't start with one of these types is ignored by the changelog.

PRs are **squash-merged**, so the **pull-request title becomes the commit on `main`** and must itself be
a valid Conventional Commit. The `pr-title-lint` workflow enforces this on every PR.

## Releasing (maintainers)

Releases are automated with [release-please](https://github.com/googleapis/release-please) — there are
no manual tags or version bumps:

1. Merging Conventional Commits to `main` makes release-please open (and keep updating) a **release PR**
   titled `chore(main): release X.Y.Z` that bumps `VersionPrefix` in `Directory.Build.props` and
   updates `CHANGELOG.md`.
2. **Merge that release PR** when you're ready to ship. release-please creates the GitHub Release and the
   `vX.Y.Z` tag, which runs the `publish` job: it builds, tests, packs both packages, and pushes them to
   NuGet.org and GitHub Packages (attaching the `.nupkg`/`.snupkg` to the release).

The NuGet.org push requires a `NUGET_API_KEY` repository secret (GitHub Packages uses the built-in
token). Configuration lives in `release-please-config.json` and `.release-please-manifest.json`.

## License

By contributing you agree that your contributions are licensed under the project's
[MIT](LICENSE) license.
