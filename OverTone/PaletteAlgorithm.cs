namespace OverTone;

/// <summary>
/// Specifies the algorithm used to extract a color palette from an image.
/// </summary>
public enum PaletteAlgorithm
{
    KMeans,
    MedianCut,
    Octree,
    FuzzyCMeans,
    Popularity,
    Wu,
    NeuQuant,

    /// <summary>SLIC superpixel segmentation → region palette (image-space, region-aware). The default.</summary>
    Slic,

    /// <summary>Spatial 5D K-Means clustering on <c>(L, a, b, x, y)</c> (image-space).</summary>
    SpatialKMeans,
}
