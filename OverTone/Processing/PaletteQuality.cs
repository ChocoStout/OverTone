using System.Collections.Concurrent;
using OverTone;
using StbImageSharp;

namespace OverTone.Processing;

/// <summary>
/// Objective quality metrics for a generated palette. Useful for tuning algorithms and settings, and
/// for comparing them on the same image with a single comparable number instead of eyeballing swatches.
/// </summary>
public static class PaletteQuality
{
    // Below this many samples, parallel overhead outweighs the benefit; run sequentially.
    private const int ParallelThreshold = 4096;

    /// <summary>
    /// Computes the mean CIE76 Delta-E between every visible pixel of an image and its nearest color in
    /// <paramref name="palette"/>. This is the palette's quantization error: <b>lower is better</b> — it
    /// measures how faithfully the palette can represent the image. Large images are stride-subsampled.
    /// </summary>
    /// <param name="imageData">Raw image bytes (PNG, JPEG, BMP, …).</param>
    /// <param name="palette">The palette to score.</param>
    /// <param name="maxSamples">Upper bound on the number of pixels sampled.</param>
    /// <param name="maxDegreeOfParallelism">
    /// Maximum worker threads; values &lt;= 1 run sequentially. Parallel runs may differ from sequential
    /// by a negligible floating-point rounding amount (summation order), which is immaterial for a mean.
    /// </param>
    /// <returns>The mean Delta-E (0 = perfect), or 0 when the palette or image is empty.</returns>
    public static double MeanDeltaE(byte[] imageData, IReadOnlyList<ColorPalette> palette,
        int maxSamples = 100_000, int maxDegreeOfParallelism = 1)
    {
        if (palette.Count == 0)
            return 0.0;

        var image = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
        var pixels = image.Data;
        var pixelCount = pixels.Length / 4;
        if (pixelCount == 0)
            return 0.0;

        // Pre-compute Lab for each palette color once.
        var paletteLab = new (double L, double a, double b)[palette.Count];
        for (var i = 0; i < palette.Count; i++)
            paletteLab[i] = ColorMetrics.RgbToLab(palette[i].R, palette[i].G, palette[i].B);

        var stride = Math.Max(1, pixelCount / Math.Max(1, maxSamples));
        var sampleCount = (pixelCount + stride - 1) / stride;

        // Nearest-palette ΔE for one sampled pixel; NaN signals a transparent pixel to skip.
        double DeltaForSample(int sampleIndex)
        {
            var i = sampleIndex * stride * 4;
            if (pixels[i + 3] <= 128)
                return double.NaN;

            var lab = ColorMetrics.RgbToLab(pixels[i], pixels[i + 1], pixels[i + 2]);
            var nearest = double.MaxValue;
            foreach (var pl in paletteLab)
            {
                var d = ColorMetrics.DeltaE76(lab, pl);
                if (d < nearest)
                    nearest = d;
            }
            return nearest;
        }

        var total = 0.0;
        var counted = 0L;

        if (maxDegreeOfParallelism <= 1 || sampleCount < ParallelThreshold)
        {
            for (var s = 0; s < sampleCount; s++)
            {
                var d = DeltaForSample(s);
                if (double.IsNaN(d)) continue;
                total += d;
                counted++;
            }
        }
        else
        {
            var sync = new object();
            Parallel.ForEach(
                Partitioner.Create(0, sampleCount),
                new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                () => (sum: 0.0, cnt: 0L),
                (range, _, local) =>
                {
                    for (var s = range.Item1; s < range.Item2; s++)
                    {
                        var d = DeltaForSample(s);
                        if (double.IsNaN(d)) continue;
                        local.sum += d;
                        local.cnt++;
                    }
                    return local;
                },
                local =>
                {
                    lock (sync)
                    {
                        total += local.sum;
                        counted += local.cnt;
                    }
                });
        }

        return counted == 0 ? 0.0 : total / counted;
    }
}
