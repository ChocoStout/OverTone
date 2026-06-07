<div align="center">

```
   ___                 _____
  / _ \  __   __ ___  |_   _|  ___   _ __   ___
 | | | | \ \ / // _ \   | |   / _ \ | '_ \ / _ \
 | |_| |  \ V /|  __/   | |  | (_) || | | ||  __/
  \___/    \_/  \___|   |_|   \___/ |_| |_| \___|
```

**Color Palette Extractor for .NET**

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512bd4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/OverTone?logo=nuget)](https://www.nuget.org/packages/OverTone)
[![Build](https://img.shields.io/github/actions/workflow/status/ChocoStout/OverTone/build.yml?logo=github)](https://github.com/ChocoStout/OverTone/actions)

Extract beautiful, region-aware color palettes from any image — local file or URL — using **image-space segmentation** (SLIC superpixels and spatial 5D K-Means). One call, no tuning: `Palette.GetColorsAsync(image, n)`.

[Getting Started](#getting-started) · [Algorithms](#algorithms) · [Tuning](#selection-modes--tuning) · [API Reference](#api-reference) · [Exports](#exports) · [Sample App](#sample-app) · [Contributing](#contributing) · [Roadmap](TODO.md)

</div>

---

## Features

| | |
|---|---|
| 🧩 **Image-space (region-aware)** | Colors come from *regions* — SLIC superpixels merged by color, or spatial 5D K-Means — so objects drive the palette, not just the global histogram |
| 🎯 **One-call `GetColors`** | `Palette.GetColorsAsync(image, n)` — no algorithm, mode, or threshold to pick; distinct colors with honest coverage |
| 🌈 **Peak, not mean** | Each region contributes its *representative* (peak) color, so vivid accents stay vivid instead of averaging to mud |
| 🧠 **Selectable narrowing** | `Salient` (chroma × area), `Diverse` (farthest-point CIELAB), or `Dominant` |
| 🌐 **URL + file support** | Pass a local path or any HTTP/HTTPS image URL |
| ⚡ **Async-first** | Every extractor is `async Task` from top to bottom |
| 📦 **Dependency-free core** | Only dependency is `StbImageSharp` for decoding; the DI glue lives in a separate package |
| 📤 **6 export formats** | JSON, hex list, C/Arduino array, CSS, SCSS, Tailwind — add your own via `IPaletteExporter` |
| 🛡️ **Validated input** | Image magic bytes are checked before decoding — mislabeled or hostile files are rejected, not parsed |
| 🧵 **Optional parallelism** | `maxDegreeOfParallelism` parallelizes the spatial extractors with bit-identical results |

---

## Getting Started

### Installation

```bash
dotnet add package OverTone
```

### Quick example

```csharp
using OverTone;

// The simplest path: the N main colors of an image. No algorithm or settings to choose.
var colors = await Palette.GetColorsAsync("photo.jpg", colorCount: 6);
foreach (var color in colors)
    Console.WriteLine($"{color.AsHex}  ({color.PixelCount:N0} px)");

// Need control? Use PaletteGenerator directly and pick the algorithm + selection mode.
var generator = new PaletteGenerator();
var palette = await generator.ExtractColorPaletteAsync(
    "https://example.com/cover.png",
    colorCount: 8,
    isUrl: true,
    algorithm: PaletteAlgorithm.Slic,
    selection: PaletteSelectionMode.Salient);
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

OverTone works in **image space**: pixel position participates in clustering, so a palette reflects the image's *regions* (a sweatshirt, the sky, the lips) rather than just its global color histogram.

| Algorithm | How it works | Best for |
|-----------|--------------|----------|
| **SLIC** (`Slic`, default) | SLIC superpixels in the joint color+space domain, merged into regions by color similarity; each region contributes its representative (peak) color, weighted by area | The recommended general-purpose choice — region-aware and accent-preserving |
| **Spatial K-Means** (`SpatialKMeans`) | 5D K-Means on `(L, a, b, x, y)` with a `SpatialWeight` dial (`0` = classic color-space K-Means) | A simpler spatial clusterer; `SpatialWeight = 0` reproduces legacy color clustering |

Both emit a **representative (peak) color per region — never the desaturated mean** — then feed a shared selection stage (`PaletteSelectionMode`):

- **`Salient`** — ranks by *saliency* (chroma × area) so a small vivid region survives against a large dull one, with a neutral floor so a dominant black/white still shows. The default for `GetColors`.
- **`Diverse`** (default for `ExtractColorPaletteAsync`) — greedy farthest-point selection in CIELAB; spreads picks across the chromatic range.
- **`Dominant`** — the most frequent colors in roughly their proportions, near-duplicates merged.

> **Honest expectations.** A region's color is its *actual* peak color — vivid when the region is vivid, muted when it's muted. Extraction reports what's in the pixels; it can't manufacture saturation (a guaranteed-vivid synthesized theme is the future *harmonize* path — see the [roadmap](TODO.md)). Segmentation is heavier than a histogram count, so large images are box-downscaled first; results are deterministic.

See [Selection modes & tuning](#selection-modes--tuning) for when to use each and how to measure quality.

---

## Selection modes & tuning

Every algorithm produces a large candidate pool that's narrowed down to your requested `colorCount`. **How** it narrows is the biggest lever on whether the result matches what you expect:

| Mode | What you get | Best for |
|------|--------------|----------|
| **`Salient`** | Colors ranked by chroma × area — a small vivid accent can outrank a large dull region, and a dominant neutral still shows | "The colors a person would name"; the `GetColors` default |
| **`Diverse`** (default) | Colors spread across the image's chromatic range (farthest-point in CIELAB) | Varied "designer" palettes |
| **`Dominant`** | The most frequent colors in roughly their proportions, near-duplicates merged | The literal main colors of a photo |

```csharp
// "The colors a person would name" — small vivid accents survive
var vivid = await generator.ExtractColorPaletteAsync("cover.jpg", 5,
    selection: PaletteSelectionMode.Salient);

// Just the most common colors
var main = await generator.ExtractColorPaletteAsync("cover.jpg", 5,
    selection: PaletteSelectionMode.Dominant);
```

> Results are **deterministic**: SLIC uses regular-grid seeds and a fixed iteration count; spatial K-Means uses k-means++ with a fixed seed; and both pair a parallel assignment step with a sequential (order-independent) center update — so the palette is identical regardless of `maxDegreeOfParallelism`.

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

- **Working resolution.** The spatial extractors box-downscale the image to ≈180k pixels (area-averaged, so small accents aren't aliased away) before segmenting, so runtime is bounded regardless of input size. Tune via `SlicOptions.MaxPixels` / `SpatialKMeansOptions.MaxPixels`.
- **Decode memory.** The decoder (`StbImageSharp`) expands the whole image into an RGBA buffer (a 60-megapixel photo ≈ 240 MB). On memory-constrained targets, **downscale before** handing the image to OverTone.
- **RAW camera files are not supported.** The decoder reads PNG, JPEG, GIF, BMP, PSD, HDR, and PNM — **not** camera RAW (CR2/NEF/ARW/DNG). The magic-byte validator rejects RAW (and any non-image) before decoding, so **convert RAW to PNG/TIFF/JPEG first**. A pluggable `IImageDecoder` seam for RAW is a possible future addition.
- **Decompression-bomb guard (planned).** An optional maximum-pixel limit can reject absurdly large images before decode.

---

## API Reference

### `PaletteGenerator`

```csharp
public class PaletteGenerator
{
    // No-config entry point: the N main colors, region-aware and accent-preserving.
    public Task<List<ColorPalette>> GetColorsAsync(
        byte[] imageData, int colorCount = 6, int maxDegreeOfParallelism = 1);
    public Task<List<ColorPalette>> GetColorsAsync(
        string source, int colorCount = 6, bool isUrl = false, int maxDegreeOfParallelism = 1);

    // Full control: choose the algorithm and selection mode.
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(
        string source,
        int    colorCount,
        bool   isUrl                              = false,
        PaletteAlgorithm     algorithm            = PaletteAlgorithm.Slic,
        PaletteSelectionMode selection            = PaletteSelectionMode.Diverse,
        int?                 candidatePoolMultiplier = null,
        double               minDeltaE            = 12.0,
        int                  maxDegreeOfParallelism = 1);

    // Same options, from in-memory bytes you already hold.
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(
        byte[] imageData, int colorCount, /* …same optional parameters… */ );
}

// One-liner sugar over a shared default generator:
List<ColorPalette> colors = await OverTone.Palette.GetColorsAsync("photo.jpg", 6);
```

| Parameter | Description |
|-----------|-------------|
| `source` | Local file path **or** HTTP/HTTPS URL |
| `colorCount` | Number of colors to return |
| `isUrl` | Set `true` when `source` is a URL |
| `algorithm` | `Slic` (default) or `SpatialKMeans` |
| `selection` | `Salient`, `Diverse` (default), or `Dominant` — see [Selection modes & tuning](#selection-modes--tuning) |
| `candidatePoolMultiplier` | Candidates per color before narrowing; `null` = per-mode default |
| `minDeltaE` | Minimum CIE76 ΔE between colors kept by `Dominant` / `Salient` |
| `maxDegreeOfParallelism` | `1` (default) = sequential; `> 1` parallelizes the spatial extractors. Identical palettes, just faster |

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

### Tuning options (optional)

```csharp
public record SlicOptions(int SuperpixelCount = 256, double Compactness = 10.0,
    int Iterations = 10, double RegionMergeDeltaE = 8.0, int MaxPixels = 180_000);

public record SpatialKMeansOptions(int Seed = 1989, int MaxIterations = 20,
    double SpatialWeight = 0.5, int MaxPixels = 180_000);
```

Pass these to the extractor constructors directly, or register one before `AddOverTone()` and the DI container wires it into the matching extractor.

### Extending with a custom extractor

```csharp
public class MyExtractor : IColorPaletteExtractor
{
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.Slic; // reuse or extend the enum

    public Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        // your implementation
    }
}
```

Provide your extractors explicitly — `new PaletteGenerator(new IColorPaletteExtractor[] { new MyExtractor(), … })` — or register them with `AddOverTone()` from the `OverTone.Extensions.DependencyInjection` package.

---

## Exports

Turn any extracted palette into a ready-to-use file. The library ships six formats out of the box — implement `IPaletteExporter` and hand it to `PaletteExporter` (or register it via `AddOverTone()`) to add your own.

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

`PaletteExporter` uses the exporters you give it (or the full set registered by `AddOverTone()`); the built-in six are wired up by default.

---

## Project Structure

```
OverTone/
├── OverTone/                     # Class library (NuGet package)
│   ├── IColorPaletteExtractor.cs
│   ├── ColorPalette.cs
│   ├── DecodedImage.cs           # RGBA buffer + width/height
│   ├── PaletteAlgorithm.cs       # Slic, SpatialKMeans
│   ├── PaletteGenerator.cs       # ExtractColorPaletteAsync + GetColorsAsync
│   ├── Palette.cs                # static GetColors sugar
│   ├── SlicOptions.cs · SpatialKMeansOptions.cs
│   ├── PaletteSelectionMode.cs   # Salient / Diverse / Dominant
│   ├── IPaletteExporter.cs · PaletteExportFormat.cs · PaletteExportOptions.cs
│   ├── PaletteExporter.cs        # Export facade
│   ├── Algorithms/
│   │   ├── ColorPaletteExtractorBase.cs   # decode, downscale, ExtractCore(DecodedImage)
│   │   ├── SlicColorExtractor.cs          # SLIC superpixels (primary)
│   │   ├── SpatialKMeansColorExtractor.cs # 5D (L,a,b,x,y) K-Means
│   │   └── RegionPaletteBuilder.cs        # labels → RAG merge → region palette
│   ├── Export/                   # One file per format exporter + ExportFormatting.cs
│   └── Processing/               # Color math, selection & quality metrics
│       ├── PalettePostProcessing.cs  # Diverse / Salient + ΔE & OkLab de-dup
│       ├── RepresentativeColor.cs    # peak (modal, chroma-biased) region color
│       ├── ColorMetrics.cs           # RGB↔Lab, OkLab, ΔE, chroma
│       └── PaletteQuality.cs         # mean ΔE + coverage assignment
├── OverTone.Extensions.DependencyInjection/   # AddOverTone() (separate package)
├── OverTone.Sample/              # Cross-platform console demo
│   └── Program.cs
├── OverTone.Web/                 # Blazor Server demo (browser version of the sample)
│   └── Components/Pages/Home.razor
└── OverTone.Tests/               # xUnit tests
    ├── PaletteExporterTests.cs
    ├── AlgorithmQualityTests.cs  # accent recovery, determinism, ΔE
    ├── SpatialExtractionTests.cs # region surfacing, accent survival, peak-not-mean
    ├── DependencyInjectionTests.cs · ImageValidationTests.cs
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

Pick a source, then in the per-image session you can **get the main colors** (no config), **extract** a palette (choosing algorithm, color count, and Salient/Diverse/Dominant mode), **compare** the algorithms side by side (mean ΔE and timing shown for context, not as a ranking), or open another image. Results show swatches with hex / RGB / HSL / name and a share bar:

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

### Web demo

`OverTone.Web` is a tiny **Blazor Server** app — a browser version of the sample. Upload an image (or load the built-in test card), choose a method, and see the palette rendered as live HTML swatches (plus a coverage-weighted color strip) right beside the source image for visual comparison. It calls the library directly via `AddOverTone()`, so the results match the CLI exactly.

```bash
dotnet run --project OverTone.Web
# then open the http://localhost:<port> URL it prints
```

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- The library and the sample both target `net10.0` and run cross-platform (Windows, macOS, Linux)

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-extractor`
3. Add your extractor implementing `IColorPaletteExtractor`
4. Open a pull request

All contributions welcome — new algorithms, bug fixes, performance improvements, docs.

---

## License

[MIT](LICENSE) © 2026 ChocoStout
