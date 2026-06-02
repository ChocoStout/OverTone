using OverTone.Algorithms;
using OverTone.Processing;
using Xunit;

namespace OverTone.Tests;

public class AlgorithmRobustnessTests
{
    private static readonly PaletteGenerator Generator = new();

    // Three solid colors — fewer than a typical request. This is the case that exposed the Wu NRE
    // and the duplicate / 0-px output.
    private static byte[] ThreeColors(int width = 300, int height = 60) =>
        SyntheticImage.VerticalStripes(width, height,
            ((220, 20, 60), 1.0),
            ((40, 160, 60), 1.0),
            ((40, 90, 200), 1.0));

    [Theory]
    [InlineData(PaletteAlgorithm.Wu)]        // regression: used to throw NullReferenceException
    [InlineData(PaletteAlgorithm.KMeans)]
    [InlineData(PaletteAlgorithm.MedianCut)]
    [InlineData(PaletteAlgorithm.Octree)]
    [InlineData(PaletteAlgorithm.Popularity)]
    public async Task Handles_more_colors_requested_than_present(PaletteAlgorithm algorithm)
    {
        var palette = await Generator.ExtractColorPaletteAsync(ThreeColors(), colorCount: 8, algorithm: algorithm);

        Assert.NotEmpty(palette);
        Assert.True(palette.Count <= 8);
    }

    [Fact]
    public async Task Diverse_selection_emits_no_exact_duplicates()
    {
        // Three distinct colors, five requested — must not pad the palette with duplicates.
        var palette = await Generator.ExtractColorPaletteAsync(ThreeColors(), colorCount: 5,
            algorithm: PaletteAlgorithm.KMeans, selection: PaletteSelectionMode.Diverse);

        var distinct = palette.Select(c => (c.R, c.G, c.B)).Distinct().Count();
        Assert.Equal(palette.Count, distinct);   // every entry is a different color
        Assert.True(palette.Count <= 3);          // at most the number of distinct colors present
    }

    [Fact]
    public async Task FuzzyCMeans_is_deterministic_across_runs()
    {
        // Small image keeps the (unsubsampled) FCM fast.
        var bytes = ThreeColors(width: 30, height: 4);

        var a = await Generator.ExtractColorPaletteAsync(bytes, 3, algorithm: PaletteAlgorithm.FuzzyCMeans);
        var b = await Generator.ExtractColorPaletteAsync(bytes, 3, algorithm: PaletteAlgorithm.FuzzyCMeans);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
            Assert.Equal((a[i].R, a[i].G, a[i].B), (b[i].R, b[i].G, b[i].B));
    }

    [Fact]
    public async Task Wu_recovers_the_actual_colors()
    {
        // Regression for the cumulative-moment fix: before it, Wu's box queries were wrong and it
        // returned garbage (or nothing). Each source color should now be represented within a tiny ΔE.
        var palette = await Generator.ExtractColorPaletteAsync(ThreeColors(), colorCount: 3,
            algorithm: PaletteAlgorithm.Wu, selection: PaletteSelectionMode.Dominant);

        (byte R, byte G, byte B)[] sources = [(220, 20, 60), (40, 160, 60), (40, 90, 200)];
        Assert.All(sources, target =>
        {
            var targetLab = ColorMetrics.RgbToLab(target.R, target.G, target.B);
            Assert.Contains(palette, c =>
                ColorMetrics.DeltaE76(ColorMetrics.RgbToLab(c.R, c.G, c.B), targetLab) < 10);
        });
    }

    [Fact]
    public async Task Parallel_kmeans_matches_sequential()
    {
        var bytes = SyntheticImage.ManyColors(400, 30); // ~12k pixels → exercises the parallel path

        var sequential = await Generator.ExtractColorPaletteAsync(bytes, 8,
            algorithm: PaletteAlgorithm.KMeans, maxDegreeOfParallelism: 1);
        var parallel = await Generator.ExtractColorPaletteAsync(bytes, 8,
            algorithm: PaletteAlgorithm.KMeans, maxDegreeOfParallelism: 4);

        // Integer channel sums make parallel accumulation order-independent → bit-identical palettes.
        Assert.Equal(sequential.Count, parallel.Count);
        for (var i = 0; i < sequential.Count; i++)
            Assert.Equal(
                (sequential[i].R, sequential[i].G, sequential[i].B, sequential[i].PixelCount),
                (parallel[i].R, parallel[i].G, parallel[i].B, parallel[i].PixelCount));
    }

    [Fact]
    public async Task Octree_does_not_truncate_large_color_counts()
    {
        // Regression: colorCount was cast to byte, so a request whose value exceeded 255 wrapped
        // around (e.g. 256 → 0) and collapsed the palette to a handful of colors.
        var many = SyntheticImage.ManyColors(400, 8);
        var palette = await new OctreeColorExtractor().ExtractColorPaletteAsync(many, colorCount: 256);

        Assert.True(palette.Count > 64, $"expected > 64 colors, got {palette.Count}");
    }
}
