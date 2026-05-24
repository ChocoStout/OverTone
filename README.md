<div align="center">

```
   ___                 _____
  / _ \  __   __ ___  |_   _|  ___   _ __   ___
 | | | | \ \ / // _ \   | |   / _ \ | '_ \ / _ \
 | |_| |  \ V /|  __/   | |  | (_) || | | ||  __/
  \___/    \_/  \___|   |_|   \___/ |_| |_| \___|
```

**Color Palette Extractor for .NET**

[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: AGPL-3.0-or-later](https://img.shields.io/badge/License-AGPL--3.0--or--later-blue.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/OverTone?logo=nuget)](https://www.nuget.org/packages/OverTone)
[![Build](https://img.shields.io/github/actions/workflow/status/ChocoStout/OverTone/build.yml?logo=github)](https://github.com/ChocoStout/OverTone/actions)

Extract beautiful, perceptually distinct color palettes from any image — local file or URL — using eight production-ready quantization algorithms.

[Getting Started](#getting-started) · [Algorithms](#algorithms) · [API Reference](#api-reference) · [Sample App](#sample-app) · [Contributing](#contributing) · [Roadmap](TODO.md)

</div>

---

## Features

| | |
|---|---|
| 🎨 **8 algorithms** | K-Means, Median Cut, Octree, Fuzzy C-Means, Popularity, Wu, NeuQuant, NeuQuant (Iterative) |
| 🌐 **URL + file support** | Pass a local path or any HTTP/HTTPS image URL |
| 🧠 **Perceptual post-processing** | Greedy farthest-point sampling in CIELAB space keeps palettes visually distinct |
| ⚡ **Async-first** | Every extractor is `async Task` from top to bottom |
| 🔌 **Extensible** | Implement `IColorPaletteExtractor` and `PaletteGenerator` discovers it automatically via reflection |
| 📦 **Zero BCL extras** | Only dependency is `StbImageSharp` for image decoding |

---

## Getting Started

### Installation

```bash
dotnet add package OverTone
```

### Quick example

```csharp
using OverTone;

var generator = new PaletteGenerator();

// From a file
var palette = await generator.ExtractColorPaletteAsync("photo.jpg", colorCount: 6);

// From a URL
var palette = await generator.ExtractColorPaletteAsync(
    "https://example.com/image.png",
    colorCount: 8,
    isUrl: true,
    algorithm: PaletteAlgorithm.Wu);

foreach (var color in palette)
    Console.WriteLine($"{color.AsHex}  ({color.PixelCount:N0} px)");
```

### Output

```
#2B4F82  (48,210 px)
#E8A23C  (31,004 px)
#F5F0E8  (22,891 px)
#8C3A2D  (18,773 px)
#4A7A55  (14,002 px)
#1C1C1C  ( 9,350 px)
```

---

## Algorithms

| # | Algorithm | Best for | Notes |
|---|-----------|----------|-------|
| 1 | **K-Means** | Accurate dominant colors | Stride-sampled to ≤ 10 k pixels; deterministic |
| 2 | **Median Cut** | Fast, consistent palettes | Classic Heckbert split on widest channel |
| 3 | **Octree** | Memory-efficient quantization | Tree pruning keeps peak memory bounded |
| 4 | **Fuzzy C-Means** | Soft cluster membership | Slower but smooth color transitions |
| 5 | **Popularity** | Exact most-frequent colors | Histogram-based; instant on any image |
| 6 | **Wu** | Perceptual quality, sharp palettes | Variance-minimising cube split (Xiaolin Wu 1992) |
| 7 | **NeuQuant** | Neural competitive learning | Auto-scales neurons + iterations per color count |
| 8 | **NeuQuant (Iterative)** | Distinct colors, no near-duplicates | Same as NeuQuant with Delta-E dedup pass |

All algorithms feed into a shared **perceptual post-processing** stage:

- **Default path** — 5× candidate pool → greedy farthest-point selection in CIELAB space  
- **Iterative/Dedupe path** — 4× candidate pool → Delta-E (CIE76) near-duplicate removal

---

## API Reference

### `PaletteGenerator`

```csharp
public class PaletteGenerator
{
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(
        string source,
        int    colorCount,
        bool   isUrl            = false,
        PaletteAlgorithm algorithm   = PaletteAlgorithm.KMeans,
        bool   dedupe           = false,
        NeuQuantOptions? neuQuantOptions = null);
}
```

| Parameter | Description |
|-----------|-------------|
| `source` | Local file path **or** HTTP/HTTPS URL |
| `colorCount` | Number of colors to return |
| `isUrl` | Set `true` when `source` is a URL |
| `algorithm` | One of the `PaletteAlgorithm` enum values |
| `dedupe` | `true` → Delta-E dedup mode (NeuQuant iterative) |
| `neuQuantOptions` | Override neuron count / iterations; `null` = auto-scale |

### `ColorPalette`

```csharp
public class ColorPalette
{
    public byte   R          { get; set; }
    public byte   G          { get; set; }
    public byte   B          { get; set; }
    public int    PixelCount { get; set; }
    public string AsHex      { get; }       // "#RRGGBB"
}
```

### `NeuQuantOptions`

```csharp
public record NeuQuantOptions(int NeuronCount, int TrainingIterations)
{
    // Auto-scales: neurons = max(colorCount × 8, 64)
    //              iterations = max(colorCount × 10, 100)
    public static NeuQuantOptions ForColorCount(int colorCount);
}
```

### Extending with a custom extractor

```csharp
public class MyExtractor : IColorPaletteExtractor
{
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.KMeans; // reuse or extend the enum

    public Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        // your implementation
    }
}
```

`PaletteGenerator` discovers all `IColorPaletteExtractor` implementations in the executing assembly automatically — no registration required.

---

## Project Structure

```
OverTone/
├── OverTone/                     # Class library (NuGet package)
│   ├── IColorPaletteExtractor.cs
│   ├── ColorPalette.cs
│   ├── PaletteAlgorithm.cs
│   ├── PaletteGenerator.cs
│   ├── NeuQuantOptions.cs
│   ├── Algorithms/               # One file per extractor + private helpers
│   │   ├── KMeansColorExtractor.cs
│   │   ├── MedianCutColorExtractor.cs
│   │   ├── OctreeColorExtractor.cs
│   │   ├── FuzzyCMeansColorExtractor.cs
│   │   ├── PopularityColorExtractor.cs
│   │   ├── WuColorExtractor.cs
│   │   ├── NeuQuantColorExtractor.cs
│   │   ├── DedupeColorExtractor.cs
│   │   └── ...helpers (Axis, Box, Octree, ColorBox, ClusteringHelpers)
│   └── Processing/               # Post-processing & color math
│       ├── PalettePostProcessing.cs
│       └── ColorMetrics.cs
└── OverTone.Sample/              # Interactive console demo (Windows)
    └── Program.cs
```

---

## Sample App

`OverTone.Sample` is a Windows console app with a full ANSI-colored TUI:

```
  1) Open image file
  2) Load image from URL
  3) Exit
```

After picking a source the app lets you choose algorithm, color count, and (for NeuQuant) override neuron count / training iterations. Results are displayed as colored swatches with a percentage bar:

```
  ██████  #2B4F82  Navy              ████████████████████░░░░░░░░  48.3%
  ██████  #E8A23C  Orange            █████████████░░░░░░░░░░░░░░░  31.1%
  ...
```

Run it:

```bash
cd OverTone.Sample
dotnet run
```

> **Windows only** — uses `OpenFileDialog` (WinForms) for the file picker.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Windows (sample app only — the library targets `net10.0` and is cross-platform)

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-extractor`
3. Add your extractor implementing `IColorPaletteExtractor`
4. Open a pull request

All contributions welcome — new algorithms, bug fixes, performance improvements, docs.

---

## License

[AGPL-3.0-or-later](LICENSE) © ChocoStout
