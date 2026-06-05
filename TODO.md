# OverTone — TODO

Tracked ideas, improvements, and known gaps. Items are grouped by area and loosely ordered by priority within each group.

---

## 🧭 Design notes & product direction

Captured from real-image experiments (Lena, TS *1989*). Context for the work below — especially palette generation.

**Three different "palette" goals — don't conflate them.** Each planned feature serves exactly one:
1. **Representative / dominant** — "the N main colors actually in the image," frequency-driven. What the planned `GetColors` returns by default. On a neutral-heavy image you get neutrals, because that's what the pixels *are*.
2. **Salient / named** — "the colors a person would call out" (the blue, the red lipstick) even when they're a tiny fraction of pixels. Needs the **Vibrant/Muted** saliency path *or* **region-aware segmentation** (people name colors by *region* — sweatshirt, lips — see the segmentation item) — *not* global frequency.
3. **Generated / harmonized** — a full, vivid, accessible scheme. The **semantic palette** work; it may *boost* saturation and synthesize roles, i.e. it doesn't just report what's in the image.

**How the clustering math actually behaves (the mental model):**
- It works in **color space, not image space** — pixel position is discarded. "Adjacent in the photo" is irrelevant; only "close in color" makes colors group.
- A pixel joins its **nearest** cluster and each returned color is the **average** of its members, so averages **drift to dull midpoints**. A small blue region beside neutrals doesn't become teal — it becomes a muddy gray-blue (observed: `#292C37`) or gets absorbed into the nearest dark cluster. So vivid named accents *cannot* fall out of pure frequency clustering on a neutral-dominated image.
- **Corollary:** extraction returns what's *in the pixels* — it can't manufacture saturation. A washed-out source yields a muted palette; guaranteed punch is the *harmonize* path (goal 3), not extraction.

**`meanDeltaE` is the wrong objective for theming.** It measures how well a palette reconstructs the *majority* of pixels, so on a neutral-heavy image it rewards all-neutral palettes and ranks the *least* vibrant algorithm "best." Use it for reconstruction fidelity, not for choosing a theming palette (see the comparison-view follow-up).

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

- [ ] **Octree — fix the broken reduction (high priority)**  
  Confirmed badly broken on real photos: in the 1989 / Lena runs Octree returned 6–12 near-identical *near-black* colors with pixel counts of 1–3 and mean ΔE of **47–62** (vs ~5–11 for every other algorithm). The reduction's leaf accounting is approximate and collapses on images with many distinct colors — it returns the deepest, rarest leaves instead of the dominant merged buckets. Rewrite `Octree.Reduce` to merge only *reducible* nodes (all children are leaves), lowest-population first, decrementing the leaf count exactly. It's also slow (~11 s on a 512² image — no subsampling + a thrashing reduction). Until fixed, the planned `GetColors` API must not route to Octree.

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

- [ ] **Spatial / region-aware extraction (segmentation)** *(matches the "colors a person names" intent)*  
  Every current extractor works in **color space only** — pixel position is discarded. To group by *region* ("the blue sweatshirt", "the red lips") rather than by global color frequency, bring spatial information in. A ladder from easiest to most capable:
  - **5D K-Means** — cluster pixels as `(L, a, b, x, y)` instead of `(L, a, b)`, with a `spatialWeight` dial (0 = today's pure-color behavior; higher = spatially coherent blobs). Smallest change; reuses the existing K-Means.
  - **SLIC superpixels** — the canonical color+space method (k-means in 5D with a spatially-localized search window + a compactness term). Produces compact regions; aggregate them into a palette weighted by area. *Mean Shift* in the joint color+spatial domain and *Felzenszwalb–Huttenlocher* graph segmentation are alternatives.
  - **Region-based palette** — segmentation yields regions with (color, area, centroid). Take one representative color per major/distinct region → naturally surfaces *object* colors (sweatshirt, hair, background), which is **how people actually name colors**. Use a *peak* color per region (not the region mean) to keep saturation, and a saliency rank so a small-but-distinct region (lipstick) still makes the cut.
  
  Honest trade-offs: heavier than the histogram quantizers (5D, iterations); the region mean is still muted (a navy sweatshirt → muted navy, not vivid blue — use a peak pixel + the harmonize path for punch); and `spatialWeight`/compactness are dials, so the no-config `GetColors` would just pick sensible defaults. This is the family the original "the red region forms its own cluster" intuition was reaching for.

---

## 🔬 Post-processing

- [x] **Configurable diversity threshold**  
  Done — `PaletteGenerator.ExtractColorPaletteAsync` now exposes `minDeltaE` and `candidatePoolMultiplier`, plus a `PaletteSelectionMode` (`Diverse`/`Dominant`) to choose between farthest-point sampling and frequency-based selection.

- [ ] **Weighted diversity sampling**  
  `SelectDiverse` treats all candidates equally. Weight the farthest-point selection by `PixelCount` so colors that represent more pixels are preferred when two candidates are equidistant in Lab space.

- [ ] **CIEDE2000 in post-processing**  
  `RemoveNearDuplicateByDeltaE` currently uses CIE76 (simple Euclidean Lab distance). Upgrade to CIEDE2000 for more perceptually accurate deduplication, especially in the blue region where CIE76 is known to be inaccurate.

- [ ] **Vibrant / Muted swatch extraction (Android Palette-style)**  
  A selection layer (not a quantizer) on top of any extractor that picks *semantic* swatches — Vibrant, Muted, Dark Vibrant, Light Vibrant, Dark Muted — by scoring candidates on HSL targets weighted by population. This is exactly how music apps theme their UI from album art, making it the highest-value addition for the music-player use case. Fits as a new `PaletteSelectionMode` (e.g. `Vibrant`) or a small dedicated swatch API. **Two musts (learned from the data):** (1) score by **saturation × population** so a small vivid region can beat a large dull one; (2) return a **representative/peak color, not the cluster mean** — averaging is precisely what desaturated the 1989 accents into muddy gray-blue in testing.

---

## 🌈 Semantic & accessible palette generation

Turn the extracted colors into a complete, ready-to-use UI palette — not just a list of swatches, but **named roles with accessible pairings and tonal ramps**. This is the "dynamic color from album art" idea (à la Material 3) and the highest-leverage feature for the music-player / theming use cases. A new `PaletteScheme` / `DesignTokens` model + builder would sit on top of the existing extractors.

- [ ] **One-call "main colors" API** *(the primary, no-config use case)*  
  A dead-simple entry point — e.g. `OverTone.GetColors(image, n)` — that "just returns the N main colors," with the caller choosing **no algorithm, no selection mode, no ΔE**. Under the hood: a sensible default extractor → **perceptual de-duplication** (group near-duplicates in OkLab so you get *distinct* colors, not five shades of cream) → **true coverage** (assign every image pixel to its nearest returned color so the percentages are real and comparable across runs) → **semantic names**. For images with small-but-meaningful accents (album-art blue / red), blend in the saliency signal from the Vibrant/Muted item so minority colors aren't ranked out by frequency. This is the library's most common use case and is currently harder than it should be (today the caller must pick an algorithm + mode + count and interpret ΔE).

- [ ] **Semantic roles**  
  Derive design-system roles from the extracted palette: `primary`, `secondary`, `tertiary`, plus `neutral` / `surface` / `background`, and status colors `success` / `warning` / `error` (alert) / `info`. Primary/secondary/tertiary come from the image's dominant + accent colors (builds on the Vibrant/Muted item); status colors are conventional hues (green / amber / red / blue), optionally harmonized toward the primary. Also emit the matching **"on" colors** (`onPrimary`, `onSurface`, …) — the text/icon color that sits on each role, chosen for contrast.

- [ ] **Tonal scales (shade ramps)**  
  For each role, generate a consistent ramp of tints/shades — e.g. Tailwind-style `50…950` or a Material-3 tonal palette — by varying *tone* in a perceptual space (OkLCh / HCT) rather than naive HSL, so the steps look evenly spaced. Reuses the planned OkLab work.

- [ ] **WCAG contrast & accessibility**  
  Add `ColorMetrics` helpers for WCAG **relative luminance** and **contrast ratio**, and verify each role/"on" pairing against the standard thresholds — **4.5:1** for normal text and **3:1** for large text & UI components (AA), **7:1** (AAA). Auto-select black vs white (or nudge the tone) so every pairing passes, and flag any that can't. Consider an **APCA** (WCAG 3 draft) mode as the modern perceptual alternative to the 2.x ratio.

- [ ] **Harmony-based derivation**  
  When an image yields too few distinct accents, synthesize secondary/tertiary from the primary via color-theory schemes — complementary, analogous, triadic, split-complementary — as hue rotations in OkLCh that hold chroma/tone.

- [ ] **Token export**  
  Emit the generated scheme through the existing exporters as design tokens: CSS custom properties, an SCSS map, and a Tailwind `theme.extend.colors` tree (role → shade → hex). Likely a new `IPaletteExporter` (or a dedicated builder) backed by the `PaletteScheme` model.

Industry references to follow: **WCAG 2.2** contrast (relative-luminance formula, 4.5 / 3 / 7 ratios), **APCA** (WCAG 3 draft); **Material Design 3** (HCT color space, tonal palettes, dynamic color from a seed image); **Tailwind** numeric scales; **Radix Colors** / **IBM Carbon** (accessible stepped systems); and **Adobe Leonardo** (generate ramps to hit target contrast ratios).

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

- [x] **Side-by-side algorithm comparison**  
  Done — the sample's "Compare" view runs every algorithm on one image, ranks by mean ΔE, and shows swatches + timing; also exportable via "run all → JSON" (`--json`). **Follow-up:** ranking on ΔE *alone* is misleading for theming — it favors neutral-heavy palettes (see Design notes), so surface a **vibrancy / coverage** signal next to ΔE so "best" reflects the user's goal, not just reconstruction error.

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
