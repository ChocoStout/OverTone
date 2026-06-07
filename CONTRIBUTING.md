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
- Versions follow [Semantic Versioning](https://semver.org/). Update `CHANGELOG.md` and bump
  `VersionPrefix` in `Directory.Build.props`.

## Releasing (maintainers)

Pushing a `v*` tag (for example `v1.1.0`) triggers `.github/workflows/release.yml`, which packs both
packages with the tag's version and publishes them to NuGet.org and GitHub Packages. The NuGet.org
push requires a `NUGET_API_KEY` repository secret.

## License

By contributing you agree that your contributions are licensed under the project's
[MIT](LICENSE) license.
