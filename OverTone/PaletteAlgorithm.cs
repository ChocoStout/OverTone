namespace OverTone;

/// <summary>
/// Specifies the algorithm used to extract a color palette from an image.
/// </summary>
public enum PaletteAlgorithm
{
    /// <summary>
    /// SLIC superpixel segmentation, merged into regions that each contribute a representative (peak)
    /// color — image-space and region-aware. The default and recommended algorithm.
    /// </summary>
    Slic,

    /// <summary>
    /// Spatial 5D K-Means clustering on <c>(L, a, b, x, y)</c>. With <c>SpatialWeight = 0</c> it reduces
    /// to classic color-space K-Means; higher weights make geometry matter.
    /// </summary>
    SpatialKMeans,
}
