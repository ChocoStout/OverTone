using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts the top-N most frequent colors from an image using a popularity histogram.
/// The implementation reduces color depth to limit histogram size (default 5 bits per channel).
/// </summary>
public class PopularityColorExtractor : IColorPaletteExtractor
{
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.Popularity;

    private readonly int _bitsPerChannel;

    public PopularityColorExtractor(int bitsPerChannel = 5)
    {
        if (bitsPerChannel < 1 || bitsPerChannel > 8)
            throw new ArgumentOutOfRangeException(nameof(bitsPerChannel));

        _bitsPerChannel = bitsPerChannel;
    }

    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        if (colorCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(colorCount));

        var image = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
        var data = image.Data;

        var shift = 8 - _bitsPerChannel;
        var histogram = new Dictionary<int, int>();

        for (var i = 0; i < data.Length; i += 4)
        {
            var a = data[i + 3];
            if (a <= 128) continue;

            var r = data[i] >> shift;
            var g = data[i + 1] >> shift;
            var b = data[i + 2] >> shift;

            var key = (r << (_bitsPerChannel * 2)) | (g << _bitsPerChannel) | b;
            histogram.TryGetValue(key, out var cnt);
            histogram[key] = cnt + 1;
        }

        if (histogram.Count == 0)
            throw new Exception("No visible pixels found on image");

        // Take top colorCount buckets
        var top = histogram.OrderByDescending(kv => kv.Value).Take(colorCount).ToList();

        var palettes = new List<ColorPalette>(top.Count);

        foreach (var kv in top)
        {
            var key = kv.Key;
            var b = key & ((1 << _bitsPerChannel) - 1);
            var g = (key >> _bitsPerChannel) & ((1 << _bitsPerChannel) - 1);
            var r = (key >> (_bitsPerChannel * 2)) & ((1 << _bitsPerChannel) - 1);

            // Expand back to 8-bit by shifting left and filling lower bits
            var r8 = (byte)((r << shift) | (r >> (_bitsPerChannel - shift)));
            var g8 = (byte)((g << shift) | (g >> (_bitsPerChannel - shift)));
            var b8 = (byte)((b << shift) | (b >> (_bitsPerChannel - shift)));

            palettes.Add(new ColorPalette { R = r8, G = g8, B = b8, PixelCount = kv.Value });
        }

        return await Task.FromResult(palettes);
    }
}
