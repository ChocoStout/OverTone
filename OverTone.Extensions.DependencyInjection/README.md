# OverTone.Extensions.DependencyInjection

[![NuGet](https://img.shields.io/nuget/v/OverTone.Extensions.DependencyInjection?logo=nuget)](https://www.nuget.org/packages/OverTone.Extensions.DependencyInjection)

Microsoft.Extensions.DependencyInjection integration for [**OverTone**](https://www.nuget.org/packages/OverTone), the region-aware color palette extractor.

A single `AddOverTone()` call registers every palette extractor and exporter, plus the
`PaletteGenerator` and `PaletteExporter` facades, as singletons. The core **OverTone** package
stays dependency-free; this optional package adds the DI glue.

## Install

```bash
dotnet add package OverTone.Extensions.DependencyInjection
```

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using OverTone;

var services = new ServiceCollection();
services.AddOverTone();

await using var provider = services.BuildServiceProvider();

var generator = provider.GetRequiredService<PaletteGenerator>();
var palette   = await generator.ExtractColorPaletteAsync("photo.jpg", colorCount: 6);
```

### Per-algorithm tuning

Register options before `AddOverTone()` and the matching extractor picks them up:

```csharp
services.AddSingleton(new SlicOptions { /* ... */ });
services.AddOverTone();
```

See the [main OverTone README](https://github.com/ChocoStout/OverTone#readme) for algorithms,
selection modes, exporters, and theming.

## License

[MIT](https://github.com/ChocoStout/OverTone/blob/main/LICENSE)
