using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts a dominant color palette from image data using Octree color quantization.
/// </summary>
/// <remarks>
/// Octree quantization builds a tree where leaves represent aggregated colors. The tree
/// is pruned until the requested number of leaves (colors) remains. 
/// </remarks>
public class OctreeColorExtractor : IColorPaletteExtractor
{
    /// <inheritdoc />
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.Octree;

    /// <summary>
    /// Extracts a palette of dominant colors using the Octree quantization algorithm.
    /// </summary>
    /// <param name="imageData">Raw image bytes (PNG, JPEG, etc.).</param>
    /// <param name="colorCount">The requested number of colors in the output palette.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> ordered by pixel frequency.</returns>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        if (colorCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(colorCount), "colorCount must be greater than zero");

        var image = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
        var pixelData = image.Data;

        // Build octree from visible pixels
        var octree = new Octree();

        for (var i = 0; i < pixelData.Length; i += 4)
        {
            var r = pixelData[i];
            var g = pixelData[i + 1];
            var b = pixelData[i + 2];
            var a = pixelData[i + 3];

            if (a <= 128)
                continue; // skip transparent pixels

            octree.AddColor(r, g, b);
        }

        // Reduce the tree until we have at most colorCount leaves
        octree.Reduce((byte)colorCount);

        var paletteEntries = octree.GetPalette().Take(colorCount).Select(e => new ColorPalette
        {
            R = e.R,
            G = e.G,
            B = e.B,
            PixelCount = e.Count
        }).OrderByDescending(p => p.PixelCount).ToList();

        return await Task.FromResult(paletteEntries);
    }
}
