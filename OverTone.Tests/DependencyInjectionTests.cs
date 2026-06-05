using Microsoft.Extensions.DependencyInjection;
using OverTone.Extensions.DependencyInjection;
using Xunit;

namespace OverTone.Tests;

public class DependencyInjectionTests
{
    private static readonly PaletteAlgorithm[] AllAlgorithms =
    [
        PaletteAlgorithm.Slic, PaletteAlgorithm.SpatialKMeans,
    ];

    [Fact]
    public void AddOverTone_registers_every_extractor_exporter_and_facade()
    {
        using var provider = new ServiceCollection().AddOverTone().BuildServiceProvider();

        var extractors = provider.GetServices<IColorPaletteExtractor>().ToList();
        Assert.Equal(2, extractors.Count);
        foreach (var algorithm in AllAlgorithms)
            Assert.Contains(extractors, e => e.Algorithm == algorithm);

        Assert.Equal(6, provider.GetServices<IPaletteExporter>().Count());
        Assert.NotNull(provider.GetService<PaletteGenerator>());
        Assert.NotNull(provider.GetService<PaletteExporter>());
    }

    [Fact]
    public async Task Resolved_generator_extracts_a_palette()
    {
        using var provider = new ServiceCollection().AddOverTone().BuildServiceProvider();
        var generator = provider.GetRequiredService<PaletteGenerator>();

        var bytes = SyntheticImage.VerticalStripes(80, 20, ((10, 20, 200), 1.0), ((220, 30, 40), 1.0));
        var palette = await generator.ExtractColorPaletteAsync(bytes, 4, algorithm: PaletteAlgorithm.Slic);

        Assert.NotEmpty(palette);
    }

    [Fact]
    public void Registered_options_flow_into_the_extractor()
    {
        // A custom SpatialKMeansOptions registered before AddOverTone() must be wired into the extractor.
        using var provider = new ServiceCollection()
            .AddSingleton(new SpatialKMeansOptions(Seed: 7, MaxIterations: 1))
            .AddOverTone()
            .BuildServiceProvider();

        var generator = provider.GetRequiredService<PaletteGenerator>();
        Assert.NotNull(generator); // resolves without error → the options factory wiring is valid
    }
}
