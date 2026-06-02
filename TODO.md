# OverTone — TODO

Tracked ideas, improvements, and known gaps. Items are grouped by area and loosely ordered by priority within each group.

---

## 🎨 Algorithms

- [x] **K-Means — smarter seeding**  
  Done — `KMeansColorExtractor` now uses [k-means++](https://en.wikipedia.org/wiki/K-means%2B%2B) seeding from a fixed seed (a fresh `Random` per extraction), so results are deterministic *and* small chromatically-distinct regions reliably seed their own cluster instead of being lost to a dominant background.

- [ ] **K-Means — convergence threshold**  
  Currently runs for a fixed `_maxIterations`. Add an early-exit when the total centroid shift between iterations falls below a configurable epsilon (e.g. `< 1.0` in RGB space) to avoid wasted iterations on converged runs.

- [x] **Fuzzy C-Means — subsampling**  
  Done — the shared `ColorPaletteExtractorBase.ExtractVisiblePixels` stride-samples visible pixels to ≤ 10k, so Fuzzy C-Means (and Median Cut) now stay bounded on large images, matching K-Means.

- [ ] **NeuQuant — learning-rate decay**  
  The current implementation uses a flat learning rate throughout training. Implement an exponential or linear decay schedule so early iterations explore broadly and later ones fine-tune, matching the behaviour of the original NeuQuant paper.

- [ ] **NeuQuant — neighbourhood function**  
  Add a Gaussian neighbourhood function so neurons near the winning neuron also move towards the input, not just the winner. This reduces isolated neurons and produces smoother palettes.

- [ ] **Wu — alpha channel support**  
  `WuColorExtractor` builds its histogram over RGB only. Extend `Box` and the moment arrays to handle RGBA so transparent pixels are excluded from the variance calculation rather than silently contributing to it.

- [ ] **Wu / Octree — 64-bit accumulators for huge images**  
  `WuColorExtractor`'s moment tables (and `Volume`) and `OctreeNode`'s per-node channel sums are `int[]`/`int`. Cumulative channel sums reach `pixelCount × 255`, which overflows above ~8 megapixels and corrupts results. Widen them to `long`. (Pixel subsampling for Median Cut / FCM is already handled by the base class; Octree and Wu are histogram-based, so only the accumulator width is at risk — not memory.)

- [ ] **Large images — optional max-pixel guard & pluggable decoder**  
  Add an opt-in maximum-pixel limit that rejects absurdly large images before decode (decompression-bomb protection). Separately, an `IImageDecoder` seam would let callers plug in a RAW (CR2/NEF/ARW/DNG) or other decoder, since `StbImageSharp` cannot read camera RAW. See README → *Large images, RAW & memory* for current behaviour and guidance.

- [ ] **New algorithm — DBSCAN**  
  Density-based clustering naturally handles irregularly shaped color clusters and doesn't require specifying `k` in advance. Add `PaletteAlgorithm.Dbscan` and a corresponding extractor.

- [ ] **New algorithm — CIEDE2000 K-Means**  
  Variant of K-Means where distances are computed in CIELAB using the CIEDE2000 metric rather than Euclidean RGB, producing more perceptually uniform clusters.

- [ ] **New algorithm — OkLab K-Means**  
  Cluster in the OkLab perceptual color space instead of RGB. OkLab is a modern, closed-form space that's cheaper than CIELAB + CIEDE2000 (no iterative ΔE) and often more uniform, especially in blues. Builds directly on the existing K-Means + base-class infrastructure — likely the best bang-for-buck perceptual upgrade.

- [ ] **New algorithm — MMCQ (Modified Median Cut Quantization)**  
  The "Color Thief" algorithm and the de-facto standard for web "palette from image" extraction: a median-cut variant that prioritises boxes by volume × population. Familiar to anyone coming from Color Thief / Vibrant.js and slots into the existing box-splitting code.

- [ ] **New algorithm — Mean Shift**  
  Mode-seeking clustering that needs no `k` (just a bandwidth) and naturally surfaces dominant color "modes". A good parameter-light companion to `Dominant` mode; pair with the existing subsampling to keep it fast.

- [ ] **New algorithm — K-Medoids (PAM)**  
  Like K-Means but cluster centers are *actual image pixels* rather than averages — guarantees palette colors that truly appear in the image (useful for LED output and "real swatch" use cases).

---

## 🔬 Post-processing

- [x] **Configurable diversity threshold**  
  Done — `PaletteGenerator.ExtractColorPaletteAsync` now exposes `minDeltaE` and `candidatePoolMultiplier`, plus a `PaletteSelectionMode` (`Diverse`/`Dominant`) to choose between farthest-point sampling and frequency-based selection.

- [ ] **Weighted diversity sampling**  
  `SelectDiverse` treats all candidates equally. Weight the farthest-point selection by `PixelCount` so colors that represent more pixels are preferred when two candidates are equidistant in Lab space.

- [ ] **CIEDE2000 in post-processing**  
  `RemoveNearDuplicateByDeltaE` currently uses CIE76 (simple Euclidean Lab distance). Upgrade to CIEDE2000 for more perceptually accurate deduplication, especially in the blue region where CIE76 is known to be inaccurate.

- [ ] **Vibrant / Muted swatch extraction (Android Palette-style)**  
  A selection layer (not a quantizer) on top of any extractor that picks *semantic* swatches — Vibrant, Muted, Dark Vibrant, Light Vibrant, Dark Muted — by scoring candidates on HSL targets weighted by population. This is exactly how music apps theme their UI from album art, making it the highest-value addition for the music-player use case. Fits as a new `PaletteSelectionMode` (e.g. `Vibrant`) or a small dedicated swatch API.

---

## 📦 Library API

- [ ] **`IColorPaletteExtractor` — cancellation support**  
  Add `CancellationToken` to `ExtractColorPaletteAsync` so long-running extractors (FCM, K-Means) can be cancelled by the caller without having to wait for them to finish.

- [ ] **`ColorPalette` — add `A` (alpha) channel**  
  Useful for images with transparency. Extractors already read the alpha channel to filter invisible pixels; surfacing it in the model costs nothing.

- [ ] **`ColorPalette` — `ToArgb()` / `ToHsl()` helpers**  
  Convenience conversions that consumer apps commonly need. Keeps the conversion logic centralised rather than every caller reimplementing it.

- [ ] **`PaletteGenerator` — `IAsyncEnumerable<ColorPalette>` overload**  
  Stream palette entries one by one as they are discovered. Useful for large palettes or progressive UI rendering.

- [ ] **`PaletteAlgorithm` — remove `Dedupe`**  
  `Dedupe` is a post-processing decorator, not a standalone extraction algorithm. It should not appear in the public enum. The iterative NeuQuant path already covers the use-case via the `dedupe` flag.

---

## 🖥️ Sample app

- [x] **Export palette to file**  
  Done — export is now a first-class library feature (`PaletteExporter` / `IPaletteExporter`), not just a sample-app convenience. Ships JSON, hex list, C/Arduino array, CSS, SCSS, and Tailwind; the sample app offers an export menu after displaying results. Design-tool formats (GIMP/Adobe `.gpl`, `.ase`, SVG) and mobile (Android `colors.xml`, iOS) are not yet implemented — add one by dropping in a new `IPaletteExporter`.

- [ ] **Side-by-side algorithm comparison**  
  Add a mode that runs all (or selected) algorithms on the same image and prints the results in a table so the user can compare quality at a glance.

- [ ] **Drag-and-drop support**  
  Accept a file path as a command-line argument (`OverTone.Sample path/to/image.png`) so the app can be used as a shell drop target or integrated into build pipelines.

- [ ] **Cross-platform file picker**  
  `ShowFileDialog()` uses `OpenFileDialog` (WinForms), limiting the sample to Windows. Detect the OS and fall back to a console path prompt on macOS/Linux.

- [ ] **Progress reporting for slow algorithms**  
  The spinner shows activity but not progress. For K-Means and FCM, emit iteration progress (e.g. `iteration 12/20`) so the user knows the algorithm is advancing rather than stuck.

---

## 🧪 Tests

- [ ] **Unit tests for each extractor**  
  The `OverTone.Tests` project now covers the exporters plus algorithm-level behaviour (accent recovery on a synthetic "1989-like" image, K-Means determinism, and the mean-ΔE quality metric). Per-extractor coverage is still thin: each should have at least a smoke test on a known 2×2 pixel image, a test that `colorCount` is respected, and a test that transparent pixels are excluded.

- [x] **Regression tests for Wu and K-Means fixes**  
  Done — `OverTone.Tests/AlgorithmRobustnessTests` covers the Wu fixes (correct 3-D cumulative moments + double-precision split scoring — Wu now recovers the actual colors and no longer throws when more colors are requested than are present), the Octree byte-overflow fix, Fuzzy C-Means determinism, and the no-duplicate selection guard, all on small deterministic synthetic images.

- [ ] **Benchmark project**  
  *Quality* benchmarking now exists: `PaletteQuality.MeanDeltaE` scores how well a palette represents an image, and `SyntheticImage` generates ground-truth test images with no external assets. Still TODO: a `BenchmarkDotNet` project measuring extraction *time* and memory for each algorithm on a standard 1080p image, as a baseline before performance work.

---

## 🔧 Infrastructure

- [ ] **GitHub Actions CI**  
  Add a `.github/workflows/build.yml` that restores, builds, and runs tests on `ubuntu-latest` and `windows-latest` for every push and PR.

- [ ] **NuGet publish workflow**  
  Add a `release.yml` workflow that packs `OverTone.csproj` and publishes to NuGet.org when a `v*` tag is pushed. The `PackageLicenseExpression`, `PackageReadmeFile`, and `PackageTags` are already wired up in the csproj.

- [ ] **`CHANGELOG.md`**  
  Start a changelog following [Keep a Changelog](https://keepachangelog.com/) conventions so release notes are easy to produce.

- [ ] **`LICENSE` file**  
  Add a `LICENSE` file containing the MIT licence text. The `PackageLicenseExpression = MIT` in the csproj references it but the file does not yet exist in the repository.
