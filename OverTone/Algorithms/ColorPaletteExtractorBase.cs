using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// Base class for all built-in color palette extractors. It owns the work every extractor shares —
/// decoding the image to RGBA and exposing the visible pixels — and implements the
/// <see cref="IColorPaletteExtractor"/> overloads, so a concrete extractor only has to implement
/// <see cref="ExtractCore"/>. This keeps the algorithms small and uniform (and easy to register for DI).
/// </summary>
public abstract class ColorPaletteExtractorBase : IColorPaletteExtractor
{
    /// <inheritdoc />
    public abstract PaletteAlgorithm Algorithm { get; }

    /// <inheritdoc />
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
        => ExtractColorPaletteAsync(imageData, colorCount, 1);

    /// <inheritdoc />
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount, int maxDegreeOfParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(colorCount);

        var rgba = DecodeRgba(imageData);
        var palette = ExtractCore(rgba, colorCount, Math.Max(1, maxDegreeOfParallelism));
        return Task.FromResult(palette);
    }

    /// <summary>
    /// Performs the algorithm-specific extraction from a decoded, tightly-packed RGBA buffer.
    /// </summary>
    /// <param name="rgba">Decoded image as RGBA bytes (4 per pixel).</param>
    /// <param name="colorCount">Number of colors to return.</param>
    /// <param name="maxDegreeOfParallelism">Worker-thread cap; <c>1</c> means sequential.</param>
    protected abstract List<ColorPalette> ExtractCore(byte[] rgba, int colorCount, int maxDegreeOfParallelism);

    /// <summary>
    /// Decodes encoded image bytes (PNG, JPEG, BMP, …) into a tightly-packed RGBA buffer.
    /// </summary>
    protected static byte[] DecodeRgba(byte[] imageData) =>
        ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha).Data;

    /// <summary>
    /// Collects visible pixels (alpha &gt; 128) as <c>[R, G, B]</c> triplets. When
    /// <paramref name="maxSamples"/> is greater than zero and the image has more visible pixels than
    /// that, the pixels are stride-subsampled down to roughly <paramref name="maxSamples"/> — this keeps
    /// the per-pixel algorithms (and memory) bounded on very large images.
    /// </summary>
    /// <exception cref="InvalidOperationException">No visible pixels were found.</exception>
    protected static List<byte[]> ExtractVisiblePixels(byte[] rgba, int maxSamples = 0)
    {
        var points = new List<byte[]>();
        for (var i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i + 3] <= 128)
                continue;
            points.Add([rgba[i], rgba[i + 1], rgba[i + 2]]);
        }

        if (points.Count == 0)
            throw new InvalidOperationException("No visible pixels found in the image.");

        if (maxSamples > 0 && points.Count > maxSamples)
        {
            var stride = points.Count / maxSamples;
            var sampled = new List<byte[]>(maxSamples);
            for (var s = 0; s < points.Count; s += stride)
                sampled.Add(points[s]);
            points = sampled;
        }

        return points;
    }
}
