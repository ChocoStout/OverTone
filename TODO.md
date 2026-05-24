# OverTone — TODO

Tracked ideas, improvements, and known gaps. Items are grouped by area and loosely ordered by priority within each group.

---

## 🎨 Algorithms

- [ ] **K-Means — smarter seeding**  
  Replace random centroid selection with [K-Means++](https://en.wikipedia.org/wiki/K-means%2B%2B) to reduce the chance of poor initial centroids and speed up convergence. The `_random` field in `KMeansColorExtractor` is already injectable for testability.

- [ ] **K-Means — convergence threshold**  
  Currently runs for a fixed `_maxIterations`. Add an early-exit when the total centroid shift between iterations falls below a configurable epsilon (e.g. `< 1.0` in RGB space) to avoid wasted iterations on converged runs.

- [ ] **Fuzzy C-Means — subsampling**  
  `FuzzyCMeansColorExtractor` runs on the full pixel set and is slow on large images. Apply the same stride-based subsampling used by K-Means (≤ 10 k pixels) to keep runtime bounded.

- [ ] **NeuQuant — learning-rate decay**  
  The current implementation uses a flat learning rate throughout training. Implement an exponential or linear decay schedule so early iterations explore broadly and later ones fine-tune, matching the behaviour of the original NeuQuant paper.

- [ ] **NeuQuant — neighbourhood function**  
  Add a Gaussian neighbourhood function so neurons near the winning neuron also move towards the input, not just the winner. This reduces isolated neurons and produces smoother palettes.

- [ ] **Wu — alpha channel support**  
  `WuColorExtractor` builds its histogram over RGB only. Extend `Box` and the moment arrays to handle RGBA so transparent pixels are excluded from the variance calculation rather than silently contributing to it.

- [ ] **New algorithm — DBSCAN**  
  Density-based clustering naturally handles irregularly shaped color clusters and doesn't require specifying `k` in advance. Add `PaletteAlgorithm.Dbscan` and a corresponding extractor.

- [ ] **New algorithm — CIEDE2000 K-Means**  
  Variant of K-Means where distances are computed in CIELAB using the CIEDE2000 metric rather than Euclidean RGB, producing more perceptually uniform clusters.

---

## 🔬 Post-processing

- [ ] **Configurable diversity threshold**  
  The `minDeltaE` parameter in `RemoveNearDuplicateByDeltaE` and the Lab farthest-point sampler in `SelectDiverse` use hard-coded defaults. Expose these through `PaletteGenerator.ExtractColorPaletteAsync` so callers can tune the deduplication aggressiveness.

- [ ] **Weighted diversity sampling**  
  `SelectDiverse` treats all candidates equally. Weight the farthest-point selection by `PixelCount` so colors that represent more pixels are preferred when two candidates are equidistant in Lab space.

- [ ] **CIEDE2000 in post-processing**  
  `RemoveNearDuplicateByDeltaE` currently uses CIE76 (simple Euclidean Lab distance). Upgrade to CIEDE2000 for more perceptually accurate deduplication, especially in the blue region where CIE76 is known to be inaccurate.

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

- [ ] **Export palette to file**  
  After displaying results, offer to save the palette as a JSON file (`[{ "hex": "#2B4F82", "pixels": 48210 }, ...]`), a CSS custom-properties snippet, or a GIMP/Adobe `.gpl` palette file.

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
  Create an `OverTone.Tests` project. Each extractor should have at least: a smoke test on a known 2×2 pixel image, a test that `colorCount` is respected, and a test that transparent pixels are excluded.

- [ ] **Regression tests for Wu and K-Means fixes**  
  The Wu `Maximize()` bounds fix and the K-Means subsampling are non-trivial. Add regression tests with small deterministic inputs so these regressions can't silently reappear.

- [ ] **Benchmark project**  
  Add a `BenchmarkDotNet` project that measures extraction time and memory for each algorithm on a standard 1080p test image. Use as a baseline before any performance work.

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
