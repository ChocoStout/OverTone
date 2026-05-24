using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts a dominant color palette from image data using the Median Cut algorithm.
/// </summary>
/// <remarks>
/// The algorithm recursively splits the set of colors along the color channel with the
/// largest range, choosing the median as the split point, until the requested number of
/// color boxes is reached. Each box's average color is returned as a palette entry.
/// </remarks>
public class MedianCutColorExtractor : IColorPaletteExtractor
{
    /// <inheritdoc />
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.MedianCut;

    /// <summary>
    /// Extracts a palette of dominant colors using the Median Cut algorithm.
    /// </summary>
    /// <param name="imageData">Raw image bytes (PNG, JPEG, etc.).</param>
    /// <param name="colorCount">The requested number of colors in the output palette.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> ordered by pixel frequency.</returns>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        if (colorCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(colorCount), "colorCount must be greater than zero");

        var imageResult = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);

        var pixelData = imageResult.Data;

        // Collect RGB vectors, ignoring transparent pixels (alpha <= 128).
        // Each entry is a 3-element byte[]: [R, G, B]
        var rgbPoints = new List<byte[]>();

        for (var pixelIndex = 0; pixelIndex < pixelData.Length; pixelIndex += 4)
        {
            var red = pixelData[pixelIndex];
            var green = pixelData[pixelIndex + 1];
            var blue = pixelData[pixelIndex + 2];
            var alpha = pixelData[pixelIndex + 3];

            if (alpha > 128)
                rgbPoints.Add([red, green, blue]);
        }

        if (rgbPoints.Count == 0)
            throw new Exception("No visible pixels found on image");

        // Start with a single box containing all colors
        var colorBoxes = new List<ColorBox> { new(rgbPoints) };

        // Split boxes until we reach the desired number or cannot split further
        while (colorBoxes.Count < colorCount)
        {
            // Choose the box with the largest color range to split
            var boxWithLargestRange = colorBoxes
                .OrderByDescending(b => b.ColorRange)
                .FirstOrDefault(b => b.ColorPoints.Count > 1);

            if (boxWithLargestRange == null)
                break; // No splittable box available

            colorBoxes.Remove(boxWithLargestRange);

            var (leftBox, rightBox) = boxWithLargestRange.Split();

            colorBoxes.Add(leftBox);
            colorBoxes.Add(rightBox);
        }

        // Convert boxes into ColorPalette entries
        var colorPalettes = colorBoxes.Select(b => new ColorPalette
        {
            R = b.AverageRed,
            G = b.AverageGreen,
            B = b.AverageBlue,
            PixelCount = b.ColorPoints.Count
        }).OrderByDescending(p => p.PixelCount).ToList();

        return await Task.FromResult(colorPalettes);
    }
}
