# Changelog

All notable changes to **OverTone** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This file covers both published packages — **OverTone** and
**OverTone.Extensions.DependencyInjection** — which are versioned together.

## [Unreleased]

## [1.0.0] - 2026-06-06

### Added
- **Image-space palette extraction**: `Slic` (SLIC superpixels merged into regions) and
  `SpatialKMeans` (5D K-Means on `(L, a, b, x, y)`), each contributing a representative
  (peak) color per region rather than a desaturated mean.
- One-call `Palette.GetColorsAsync(...)` facade, plus `PaletteGenerator.ExtractColorPaletteAsync(...)`
  for image files, URLs, or in-memory bytes.
- Selection modes: `Salient` (chroma × area), `Diverse` (farthest-point in CIELAB), and `Dominant`.
- Six palette exporters — JSON, hex list, C array, CSS, SCSS, Tailwind — via `IPaletteExporter`.
- Theming: OkLCh-based semantic color schemes (`SchemeBuilder`, color roles, light/dark pairs).
- `PaletteQuality.MeanDeltaE` quality metric and image-format validation before decoding.
- Optional parallelism (`maxDegreeOfParallelism`) with deterministic, order-independent results.
- **OverTone.Extensions.DependencyInjection** package with a single `AddOverTone()` registration.
- Multi-targeting for **.NET 8.0** and **.NET 10.0**.
- Source Link, deterministic builds, a package icon, and symbol packages (`.snupkg`).

[Unreleased]: https://github.com/ChocoStout/OverTone/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/ChocoStout/OverTone/releases/tag/v1.0.0
