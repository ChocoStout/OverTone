using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// Base class for all built-in color palette extractors. It owns the work every extractor shares —
/// decoding the image to RGBA and exposing the visible pixels — and implements the
/// <see cref="IColorPaletteExtractor"/> overloads, so a concrete extractor only has to implement
/// <see cref="ExtractCore(DecodedImage, int, int)"/>. This keeps the algorithms small and uniform (and
/// easy to register for DI).
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

        var image = DecodeImage(imageData);
        var palette = ExtractCore(image, colorCount, Math.Max(1, maxDegreeOfParallelism));
        return Task.FromResult(palette);
    }

    /// <summary>
    /// Performs the algorithm-specific extraction from a decoded, tightly-packed RGBA buffer.
    /// </summary>
    /// <param name="rgba">Decoded image as RGBA bytes (4 per pixel).</param>
    /// <param name="colorCount">Number of colors to return.</param>
    /// <param name="maxDegreeOfParallelism">Worker-thread cap; <c>1</c> means sequential.</param>
    /// <remarks>
    /// Legacy color-only entry point. During the image-space migration this is a virtual bridge target
    /// rather than an abstract method: spatial extractors override <see cref="ExtractCore(DecodedImage, int, int)"/>
    /// instead and never reach this. (It will be removed once all extractors are spatial.)
    /// </remarks>
    protected virtual List<ColorPalette> ExtractCore(byte[] rgba, int colorCount, int maxDegreeOfParallelism)
        => throw new NotSupportedException(
            "This extractor implements the image-space ExtractCore(DecodedImage, …) overload.");

    /// <summary>
    /// Performs the algorithm-specific extraction from a decoded image (RGBA bytes plus dimensions).
    /// Spatial extractors override this directly because they need pixel position; the default bridges
    /// to the legacy color-only <see cref="ExtractCore(byte[], int, int)"/> for extractors that work on
    /// color alone.
    /// </summary>
    /// <param name="image">The decoded image (RGBA buffer + width/height).</param>
    /// <param name="colorCount">Number of colors to return.</param>
    /// <param name="maxDegreeOfParallelism">Worker-thread cap; <c>1</c> means sequential.</param>
    protected virtual List<ColorPalette> ExtractCore(DecodedImage image, int colorCount, int maxDegreeOfParallelism)
        => ExtractCore(image.Rgba, colorCount, maxDegreeOfParallelism);

    /// <summary>
    /// Box-downscales an image so it has at most <paramref name="maxPixels"/> pixels, preserving the 2D
    /// grid: each output pixel is the average of the input pixels it covers (alpha included). Spatial
    /// segmentation is heavier than histogram counting, so bounding the working resolution keeps it
    /// fast — and averaging (rather than nearest-neighbor) avoids aliasing small accents out of
    /// existence. Returns the image unchanged when it is already small enough.
    /// </summary>
    protected static DecodedImage DownscaleToMaxPixels(DecodedImage image, int maxPixels)
    {
        var n = (long)image.Width * image.Height;
        if (maxPixels <= 0 || n <= maxPixels || image.Width <= 1 || image.Height <= 1)
            return image;

        var scale = Math.Sqrt((double)maxPixels / n);
        var dstW = Math.Max(1, (int)(image.Width * scale));
        var dstH = Math.Max(1, (int)(image.Height * scale));

        var src = image.Rgba;
        var dst = new byte[dstW * dstH * 4];

        for (var dy = 0; dy < dstH; dy++)
        {
            var sy0 = (int)((long)dy * image.Height / dstH);
            var sy1 = Math.Max(sy0 + 1, (int)((long)(dy + 1) * image.Height / dstH));
            for (var dx = 0; dx < dstW; dx++)
            {
                var sx0 = (int)((long)dx * image.Width / dstW);
                var sx1 = Math.Max(sx0 + 1, (int)((long)(dx + 1) * image.Width / dstW));

                long sr = 0, sg = 0, sb = 0, sa = 0, cnt = 0;
                for (var sy = sy0; sy < sy1; sy++)
                {
                    var row = sy * image.Width;
                    for (var sx = sx0; sx < sx1; sx++)
                    {
                        var si = (row + sx) * 4;
                        sr += src[si];
                        sg += src[si + 1];
                        sb += src[si + 2];
                        sa += src[si + 3];
                        cnt++;
                    }
                }

                var di = (dy * dstW + dx) * 4;
                dst[di] = (byte)(sr / cnt);
                dst[di + 1] = (byte)(sg / cnt);
                dst[di + 2] = (byte)(sb / cnt);
                dst[di + 3] = (byte)(sa / cnt);
            }
        }

        return new DecodedImage(dst, dstW, dstH);
    }

    /// <summary>
    /// Decodes encoded image bytes (PNG, JPEG, BMP, …) into a tightly-packed RGBA buffer.
    /// </summary>
    protected static byte[] DecodeRgba(byte[] imageData) =>
        ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha).Data;

    /// <summary>
    /// Decodes encoded image bytes (PNG, JPEG, BMP, …) into a <see cref="DecodedImage"/> — a packed
    /// RGBA buffer together with its width and height.
    /// </summary>
    protected static DecodedImage DecodeImage(byte[] imageData)
    {
        var result = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
        return new DecodedImage(result.Data, result.Width, result.Height);
    }

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
