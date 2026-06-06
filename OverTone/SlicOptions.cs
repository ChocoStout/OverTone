namespace OverTone;

/// <summary>
/// Tuning for the SLIC superpixel extractor (<see cref="PaletteAlgorithm.Slic"/>).
/// </summary>
/// <param name="SuperpixelCount">
/// Target number of superpixels SLIC over-segments the image into, <em>before</em> region merging.
/// Deliberately decoupled from the requested color count — SLIC's job is honest over-segmentation;
/// merging and selection reduce it to the final palette. Higher = finer regions.
/// </param>
/// <param name="Compactness">
/// SLIC compactness <c>m</c>. Higher values favor square, spatially-regular superpixels; lower values
/// (the default) hug color edges, which is what palette extraction wants.
/// </param>
/// <param name="Iterations">
/// Fixed number of Lloyd iterations. SLIC converges visually within ~10; a fixed count (no
/// convergence threshold) also keeps results deterministic.
/// </param>
/// <param name="RegionMergeDeltaE">
/// Adjacent superpixels whose aggregate mean-color CIE76 ΔE falls below this threshold are merged into
/// a single region (so a large object spanning many superpixels becomes one region, and a small vivid
/// region stays one high-saliency candidate). Set to 0 to disable merging.
/// </param>
/// <param name="MaxPixels">
/// The image is box-downscaled to at most this many pixels before segmentation, to keep the heavier
/// spatial work bounded on large images.
/// </param>
public record SlicOptions(
    int SuperpixelCount = 256,
    double Compactness = 10.0,
    int Iterations = 10,
    double RegionMergeDeltaE = 8.0,
    int MaxPixels = 180_000);
