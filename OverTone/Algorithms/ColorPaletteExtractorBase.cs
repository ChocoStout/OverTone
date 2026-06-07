using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// Base class for all built-in color palette extractors. It owns the work every extractor shares —
/// decoding the image and bounding its working size — and implements the
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
        => ExtractColorPaletteAsync(imageData, colorCount, 1, CancellationToken.None);

    /// <inheritdoc />
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount, int maxDegreeOfParallelism)
        => ExtractColorPaletteAsync(imageData, colorCount, maxDegreeOfParallelism, CancellationToken.None);

    /// <inheritdoc />
    public Task<List<ColorPalette>> ExtractColorPaletteAsync(
        byte[] imageData, int colorCount, int maxDegreeOfParallelism, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(colorCount);
        cancellationToken.ThrowIfCancellationRequested();

        var image = DecodeImage(imageData);
        var palette = ExtractCore(image, colorCount, ClampParallelism(maxDegreeOfParallelism), cancellationToken);
        return Task.FromResult(palette);
    }

    /// <summary>
    /// Clamps a requested worker-thread count to a sane range: at least 1, and never more than
    /// <see cref="Environment.ProcessorCount"/>. Oversubscribing CPU-bound segmentation doesn't speed it
    /// up and can starve co-hosted work; the extractors are deterministic, so the clamp only affects speed.
    /// </summary>
    protected static int ClampParallelism(int maxDegreeOfParallelism)
        => Math.Clamp(maxDegreeOfParallelism, 1, Environment.ProcessorCount);

    /// <summary>
    /// Performs the algorithm-specific extraction from a decoded image (RGBA bytes plus dimensions). This
    /// is the one member a concrete extractor must implement; override the cancellation-aware overload too
    /// if the algorithm can check a token between iterations.
    /// </summary>
    /// <param name="image">The decoded image (RGBA buffer + width/height).</param>
    /// <param name="colorCount">Number of colors to return.</param>
    /// <param name="maxDegreeOfParallelism">Worker-thread cap; <c>1</c> means sequential.</param>
    protected abstract List<ColorPalette> ExtractCore(DecodedImage image, int colorCount, int maxDegreeOfParallelism);

    /// <summary>
    /// Cancellation-aware extraction. The default delegates to
    /// <see cref="ExtractCore(DecodedImage, int, int)"/> and ignores the token; the built-in extractors
    /// override this to check <paramref name="cancellationToken"/> between iterations so a long run can be
    /// abandoned. Overriding it is optional — a custom extractor that ignores cancellation needs only the
    /// three-argument overload.
    /// </summary>
    /// <param name="image">The decoded image (RGBA buffer + width/height).</param>
    /// <param name="colorCount">Number of colors to return.</param>
    /// <param name="maxDegreeOfParallelism">Worker-thread cap; <c>1</c> means sequential.</param>
    /// <param name="cancellationToken">Token observed between iterations for cooperative cancellation.</param>
    protected virtual List<ColorPalette> ExtractCore(
        DecodedImage image, int colorCount, int maxDegreeOfParallelism, CancellationToken cancellationToken)
        => ExtractCore(image, colorCount, maxDegreeOfParallelism);

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
    /// Decodes encoded image bytes (PNG, JPEG, BMP, …) into a <see cref="DecodedImage"/> — a packed
    /// RGBA buffer together with its width and height.
    /// </summary>
    protected static DecodedImage DecodeImage(byte[] imageData)
    {
        var result = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
        return new DecodedImage(result.Data, result.Width, result.Height);
    }
}
