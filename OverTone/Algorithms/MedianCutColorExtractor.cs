using OverTone;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts a dominant color palette using the Median Cut algorithm: recursively split the set of
/// colors along the channel with the largest range, at the median, until the requested number of
/// boxes is reached. Each box's average color is returned. Visible pixels are stride-sampled to ≤ 10k.
/// </summary>
public sealed class MedianCutColorExtractor : ColorPaletteExtractorBase
{
    private const int MaxSamples = 10_000;

    /// <inheritdoc />
    public override PaletteAlgorithm Algorithm => PaletteAlgorithm.MedianCut;

    /// <inheritdoc />
    protected override List<ColorPalette> ExtractCore(byte[] rgba, int colorCount, int maxDegreeOfParallelism)
    {
        var rgbPoints = ExtractVisiblePixels(rgba, MaxSamples);

        var colorBoxes = new List<ColorBox> { new(rgbPoints) };

        while (colorBoxes.Count < colorCount)
        {
            var boxToSplit = colorBoxes
                .OrderByDescending(b => b.ColorRange)
                .FirstOrDefault(b => b.ColorPoints.Count > 1);

            if (boxToSplit == null)
                break;

            colorBoxes.Remove(boxToSplit);
            var (leftBox, rightBox) = boxToSplit.Split();
            colorBoxes.Add(leftBox);
            colorBoxes.Add(rightBox);
        }

        return colorBoxes
            .Select(b => new ColorPalette
            {
                R = b.AverageRed,
                G = b.AverageGreen,
                B = b.AverageBlue,
                PixelCount = b.ColorPoints.Count
            })
            .OrderByDescending(p => p.PixelCount)
            .ToList();
    }
}
