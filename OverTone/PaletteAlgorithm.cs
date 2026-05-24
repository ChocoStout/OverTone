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
    Dedupe,
}
