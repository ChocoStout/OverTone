using Microsoft.Extensions.DependencyInjection;
using OverTone.Algorithms;
using OverTone.Export;

namespace OverTone.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection registration for OverTone.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all built-in OverTone services — every <see cref="IColorPaletteExtractor"/> and
    /// <see cref="IPaletteExporter"/>, plus the <see cref="PaletteGenerator"/> and
    /// <see cref="PaletteExporter"/> facades — as singletons.
    /// </summary>
    /// <remarks>
    /// Per-algorithm tuning is honored if registered: e.g. register a <see cref="KMeansOptions"/> before
    /// calling this method and the K-Means extractor will pick it up. Extractors are created via explicit
    /// factories, so options resolution doesn't depend on the container's optional-parameter behavior.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddOverTone(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Extractors — options pulled from the container when registered, otherwise defaults.
        services.AddSingleton<IColorPaletteExtractor>(sp => new SlicColorExtractor(sp.GetService<SlicOptions>()));
        services.AddSingleton<IColorPaletteExtractor>(sp => new SpatialKMeansColorExtractor(sp.GetService<SpatialKMeansOptions>()));
        services.AddSingleton<IColorPaletteExtractor>(sp => new KMeansColorExtractor(sp.GetService<KMeansOptions>()));
        services.AddSingleton<IColorPaletteExtractor>(_ => new MedianCutColorExtractor());
        services.AddSingleton<IColorPaletteExtractor>(_ => new OctreeColorExtractor());
        services.AddSingleton<IColorPaletteExtractor>(sp => new FuzzyCMeansColorExtractor(sp.GetService<FuzzyCMeansOptions>()));
        services.AddSingleton<IColorPaletteExtractor>(sp => new PopularityColorExtractor(sp.GetService<PopularityOptions>()));
        services.AddSingleton<IColorPaletteExtractor>(_ => new WuColorExtractor());
        services.AddSingleton<IColorPaletteExtractor>(sp => new NeuQuantColorExtractor(sp.GetService<NeuQuantOptions>()));

        // Exporters.
        services.AddSingleton<IPaletteExporter>(_ => new JsonPaletteExporter());
        services.AddSingleton<IPaletteExporter>(_ => new HexListPaletteExporter());
        services.AddSingleton<IPaletteExporter>(_ => new CArrayPaletteExporter());
        services.AddSingleton<IPaletteExporter>(_ => new CssPaletteExporter());
        services.AddSingleton<IPaletteExporter>(_ => new ScssPaletteExporter());
        services.AddSingleton<IPaletteExporter>(_ => new TailwindPaletteExporter());

        // Facades, built from the registered collections.
        services.AddSingleton(sp => new PaletteGenerator(sp.GetServices<IColorPaletteExtractor>()));
        services.AddSingleton(sp => new PaletteExporter(sp.GetServices<IPaletteExporter>()));

        return services;
    }
}
