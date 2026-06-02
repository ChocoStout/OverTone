using OverTone;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts the top-N most frequent colors using a popularity histogram. Color depth is reduced to
/// limit histogram size (default 5 bits per channel). Histogram-based, so memory stays bounded.
/// </summary>
public sealed class PopularityColorExtractor : ColorPaletteExtractorBase
{
    private readonly int _bitsPerChannel;

    /// <summary>Creates the extractor with the given options (defaults when <c>null</c>).</summary>
    public PopularityColorExtractor(PopularityOptions? options = null)
    {
        var bits = (options ?? new PopularityOptions()).BitsPerChannel;
        if (bits is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(options), "BitsPerChannel must be between 1 and 8.");
        _bitsPerChannel = bits;
    }

    /// <inheritdoc />
    public override PaletteAlgorithm Algorithm => PaletteAlgorithm.Popularity;

    /// <inheritdoc />
    protected override List<ColorPalette> ExtractCore(byte[] rgba, int colorCount, int maxDegreeOfParallelism)
    {
        var shift = 8 - _bitsPerChannel;
        var histogram = new Dictionary<int, int>();

        for (var i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i + 3] <= 128)
                continue;

            var r = rgba[i] >> shift;
            var g = rgba[i + 1] >> shift;
            var b = rgba[i + 2] >> shift;

            var key = (r << (_bitsPerChannel * 2)) | (g << _bitsPerChannel) | b;
            histogram.TryGetValue(key, out var cnt);
            histogram[key] = cnt + 1;
        }

        if (histogram.Count == 0)
            throw new InvalidOperationException("No visible pixels found in the image.");

        var top = histogram.OrderByDescending(kv => kv.Value).Take(colorCount).ToList();
        var palettes = new List<ColorPalette>(top.Count);

        foreach (var kv in top)
        {
            var key = kv.Key;
            var b = key & ((1 << _bitsPerChannel) - 1);
            var g = (key >> _bitsPerChannel) & ((1 << _bitsPerChannel) - 1);
            var r = (key >> (_bitsPerChannel * 2)) & ((1 << _bitsPerChannel) - 1);

            // Expand back to 8-bit by replicating the high bits into the low bits.
            var r8 = (byte)((r << shift) | (r >> (_bitsPerChannel - shift)));
            var g8 = (byte)((g << shift) | (g >> (_bitsPerChannel - shift)));
            var b8 = (byte)((b << shift) | (b >> (_bitsPerChannel - shift)));

            palettes.Add(new ColorPalette { R = r8, G = g8, B = b8, PixelCount = kv.Value });
        }

        return palettes;
    }
}
