using OverTone.Processing;
using Xunit;

namespace OverTone.Tests;

public class AlgorithmQualityTests
{
    private static readonly PaletteGenerator Generator = new();

    // A "1989-like" image: a dominant light/cream background with small chromatic accents —
    // the exact situation where naive, frequency-only extraction returns only neutrals.
    private static readonly (byte R, byte G, byte B) Cream  = (245, 240, 230);
    private static readonly (byte R, byte G, byte B) Black  = (20, 20, 25);
    private static readonly (byte R, byte G, byte B) Blue   = (40, 90, 200);
    private static readonly (byte R, byte G, byte B) Orange = (230, 130, 40);
    private static readonly (byte R, byte G, byte B) Teal   = (30, 150, 150);

    private static byte[] NineteenEightyNineLike() =>
        SyntheticImage.VerticalStripes(400, 100,
            (Cream,  0.55),
            (Black,  0.15),
            (Blue,   0.12),
            (Orange, 0.10),
            (Teal,   0.08));

    private static async Task<List<ColorPalette>> ExtractAsync(
        PaletteAlgorithm algorithm, PaletteSelectionMode selection = PaletteSelectionMode.Diverse, int colorCount = 5)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ot-{Guid.NewGuid():N}.bmp");
        await File.WriteAllBytesAsync(path, NineteenEightyNineLike());
        try
        {
            return await Generator.ExtractColorPaletteAsync(path, colorCount, isUrl: false,
                algorithm: algorithm, selection: selection);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool ContainsNear(IEnumerable<ColorPalette> palette, (byte R, byte G, byte B) target, double maxDeltaE)
    {
        var targetLab = ColorMetrics.RgbToLab(target.R, target.G, target.B);
        return palette.Any(c =>
            ColorMetrics.DeltaE76(ColorMetrics.RgbToLab(c.R, c.G, c.B), targetLab) <= maxDeltaE);
    }

    [Fact]
    public async Task KMeans_diverse_recovers_the_chromatic_accents()
    {
        var palette = await ExtractAsync(PaletteAlgorithm.KMeans, PaletteSelectionMode.Diverse);

        // The whole point of the 1989 case: the accent colors must surface, not just the neutrals.
        Assert.True(ContainsNear(palette, Blue, 25),   "expected a blue accent in the palette");
        Assert.True(ContainsNear(palette, Orange, 25), "expected an orange accent in the palette");
        Assert.True(ContainsNear(palette, Teal, 25),   "expected a teal accent in the palette");
    }

    [Fact]
    public async Task KMeans_is_deterministic_across_runs()
    {
        var a = await ExtractAsync(PaletteAlgorithm.KMeans);
        var b = await ExtractAsync(PaletteAlgorithm.KMeans);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].R, b[i].R);
            Assert.Equal(a[i].G, b[i].G);
            Assert.Equal(a[i].B, b[i].B);
        }
    }

    [Fact]
    public async Task Dominant_mode_leads_with_the_background()
    {
        var palette = await ExtractAsync(PaletteAlgorithm.KMeans, PaletteSelectionMode.Dominant);

        // Cream is ~55% of the image, so dominant mode (ordered by frequency) should put it first.
        Assert.True(ContainsNear([palette[0]], Cream, 20), "dominant mode should lead with the background color");
    }

    [Fact]
    public async Task MeanDeltaE_improves_with_more_colors()
    {
        var bytes = NineteenEightyNineLike();
        var path = Path.Combine(Path.GetTempPath(), $"ot-{Guid.NewGuid():N}.bmp");
        await File.WriteAllBytesAsync(path, bytes);
        try
        {
            var few  = await Generator.ExtractColorPaletteAsync(path, 3, isUrl: false, algorithm: PaletteAlgorithm.KMeans);
            var many = await Generator.ExtractColorPaletteAsync(path, 5, isUrl: false, algorithm: PaletteAlgorithm.KMeans);

            var errFew  = PaletteQuality.MeanDeltaE(bytes, few);
            var errMany = PaletteQuality.MeanDeltaE(bytes, many);

            // Representing all five colors should leave less quantization error than representing three.
            Assert.True(errMany <= errFew + 1e-6, $"expected error with 5 colors ({errMany}) <= 3 colors ({errFew})");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
