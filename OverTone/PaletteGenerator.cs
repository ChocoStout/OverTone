using System.Reflection;
using OverTone.Algorithms;
using OverTone.Processing;

namespace OverTone;

/// <summary>
/// Provides methods to extract a color palette from an image source.
/// Supports extracting from local files or remote URLs and delegates the
/// actual extraction work to an <see cref="IColorPaletteExtractor"/> implementation.
/// </summary>
public class PaletteGenerator
{
    private readonly HttpClient _httpClient = new();

    // Populated at runtime by reflecting over implementations of IColorPaletteExtractor
    private readonly Dictionary<PaletteAlgorithm, IColorPaletteExtractor> _colorPaletteExtractors;

    /// <summary>
    /// Creates a new <see cref="PaletteGenerator"/> and discovers available palette extractors
    /// in the current assembly using reflection.
    /// </summary>
    public PaletteGenerator()
    {
        _colorPaletteExtractors = new Dictionary<PaletteAlgorithm, IColorPaletteExtractor>();

        var extractorInterface = typeof(IColorPaletteExtractor);
        var assembly = Assembly.GetExecutingAssembly();

        var extractorTypes = assembly
            .GetTypes()
            .Where(t => extractorInterface.IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

        foreach (var type in extractorTypes)
        {
            try
            {
                // Accept a true parameterless constructor OR one where every parameter
                // has a default value (e.g. PopularityColorExtractor(int bitsPerChannel = 5)).
                var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(c => c.GetParameters().All(p => p.HasDefaultValue));

                if (ctor is null) continue;

                var args = ctor.GetParameters()
                    .Select(p => p.DefaultValue)
                    .ToArray();

                if (ctor.Invoke(args) is not IColorPaletteExtractor instance)
                    continue;

                _colorPaletteExtractors.TryAdd(instance.Algorithm, instance);
            }
            catch
            {
                // Ignore types that cannot be instantiated (abstract base, bad ctor, etc.)
            }
        }
    }

    /// <summary>
    /// Extracts a list of dominant colors from the given image source.
    /// </summary>
    /// <param name="source">A file path or URL to the image.</param>
    /// <param name="colorCount">The number of colors to return in the palette.</param>
    /// <param name="isUrl">True when <paramref name="source"/> is a URL; false when it is a local file path.</param>
    /// <param name="algorithm">The clustering algorithm to use for extraction.</param>
    /// <param name="dedupe">
    /// When <c>true</c>, uses a 4× candidate pool and removes perceptually near-duplicate colors
    /// via Delta-E (CIE76) instead of farthest-point diversity sampling. This is well-suited for
    /// NeuQuant-style runs where a large pool is generated and similar neighbors should be merged.
    /// When <c>false</c> (default), uses a 5× candidate pool with farthest-point Lab sampling.
    /// </param>
    /// <param name="neuQuantOptions">
    /// NeuQuant-specific tuning. Only used when <paramref name="algorithm"/> is
    /// <see cref="PaletteAlgorithm.NeuQuant"/>. When <c>null</c> (default), options are
    /// auto-scaled from <paramref name="colorCount"/> via <see cref="NeuQuantOptions.ForColorCount"/>.
    /// </param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> entries, ordered by frequency.</returns>
    /// <exception cref="NotSupportedException">Thrown when the requested algorithm is not implemented.</exception>
    /// <exception cref="System.IO.IOException">Thrown when reading the source image fails.</exception>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(string source, int colorCount, bool isUrl,
        PaletteAlgorithm algorithm = PaletteAlgorithm.KMeans, bool dedupe = false,
        NeuQuantOptions? neuQuantOptions = null)
    {
        byte[] imageData;

        if (isUrl)
            imageData = await _httpClient.GetByteArrayAsync(source);
        else
            imageData = await File.ReadAllBytesAsync(source);

        if (!_colorPaletteExtractors.TryGetValue(algorithm, out var extractor))
            throw new NotSupportedException($"Algorithm: {algorithm} is not implemented");

        // For NeuQuant, build a correctly-scaled extractor per call rather than
        // reusing the default-constructed registry instance.
        if (algorithm == PaletteAlgorithm.NeuQuant)
        {
            var opts = neuQuantOptions ?? NeuQuantOptions.ForColorCount(colorCount);
            extractor = new NeuQuantColorExtractor(opts.NeuronCount, opts.TrainingIterations);
        }

        if (dedupe)
        {
            // Dedupe mode: extract a large candidate pool then drop perceptually similar neighbours.
            const int candidateMultiplier = 4;
            var candidates = await extractor.ExtractColorPaletteAsync(imageData, colorCount * candidateMultiplier);
            return PalettePostProcessing.RemoveNearDuplicateByDeltaE(candidates, minDeltaE: 12.0, maxCount: colorCount);
        }
        else
        {
            // Default mode: extract a large candidate pool then pick maximally diverse colors.
            const int candidateMultiplier = 5;
            var candidates = await extractor.ExtractColorPaletteAsync(imageData, colorCount * candidateMultiplier);
            return PalettePostProcessing.SelectDiverse(candidates, colorCount);
        }
    }
}