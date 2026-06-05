# OverTone — TODO

Tracked ideas, improvements, and known gaps. Items are grouped by area and loosely ordered by priority within each group.

---

## 🧭 Design notes & product direction

Captured from real-image experiments (Lena, TS *1989*). Context for the work below — especially palette generation.

> **Update — the image-space migration shipped.** OverTone now extracts in *image space* by default: SLIC superpixels + spatial 5D K-Means → region palette, with peak/representative colors, a saliency-ranked `Salient` selection mode, and a no-config `GetColors` facade. The color-space mental model below is the **historical motivation** — it explains why the old quantizers disappointed and why segmentation replaced them.

**Three different "palette" goals — don't conflate them.** Each feature serves exactly one:
1. **Representative / dominant** — "the N main colors actually in the image," coverage-driven. What `GetColors` orders by. On a neutral-heavy image you still get neutrals, because that's what the pixels *are*.
2. **Salient / named** — "the colors a person would call out" (the blue, the red lipstick) even when they're a tiny fraction of pixels. Delivered by **region-aware segmentation + the `Salient` mode** (people name colors by *region* — sweatshirt, lips — and saliency = chroma × area lifts small vivid regions). *Not* global frequency.
3. **Generated / harmonized** — a full, vivid, accessible scheme. The **semantic palette** work below; it may *boost* saturation and synthesize roles, i.e. it doesn't just report what's in the image.

**How the *old* color-space clustering behaved (the mental model that motivated the migration):**
- It worked in **color space, not image space** — pixel position was discarded. "Adjacent in the photo" was irrelevant; only "close in color" made colors group.
- A pixel joined its **nearest** cluster and each returned color was the **average** of its members, so averages **drift to dull midpoints**. A small blue region beside neutrals didn't become teal — it became a muddy gray-blue (observed: `#292C37`) or was absorbed into the nearest dark cluster. Vivid named accents *cannot* fall out of pure frequency clustering on a neutral-dominated image. **The fix:** segment by region, then take a *peak* (representative) color per region, not the mean.
- **Corollary (still true in image space):** extraction returns what's *in the pixels* — it can't manufacture saturation. A washed-out source yields a muted palette; guaranteed punch is the *harmonize* path (goal 3), not extraction.

**`meanDeltaE` is the wrong objective for theming.** It measures how well a palette reconstructs the *majority* of pixels, so on a neutral-heavy image it rewards all-neutral palettes and ranks the *least* vibrant approach "best." It's a reconstruction-fidelity metric, not a theming-quality score — the sample's compare view now shows it for context only, not as a ranking.

---

## 🎨 Algorithms

- [x] **Image-space / region-aware extraction — the migration** ✅  
  Done. Extraction now works in image space: `SlicColorExtractor` (SLIC superpixels → region-adjacency color merge → region palette) is the default, with `SpatialKMeansColorExtractor` (5D `(L, a, b, x, y)`, `SpatialWeight` dial; `0` = legacy color clustering) as the simpler companion. Regions emit a **representative (peak) color, not the mean** (`RepresentativeColor`); a saliency rank (chroma × area, with a neutral floor) surfaces small vivid accents; and the no-config `GetColors` facade ties it together with OkLab de-dup + true-coverage reassignment. This is the family the original "the red region forms its own cluster" intuition was reaching for.

- **Removed (recoverable via git history).** The pure color-space quantizers — K-Means, Median Cut, Octree, Fuzzy C-Means, Popularity, Wu, NeuQuant — and their helpers/options were deleted in the migration: they cluster a *bag of colors* (position discarded) and average members to muddy midpoints, exactly what disappointed on real album art. Restore any from history if a histogram quantizer is ever needed again. This retires the old per-algorithm fixes that used to live here (Octree's broken reduction, Wu/Octree 64-bit widening, Wu alpha, Median Cut variance split, K-Means convergence threshold, NeuQuant decay/neighbourhood, MMCQ, DBSCAN-on-colors).

### Future spatial / perceptual work
- [ ] **OkLab-space segmentation** — run SLIC / 5D K-Means in OkLab rather than CIELAB for more uniform spacing, especially in the blues (OkLab already powers `GetColors` de-dup). Likely the best bang-for-buck perceptual upgrade.
- [ ] **CIEDE2000 for merge & selection** — the RAG merge and the ΔE selection paths use CIE76; add `ColorMetrics.DeltaE2000` and use it in `RegionPaletteBuilder` and `RemoveNearDuplicate*` for more accurate grouping, then verify against the Sharma et al. reference pairs.
- [ ] **Smarter region merging** — the current merge is single-linkage by mean-Lab ΔE with no connectivity guarantee. Consider Felzenszwalb–Huttenlocher graph segmentation (edge-weight / region-size heuristics) and optional connectivity enforcement so each region is one connected blob.
- [ ] **Mean Shift (joint color+space domain)** — a bandwidth-only, `k`-free segmenter that finds color "modes"; a good parameter-light alternative to SLIC.
- [ ] **K-Medoids representative** — option to return an *actual image pixel* per region (instead of the modal-bin mean) for "real swatch" use cases (LED output).
- [ ] **Large images — decompression-bomb guard & pluggable `IImageDecoder`** — opt-in maximum-pixel limit before decode, plus a decoder seam for RAW (CR2/NEF/ARW/DNG), which `StbImageSharp` cannot read. (Working resolution is already bounded by `MaxPixels` box-downscaling.)

---

## 🔬 Post-processing

- [x] **Selection modes & configurable thresholds**  
  Done — `PaletteGenerator.ExtractColorPaletteAsync` exposes `minDeltaE` and `candidatePoolMultiplier`, plus a `PaletteSelectionMode`: `Salient` (chroma × area), `Diverse` (farthest-point in Lab), and `Dominant` (frequency with ΔE de-dup).

- [x] **Saliency + peak color (was: Vibrant / Muted swatch extraction)**  
  Largely delivered — the two musts learned from the data are now in: (1) `SelectSalient` scores by **saturation × population** (with a neutral floor) so a small vivid region can beat a large dull one; (2) `RepresentativeColor` returns a **peak/modal color, not the cluster mean**. **Still TODO:** named, Android-Palette-style roles — *Vibrant, Muted, Dark Vibrant, Light Vibrant, Dark Muted* — scored against HSL targets, as a small dedicated swatch API.

- [ ] **Weighted diversity sampling**  
  `SelectDiverse` treats all candidates equally. Weight the farthest-point selection by `PixelCount` so colors that represent more pixels are preferred when two candidates are equidistant in Lab space.

---

## 🌈 Semantic & accessible palette generation

Turn the extracted colors into a complete, ready-to-use UI palette — not just a list of swatches, but **named roles with accessible pairings and tonal ramps**. This is the "dynamic color from album art" idea (à la Material 3) and the highest-leverage remaining feature for the music-player / theming use cases. A new `PaletteScheme` / `DesignTokens` model + builder would sit on top of the extractors and `GetColors`.

- [x] **One-call "main colors" API** *(the primary, no-config use case)*  
  Done — `Palette.GetColorsAsync(image, n)` (and `PaletteGenerator.GetColorsAsync`) "just returns the N main colors" with no algorithm, mode, or ΔE to choose. Under the hood: SLIC region palette → **OkLab perceptual de-duplication** (distinct colors, not five shades of cream) → **saliency** ranking (so album-art accents aren't ranked out by frequency) → **true coverage** (every pixel reassigned to its nearest returned color, so percentages are real). Semantic *names* are available via `ColorNaming.NearestName` (not yet attached to the returned model — see the roles item).

- [ ] **Semantic roles**  
  Derive design-system roles from the extracted palette: `primary`, `secondary`, `tertiary`, plus `neutral` / `surface` / `background`, and status colors `success` / `warning` / `error` (alert) / `info`. Primary/secondary/tertiary come from the image's dominant + salient colors; status colors are conventional hues (green / amber / red / blue), optionally harmonized toward the primary. Also emit the matching **"on" colors** (`onPrimary`, `onSurface`, …) — the text/icon color that sits on each role, chosen for contrast.

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
  Add `CancellationToken` to `ExtractColorPaletteAsync` so long-running extractors (SLIC, spatial K-Means) can be cancelled by the caller without waiting for them to finish.

- [ ] **`ColorPalette` — add `A` (alpha) channel**  
  Useful for images with transparency. Extractors already read the alpha channel to filter invisible pixels; surfacing it in the model costs nothing.

- [ ] **`ColorPalette` — `ToArgb()` / `ToHsl()` helpers**  
  Convenience conversions that consumer apps commonly need. Keeps the conversion logic centralised rather than every caller reimplementing it.

- [ ] **`PaletteGenerator` — `IAsyncEnumerable<ColorPalette>` overload**  
  Stream palette entries one by one as they are discovered. Useful for large palettes or progressive UI rendering.

---

## 🖥️ Sample app

- [x] **Export palette to file**  
  Done — export is a first-class library feature (`PaletteExporter` / `IPaletteExporter`): JSON, hex list, C/Arduino array, CSS, SCSS, Tailwind; the sample offers an export menu after results. Design-tool formats (GIMP/Adobe `.gpl`, `.ase`, SVG) and mobile (Android `colors.xml`, iOS) are not yet implemented — add one by dropping in a new `IPaletteExporter`.

- [x] **Cross-platform CLI + "get main colors" + comparison**  
  Done — the sample is cross-platform (no WinForms), takes an image path/URL (or `testcard`) as an argument, leads with a no-config **Get main colors** option, and has a **Compare** view (mean ΔE + timing shown for context, not as a ranking) exportable via `--json`. **Follow-up:** surface a vibrancy/coverage signal in the compare view so the side-by-side reflects the user's goal, not just reconstruction error.

- [ ] **Progress reporting for slow runs**  
  The spinner shows activity but not progress. For SLIC / spatial K-Means, emit iteration progress (e.g. `iteration 6/10`) so the user knows it's advancing rather than stuck.

---

## 🧪 Tests

- [ ] **Per-extractor coverage**  
  `OverTone.Tests` covers spatial behavior (`SpatialExtractionTests`: region surfacing, small-accent survival, peak-not-mean, determinism, parallel == sequential), accent recovery on a synthetic "1989-like" image, DI wiring, exporters, and image validation. Still thin per-extractor: add explicit `colorCount`-respected and transparent-pixel-excluded smoke tests for both `Slic` and `SpatialKMeans` (the latter needs an RGBA fixture — the BMP generator is opaque-only today).

- [ ] **Benchmark project**  
  *Quality* benchmarking exists (`PaletteQuality.MeanDeltaE` + the ground-truth `SyntheticImage`/test card). Still TODO: a `BenchmarkDotNet` project measuring extraction *time* and memory for each algorithm on a standard 1080p image, as a baseline before performance work.

---

## 🔧 Infrastructure

- [ ] **GitHub Actions CI**  
  Add a `.github/workflows/build.yml` that restores, builds, and runs tests on `ubuntu-latest` and `windows-latest` for every push and PR.

- [ ] **NuGet publish workflow**  
  Add a `release.yml` workflow that packs `OverTone.csproj` and publishes to NuGet.org when a `v*` tag is pushed. The `PackageLicenseExpression`, `PackageReadmeFile`, and `PackageTags` are already wired up in the csproj.

- [ ] **`CHANGELOG.md`**  
  Start a changelog following [Keep a Changelog](https://keepachangelog.com/) conventions so release notes are easy to produce — the image-space migration (removed algorithms, new `GetColors`, breaking `PaletteGenerator` signature change) is a natural first entry.

- [x] **`LICENSE` file**  
  Done — `LICENSE` contains the **MIT** license text, matching `PackageLicenseExpression = MIT` in both packable projects (core + DI extensions).
