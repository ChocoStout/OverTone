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

[Getting Started](#getting-started) · [Algorithms](#algorithms) · [Tuning](#selection-modes--tuning) · [API Reference](#api-reference) · [Exports](#exports) · [Sample App](#sample-app) · [Contributing](#contributing) · [Roadmap](TODO.md)

</div>

---

## Features

| | |
|---|---|
| 🎨 **8 algorithms** | K-Means, Median Cut, Octree, Fuzzy C-Means, Popularity, Wu, NeuQuant, NeuQuant (Iterative) |
| 🌐 **URL + file support** | Pass a local path or any HTTP/HTTPS image URL |
| 🧠 **Selectable post-processing** | `Diverse` (farthest-point CIELAB) or `Dominant` modes — tune which colors surface, and score results with mean ΔE |
| ⚡ **Async-first** | Every extractor is `async Task` from top to bottom |
| 🔌 **Extensible** | Implement `IColorPaletteExtractor` and `PaletteGenerator` discovers it automatically via reflection |
| 📦 **Zero BCL extras** | Only dependency is `StbImageSharp` for image decoding |
| 📤 **6 export formats** | JSON, hex list, C/Arduino array, CSS, SCSS, Tailwind — add your own via `IPaletteExporter` |
| 🛡️ **Validated input** | Image magic bytes are checked before decoding — mislabeled or hostile files are rejected, not parsed |
| 🧵 **Optional parallelism** | `maxDegreeOfParallelism` parallelizes K-Means with bit-identical results |

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
| 1 | **K-Means** | Accurate dominant colors | k-means++ seeding, stride-sampled to ≤ 10 k pixels; fully deterministic |
| 2 | **Median Cut** | Fast, consistent palettes | Classic Heckbert split on widest channel |
| 3 | **Octree** | Memory-efficient quantization | Tree pruning keeps peak memory bounded |
| 4 | **Fuzzy C-Means** | Soft cluster membership | Slower but smooth color transitions |
| 5 | **Popularity** | Exact most-frequent colors | Histogram-based; instant on any image |
| 6 | **Wu** | Perceptual quality, sharp palettes | Variance-minimising cube split (Xiaolin Wu 1992) |
| 7 | **NeuQuant** | Neural competitive learning | Auto-scales neurons + iterations per color count |
| 8 | **NeuQuant (Iterative)** | Distinct colors, no near-duplicates | Same as NeuQuant with Delta-E dedup pass |

All algorithms feed into a shared **perceptual post-processing** stage, selected via `PaletteSelectionMode`:

- **`Diverse`** (default) — 5× candidate pool → greedy farthest-point selection in CIELAB space; spreads picks across the chromatic range so accent colors surface
- **`Dominant`** — 4× candidate pool → Delta-E (CIE76) near-duplicate removal; keeps the most frequent colors in roughly their proportions

See [Selection modes & tuning](#selection-modes--tuning) for when to use each and how to measure quality.

---

## Selection modes & tuning

Every algorithm produces a large candidate pool that's narrowed down to your requested `colorCount`. **How** it narrows is the biggest lever on whether the result matches what you expect:

| Mode | What you get | Best for |
|------|--------------|----------|
| **`Diverse`** (default) | Colors spread across the image's chromatic range (farthest-point in CIELAB), seeded with the dominant color | "Designer" palettes that surface accent colors, even small ones |
| **`Dominant`** | The most frequent colors in roughly their proportions, near-duplicates merged | The literal main colors of a photo |

```csharp
// Surface the accent colors (default)
var vivid = await generator.ExtractColorPaletteAsync("cover.jpg", 5,
    selection: PaletteSelectionMode.Diverse);

// Just the most common colors
var main = await generator.ExtractColorPaletteAsync("cover.jpg", 5,
    selection: PaletteSelectionMode.Dominant);
```

> K-Means uses **k-means++** seeding with a fixed seed: results are deterministic, and small chromatically-distinct regions (logos, accents) get a fair chance to form their own cluster instead of being swallowed by a dominant background.

### Measuring quality

Tuning is easier when it's measurable. `PaletteQuality.MeanDeltaE` returns the mean CIE76 ΔE between every pixel and its nearest palette color — **lower means the palette represents the image better** — so you can compare algorithms and settings objectively instead of eyeballing swatches:

```csharp
using OverTone.Processing;

var palette = await generator.ExtractColorPaletteAsync("cover.jpg", 6);
var error   = PaletteQuality.MeanDeltaE(File.ReadAllBytes("cover.jpg"), palette);
Console.WriteLine($"mean ΔE = {error:F2}");
```

### Test images

Lena is retired; for reproducible tuning prefer:

- **Ground truth (best for accuracy):** the [X-Rite/Macbeth ColorChecker](https://en.wikipedia.org/wiki/ColorChecker) (24 patches with published reference values), or **synthetic images built from a known palette** — see `SyntheticImage` in the test project, which needs no external assets and is fully reproducible.
- **Real photos (perceptual):** the [Kodak True Color Image Suite](https://r0k.us/graphics/kodak/) (the common Lena replacement), or freely-licensed sets like [Unsplash](https://unsplash.com/data) and [DIV2K](https://data.vision.ee.ethz.ch/cvl/DIV2K/). Deliberately include hard cases: gradients, skin tones, foliage, and a dominant neutral with a small saturated accent — the classic "where did my accent color go?" case.

### Large images, RAW & memory

OverTone keeps runtime and memory bounded on big inputs — here's how it behaves and what to watch for:

- **Pixel sampling.** K-Means, Median Cut, and Fuzzy C-Means stride-sample visible pixels down to ≤ 10k before clustering, so their runtime is independent of resolution. Octree, Popularity, and Wu are histogram-based — their memory is fixed by the histogram size no matter how many pixels you feed them.
- **Decode memory.** The decoder (`StbImageSharp`) expands the whole image into an RGBA buffer (a 60-megapixel photo ≈ 240 MB). On memory-constrained targets, **downscale before** handing the image to OverTone.
- **Accumulator width (planned).** Wu's moment tables and Octree's per-node channel sums are currently `int`; their cumulative sums (`pixelCount × 255`) overflow above ~8 megapixels. These are being widened to `long` so very large images stay correct — K-Means and Median Cut already use 64-bit sums. ([Roadmap](TODO.md).)
- **RAW camera files are not supported.** The decoder reads PNG, JPEG, GIF, BMP, PSD, HDR, and PNM — **not** camera RAW (CR2/NEF/ARW/DNG). The magic-byte validator rejects RAW (and any non-image) before decoding, so **convert RAW to PNG/TIFF/JPEG first**. A pluggable `IImageDecoder` seam for RAW and other formats is a possible future addition.
- **Decompression-bomb guard (planned).** An optional maximum-pixel limit can reject absurdly large images before decode.

---

## API Reference

### `PaletteGenerator`

```csharp
public class PaletteGenerator
{
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(
        string source,
        int    colorCount,
        bool   isUrl                              = false,
        PaletteAlgorithm     algorithm            = PaletteAlgorithm.KMeans,
        PaletteSelectionMode selection            = PaletteSelectionMode.Diverse,
        NeuQuantOptions?     neuQuantOptions       = null,
        int?                 candidatePoolMultiplier = null,
        double               minDeltaE            = 12.0,
        int                  maxDegreeOfParallelism = 1);

    // Same options, but extracts from an in-memory image you already hold
    // (album art, a decoded frame, an upload) — avoids a redundant read.
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(
        byte[] imageData, int colorCount, /* …same optional parameters… */ );
}
```

| Parameter | Description |
|-----------|-------------|
| `source` | Local file path **or** HTTP/HTTPS URL |
| `colorCount` | Number of colors to return |
| `isUrl` | Set `true` when `source` is a URL |
| `algorithm` | One of the `PaletteAlgorithm` enum values |
| `selection` | `Diverse` (default) or `Dominant` — see [Selection modes & tuning](#selection-modes--tuning) |
| `neuQuantOptions` | Override neuron count / iterations; `null` = auto-scale |
| `candidatePoolMultiplier` | Candidates per color before narrowing; `null` = per-mode default (5× / 4×) |
| `minDeltaE` | Minimum CIE76 ΔE between colors kept by `Dominant` mode |
| `maxDegreeOfParallelism` | `1` (default) = sequential; `> 1` parallelizes K-Means. Identical palettes, just faster |

> **Input is validated before decoding.** Both overloads check the image's magic bytes (PNG, JPEG, GIF, BMP, PSD, HDR, PNM) up front and throw `UnsupportedImageFormatException` for anything else — a renamed script, a truncated upload, or an HTML error page from a URL never reaches the decoder. Use `ImageValidation.IsSupportedImage(bytes)` to check without throwing.

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

## Exports

Turn any extracted palette into a ready-to-use file. The library ships six formats out of the box and discovers them the same way it discovers algorithms — implement `IPaletteExporter` to add your own.

```csharp
using OverTone;

var generator = new PaletteGenerator();
var exporter  = new PaletteExporter();

var palette = await generator.ExtractColorPaletteAsync("photo.jpg", colorCount: 6);

// Serialize to a string…
string css = exporter.Export(palette, PaletteExportFormat.Css);

// …or write straight to disk.
await exporter.ExportToFileAsync(palette, PaletteExportFormat.Json, "palette.json");
```

| Format | `PaletteExportFormat` | Ext | Great for |
|--------|-----------------------|-----|-----------|
| **JSON** | `Json` | `.json` | Music players, LED apps, anything programmatic — hex, RGB, HSL, a color name, and (optional) pixel metadata |
| **Hex list** | `HexList` | `.txt` | A plain `#RRGGBB` per line; round-trips anywhere |
| **C / Arduino array** | `CArray` | `.h` | LED strips — a `uint8_t[][3]` array + length macro, FastLED-friendly |
| **CSS** | `Css` | `.css` | `:root` custom properties for the web |
| **SCSS** | `Scss` | `.scss` | Sass variables + a `$palette` list |
| **Tailwind** | `Tailwind` | `.js` | A `theme.extend.colors` snippet for `tailwind.config.js` |

### `PaletteExporter`

```csharp
public class PaletteExporter
{
    public IReadOnlyCollection<PaletteExportFormat> AvailableFormats { get; }

    public string GetFileExtension(PaletteExportFormat format);

    public string Export(
        IReadOnlyList<ColorPalette> palette,
        PaletteExportFormat         format,
        PaletteExportOptions?       options = null);

    public Task ExportToFileAsync(
        IReadOnlyList<ColorPalette> palette,
        PaletteExportFormat         format,
        string                      path,
        PaletteExportOptions?       options = null,
        CancellationToken           cancellationToken = default);
}
```

### `PaletteExportOptions`

```csharp
public record PaletteExportOptions
{
    public string PaletteName     { get; init; } = "OverTone Palette"; // JSON name, header comments, C identifier
    public string Prefix          { get; init; } = "color";           // --color-1 / $color-1 / 'color-1'
    public bool   IncludeMetadata { get; init; } = true;              // pixel counts + percentages in JSON
}
```

### Example output

`PaletteExportFormat.Json`:

```json
{
  "name": "OverTone Palette",
  "colorCount": 2,
  "totalPixels": 150,
  "colors": [
    { "hex": "#2B4F82", "rgb": { "r": 43, "g": 79, "b": 130 },
      "hsl": { "h": 215, "s": 50, "l": 34 }, "name": "Charcoal",
      "pixelCount": 100, "percentage": 66.67 }
  ]
}
```

`PaletteExportFormat.CArray` — drop straight into an Arduino/FastLED sketch:

```c
// OverTone Palette — 2 colors
#define OVERTONE_PALETTE_LEN 2
const uint8_t OVERTONE_PALETTE[OVERTONE_PALETTE_LEN][3] = {
    {  43,  79, 130 }, // #2B4F82
    { 232, 162,  60 }, // #E8A23C
};
```

### Adding a custom format

```csharp
public class GplPaletteExporter : IPaletteExporter
{
    public PaletteExportFormat Format => PaletteExportFormat.Json; // reuse or extend the enum
    public string FileExtension => "gpl";

    public string Export(IReadOnlyList<ColorPalette> palette, PaletteExportOptions options)
    {
        // your implementation
    }
}
```

`PaletteExporter` discovers all `IPaletteExporter` implementations in the executing assembly automatically — no registration required.

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
│   ├── PaletteSelectionMode.cs   # Diverse vs Dominant narrowing
│   ├── IPaletteExporter.cs       # Export contract (discovered via reflection)
│   ├── PaletteExportFormat.cs
│   ├── PaletteExportOptions.cs
│   ├── PaletteExporter.cs        # Export facade + reflection registry
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
│   ├── Export/                   # One file per format exporter
│   │   ├── JsonPaletteExporter.cs
│   │   ├── HexListPaletteExporter.cs
│   │   ├── CArrayPaletteExporter.cs
│   │   ├── CssPaletteExporter.cs
│   │   ├── ScssPaletteExporter.cs
│   │   ├── TailwindPaletteExporter.cs
│   │   └── ExportFormatting.cs   # Shared HSL / percentage / naming helpers
│   └── Processing/               # Post-processing, color math & quality metrics
│       ├── PalettePostProcessing.cs
│       ├── ColorMetrics.cs       # RGB distance, RGB→Lab, ΔE
│       └── PaletteQuality.cs     # Mean ΔE quantization error
├── OverTone.Sample/              # Interactive console demo (Windows)
│   └── Program.cs
└── OverTone.Tests/               # xUnit tests
    ├── PaletteExporterTests.cs
    ├── AlgorithmQualityTests.cs  # accent recovery, determinism, ΔE
    └── SyntheticImage.cs         # dependency-free BMP generator
```

---

## Sample App

`OverTone.Sample` is a **cross-platform** console app (no WinForms dependency) with an ANSI-colored TUI:

```
  1) Open image file
  2) Load image from URL
  3) Use the built-in test card (12 known colors)
  q) Exit
```

Pick a source, then in the per-image session you can **extract** a palette (choosing algorithm, color count, and Diverse/Dominant mode), **compare** every algorithm ranked by mean ΔE (with timing), or open another image. Extraction shows swatches with hex / RGB / HSL / name and a share bar:

```
  ██████  #2B4F82  rgb( 43, 79,130)  hsl(215, 50%, 34%)  Dark Slate Blue  ████████░░  33.4%
  ██████  #E8A23C  rgb(232,162, 60)  hsl( 38, 80%, 57%)  Orange           ████░░░░░░  17.1%
  ...
```

After results, the app offers to **export** the palette to any format (or `A` for all six).

### Command-line use

Pass a source to skip the menu — handy for scripting and gathering data:

```bash
# Extract interactively from a file or URL
dotnet run --project OverTone.Sample -- path/to/image.png
dotnet run --project OverTone.Sample -- https://example.com/cover.jpg

# Run EVERY algorithm and dump the full comparison (palettes + mean ΔE + timing) to JSON
dotnet run --project OverTone.Sample -- cover.jpg --json results.json --colors 8

# Use the built-in known-palette test card (no file needed) — ideal for comparable data
dotnet run --project OverTone.Sample -- testcard --json testcard.results.json

# Save the test card image to disk for use elsewhere
dotnet run --project OverTone.Sample -- --make-testcard testcard.bmp
```

> Runs anywhere .NET 10 runs (Windows, macOS, Linux). On a plain terminal without truecolor ANSI it falls back to a simpler colored view.

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
