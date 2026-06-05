namespace OverTone;

/// <summary>
/// Controls how the final palette is chosen from the larger candidate pool an algorithm produces.
/// </summary>
public enum PaletteSelectionMode
{
    /// <summary>
    /// Maximally distinct colors that span the image's chromatic range, via farthest-point sampling
    /// in CIELAB, seeded with the most dominant color. Best when you want a varied, "designer" palette
    /// that surfaces accent colors even when they cover few pixels. This is the default.
    /// </summary>
    Diverse,

    /// <summary>
    /// The most frequent colors in the image, with perceptual near-duplicates merged. Best when you
    /// want colors in roughly the proportions they appear — the literal main colors of a photo.
    /// </summary>
    Dominant,

    /// <summary>
    /// The colors a person would actually name — ranked by <em>saliency</em>, a blend of chroma and
    /// area, so a small but vivid region (e.g. lips) can outrank a large dull one (e.g. sky), while a
    /// dominant neutral still surfaces. Pairs with the region-aware extractors and is the default for
    /// the no-config <c>GetColors</c> entry point.
    /// </summary>
    Salient,
}
