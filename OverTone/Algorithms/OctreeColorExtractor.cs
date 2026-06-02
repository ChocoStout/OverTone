using OverTone;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts a dominant color palette using Octree color quantization. Colors are inserted into an
/// octree whose leaves aggregate similar colors; the tree is then pruned until at most the requested
/// number of leaves remain. Histogram-based, so memory stays bounded regardless of image size.
/// </summary>
public sealed class OctreeColorExtractor : ColorPaletteExtractorBase
{
    /// <inheritdoc />
    public override PaletteAlgorithm Algorithm => PaletteAlgorithm.Octree;

    /// <inheritdoc />
    protected override List<ColorPalette> ExtractCore(byte[] rgba, int colorCount, int maxDegreeOfParallelism)
    {
        var octree = new Octree();

        for (var i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i + 3] <= 128)
                continue; // skip transparent pixels
            octree.AddColor(rgba[i], rgba[i + 1], rgba[i + 2]);
        }

        octree.Reduce(colorCount);

        return octree.GetPalette()
            .Take(colorCount)
            .Select(e => new ColorPalette { R = e.R, G = e.G, B = e.B, PixelCount = e.Count })
            .OrderByDescending(p => p.PixelCount)
            .ToList();
    }
}
