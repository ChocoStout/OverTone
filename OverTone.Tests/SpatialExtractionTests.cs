using OverTone.Algorithms;
using OverTone.Processing;
using Xunit;

namespace OverTone.Tests;

/// <summary>
/// Behavioral tests for the image-space (segmentation) extractors. These encode the migration's core
/// promises: regions surface as colors, a small vivid accent survives against a large dull background,
/// representative colors are peaks (not desaturated means), and results stay deterministic.
/// </summary>
public class SpatialExtractionTests
{
    private static readonly PaletteGenerator Generator = new();

    private static bool Contains(IEnumerable<ColorPalette> palette, (byte R, byte G, byte B) target, double tolerance = 25.0)
    {
        var targetLab = ColorMetrics.RgbToLab(target.R, target.G, target.B);
        return palette.Any(c => ColorMetrics.DeltaE76(ColorMetrics.RgbToLab(c.R, c.G, c.B), targetLab) < tolerance);
    }

    [Fact]
    public void RepresentativeColor_picks_the_mode_not_the_mean()
    {
        // A region that is 80% vivid red and 20% white. The arithmetic mean is a washed-out pink; the
        // representative must stay red (the mode), which is exactly the desaturation fix.
        var acc = new RepresentativeColorAccumulator();
        for (var i = 0; i < 80; i++) acc.Add(200, 30, 40);
        for (var i = 0; i < 20; i++) acc.Add(240, 240, 240);

        var (r, g, b) = acc.Resolve();

        var rep = ColorMetrics.RgbToLab(r, g, b);
        var red = ColorMetrics.RgbToLab(200, 30, 40);
        var mean = ColorMetrics.RgbToLab(208, 72, 80); // (200·80 + 240·20)/100 per channel

        Assert.True(ColorMetrics.DeltaE76(rep, red) < 5, $"representative {r},{g},{b} should be ~red");
        Assert.True(ColorMetrics.DeltaE76(rep, red) < ColorMetrics.DeltaE76(rep, mean));
    }

    [Theory]
    [InlineData(PaletteAlgorithm.Slic)]
    [InlineData(PaletteAlgorithm.SpatialKMeans)]
    public async Task Surfaces_both_colors_of_a_two_region_image(PaletteAlgorithm algorithm)
    {
        var bytes = SyntheticImage.VerticalStripes(200, 80, ((40, 90, 200), 1.0), ((220, 30, 40), 1.0));

        var palette = await Generator.ExtractColorPaletteAsync(bytes, 4,
            algorithm: algorithm, selection: PaletteSelectionMode.Salient);

        Assert.True(Contains(palette, (40, 90, 200)), "expected the blue region");
        Assert.True(Contains(palette, (220, 30, 40)), "expected the red region");
    }

    [Fact]
    public async Task Salient_surfaces_a_small_vivid_accent_over_a_dominant_neutral()
    {
        // 90% near-black, a 6% vivid red accent, a 4% white accent. Frequency alone would bury the red;
        // saliency (chroma × area) must lift it above the equally-small white, while black still shows.
        var bytes = SyntheticImage.VerticalStripes(300, 80,
            ((20, 20, 25), 0.90),
            ((220, 20, 60), 0.06),
            ((235, 235, 235), 0.04));

        var palette = await Generator.ExtractColorPaletteAsync(bytes, 4,
            algorithm: PaletteAlgorithm.Slic, selection: PaletteSelectionMode.Salient);

        Assert.True(Contains(palette, (220, 20, 60)), "the small vivid red accent should survive");
        Assert.True(Contains(palette, (20, 20, 25)), "the dominant near-black should still surface");
    }

    [Theory]
    [InlineData(PaletteAlgorithm.Slic)]
    [InlineData(PaletteAlgorithm.SpatialKMeans)]
    public async Task Is_deterministic_across_runs(PaletteAlgorithm algorithm)
    {
        var bytes = SyntheticImage.VerticalStripes(220, 60,
            ((220, 20, 60), 1.0), ((40, 160, 60), 1.0), ((40, 90, 200), 1.0));

        var a = await Generator.ExtractColorPaletteAsync(bytes, 5, algorithm: algorithm);
        var b = await Generator.ExtractColorPaletteAsync(bytes, 5, algorithm: algorithm);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
            Assert.Equal((a[i].R, a[i].G, a[i].B, a[i].PixelCount), (b[i].R, b[i].G, b[i].B, b[i].PixelCount));
    }

    [Fact]
    public async Task SpatialKMeans_parallel_matches_sequential()
    {
        var bytes = SyntheticImage.ManyColors(400, 30); // ~12k pixels → exercises the parallel path

        var sequential = await Generator.ExtractColorPaletteAsync(bytes, 8,
            algorithm: PaletteAlgorithm.SpatialKMeans, maxDegreeOfParallelism: 1);
        var parallel = await Generator.ExtractColorPaletteAsync(bytes, 8,
            algorithm: PaletteAlgorithm.SpatialKMeans, maxDegreeOfParallelism: 4);

        // Parallel assignment + sequential center update → bit-identical palettes regardless of threads.
        Assert.Equal(sequential.Count, parallel.Count);
        for (var i = 0; i < sequential.Count; i++)
            Assert.Equal(
                (sequential[i].R, sequential[i].G, sequential[i].B, sequential[i].PixelCount),
                (parallel[i].R, parallel[i].G, parallel[i].B, parallel[i].PixelCount));
    }

    [Theory]
    [InlineData(PaletteAlgorithm.Slic)]
    [InlineData(PaletteAlgorithm.SpatialKMeans)]
    public async Task Handles_more_colors_requested_than_present(PaletteAlgorithm algorithm)
    {
        var bytes = SyntheticImage.VerticalStripes(200, 40,
            ((220, 20, 60), 1.0), ((40, 160, 60), 1.0), ((40, 90, 200), 1.0));

        var palette = await Generator.ExtractColorPaletteAsync(bytes, 8, algorithm: algorithm);

        Assert.NotEmpty(palette);
        Assert.True(palette.Count <= 8);
        var distinct = palette.Select(c => (c.R, c.G, c.B)).Distinct().Count();
        Assert.Equal(palette.Count, distinct); // no duplicate padding
    }

    [Fact]
    public async Task GetColors_returns_distinct_main_colors_with_true_coverage()
    {
        const int w = 240, h = 80;
        var bytes = SyntheticImage.VerticalStripes(w, h,
            ((220, 20, 60), 1.0), ((40, 160, 60), 1.0), ((40, 90, 200), 1.0));

        var colors = await Generator.GetColorsAsync(bytes, 3);

        Assert.NotEmpty(colors);
        Assert.True(colors.Count <= 3);

        var distinct = colors.Select(c => (c.R, c.G, c.B)).Distinct().Count();
        Assert.Equal(colors.Count, distinct);

        // Coverage reassigns every (opaque) pixel to its nearest returned color, so counts sum to all pixels.
        Assert.Equal((long)w * h, colors.Sum(c => (long)c.PixelCount));

        // The three vivid source colors should all be recovered.
        Assert.True(Contains(colors, (220, 20, 60)));
        Assert.True(Contains(colors, (40, 160, 60)));
        Assert.True(Contains(colors, (40, 90, 200)));
    }
}
