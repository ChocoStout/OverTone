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
    private readonly Dictionary<PaletteAlgorithm, IColorPaletteExtractor> _colorPaletteExtractors;

    /// <summary>
    /// Creates a generator backed by the built-in extractors (see <see cref="DefaultExtractors"/>).
    /// For dependency injection, prefer the <see cref="PaletteGenerator(IEnumerable{IColorPaletteExtractor})"/>
    /// overload, or call <c>AddOverTone()</c> from the OverTone.Extensions.DependencyInjection package.
    /// </summary>
    public PaletteGenerator() : this(DefaultExtractors())
    {
    }

    /// <summary>
    /// Creates a generator from an explicit set of extractors (e.g. resolved by a DI container).
    /// When two extractors report the same <see cref="PaletteAlgorithm"/>, the first one wins.
    /// </summary>
    /// <param name="extractors">The extractors to make available.</param>
    public PaletteGenerator(IEnumerable<IColorPaletteExtractor> extractors)
    {
        _colorPaletteExtractors = new Dictionary<PaletteAlgorithm, IColorPaletteExtractor>();
        foreach (var extractor in extractors)
            _colorPaletteExtractors.TryAdd(extractor.Algorithm, extractor);
    }

    /// <summary>
    /// Creates a fresh instance of every built-in extractor, in a stable order. Useful for wiring up
    /// the non-DI path or registering the defaults explicitly.
    /// </summary>
    public static IReadOnlyList<IColorPaletteExtractor> DefaultExtractors() =>
    [
        new SlicColorExtractor(),
        new SpatialKMeansColorExtractor(),
        new KMeansColorExtractor(),
        new MedianCutColorExtractor(),
        new OctreeColorExtractor(),
        new FuzzyCMeansColorExtractor(),
        new PopularityColorExtractor(),
        new WuColorExtractor(),
        new NeuQuantColorExtractor(),
    ];

    /// <summary>
    /// Extracts a list of dominant colors from the given image source.
    /// </summary>
    /// <param name="source">A file path or URL to the image.</param>
    /// <param name="colorCount">The number of colors to return in the palette.</param>
    /// <param name="isUrl">True when <paramref name="source"/> is a URL; false when it is a local file path.</param>
    /// <param name="algorithm">The clustering algorithm to use for extraction.</param>
    /// <param name="selection">
    /// How to narrow the candidate pool into the final palette. <see cref="PaletteSelectionMode.Diverse"/>
    /// (default) spreads colors across the image's chromatic range via farthest-point Lab sampling;
    /// <see cref="PaletteSelectionMode.Dominant"/> keeps the most frequent colors with perceptual
    /// near-duplicates merged.
    /// </param>
    /// <param name="neuQuantOptions">
    /// NeuQuant-specific tuning. Only used when <paramref name="algorithm"/> is
    /// <see cref="PaletteAlgorithm.NeuQuant"/>. When <c>null</c> (default), options are
    /// auto-scaled from <paramref name="colorCount"/> via <see cref="NeuQuantOptions.ForColorCount"/>.
    /// </param>
    /// <param name="candidatePoolMultiplier">
    /// How many candidates to extract per requested color (<c>colorCount × multiplier</c>) before
    /// narrowing. A larger pool surfaces more minority colors at some cost in time. When <c>null</c>,
    /// a sensible per-mode default is used (5× for Diverse, 4× for Dominant).
    /// </param>
    /// <param name="minDeltaE">
    /// Minimum perceptual distance (CIE76 Delta-E) between colors kept by
    /// <see cref="PaletteSelectionMode.Dominant"/>. Larger values merge similar colors more aggressively.
    /// </param>
    /// <param name="maxDegreeOfParallelism">
    /// Maximum worker threads for parallel extraction (currently honored by K-Means). 1 (default) runs
    /// sequentially; larger values parallelize the work and produce identical palettes, just faster.
    /// </param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> entries, ordered by frequency.</returns>
    /// <exception cref="NotSupportedException">Thrown when the requested algorithm is not implemented.</exception>
    /// <exception cref="System.IO.IOException">Thrown when reading the source image fails.</exception>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(string source, int colorCount, bool isUrl = false,
        PaletteAlgorithm algorithm = PaletteAlgorithm.KMeans,
        PaletteSelectionMode selection = PaletteSelectionMode.Diverse,
        NeuQuantOptions? neuQuantOptions = null,
        int? candidatePoolMultiplier = null,
        double minDeltaE = 12.0,
        int maxDegreeOfParallelism = 1)
    {
        var imageData = isUrl
            ? await _httpClient.GetByteArrayAsync(source)
            : await File.ReadAllBytesAsync(source);

        return await ExtractColorPaletteAsync(imageData, colorCount, algorithm, selection,
            neuQuantOptions, candidatePoolMultiplier, minDeltaE, maxDegreeOfParallelism);
    }

    /// <summary>
    /// Extracts a list of dominant colors from already-loaded image bytes. Use this overload when you
    /// already hold the image in memory (album art, a decoded video frame, an uploaded file) to avoid a
    /// redundant read.
    /// </summary>
    /// <param name="imageData">The raw, encoded image bytes (PNG, JPEG, BMP, …).</param>
    /// <param name="colorCount">The number of colors to return in the palette.</param>
    /// <param name="algorithm">The clustering algorithm to use for extraction.</param>
    /// <param name="selection">How to narrow the candidate pool into the final palette.</param>
    /// <param name="neuQuantOptions">NeuQuant-specific tuning; <c>null</c> auto-scales from <paramref name="colorCount"/>.</param>
    /// <param name="candidatePoolMultiplier">Candidates per color before narrowing; <c>null</c> uses a per-mode default.</param>
    /// <param name="minDeltaE">Minimum CIE76 Delta-E between colors kept by <see cref="PaletteSelectionMode.Dominant"/>.</param>
    /// <param name="maxDegreeOfParallelism">Maximum worker threads (honored by K-Means); 1 = sequential. Larger values yield identical palettes, just faster.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> entries, ordered by frequency.</returns>
    /// <exception cref="NotSupportedException">Thrown when the requested algorithm is not implemented.</exception>
    /// <exception cref="UnsupportedImageFormatException">Thrown when the data is not a recognized image.</exception>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount,
        PaletteAlgorithm algorithm = PaletteAlgorithm.KMeans,
        PaletteSelectionMode selection = PaletteSelectionMode.Diverse,
        NeuQuantOptions? neuQuantOptions = null,
        int? candidatePoolMultiplier = null,
        double minDeltaE = 12.0,
        int maxDegreeOfParallelism = 1)
    {
        // Reject anything that isn't a recognized image before the decoder touches it.
        ImageValidation.EnsureSupportedImage(imageData);

        if (!_colorPaletteExtractors.TryGetValue(algorithm, out var extractor))
            throw new NotSupportedException($"Algorithm: {algorithm} is not implemented");

        // For NeuQuant, build a correctly-scaled extractor per call rather than
        // reusing the default-constructed registry instance.
        if (algorithm == PaletteAlgorithm.NeuQuant)
        {
            var opts = neuQuantOptions ?? NeuQuantOptions.ForColorCount(colorCount);
            extractor = new NeuQuantColorExtractor(opts);
        }

        // Each mode pulls a larger candidate pool, then narrows it. Dominant keeps the most frequent
        // colors with near-duplicates merged; Diverse spreads picks across the chromatic range.
        if (selection == PaletteSelectionMode.Dominant)
        {
            var multiplier = Math.Max(1, candidatePoolMultiplier ?? 4);
            var candidates = await extractor.ExtractColorPaletteAsync(imageData, colorCount * multiplier, maxDegreeOfParallelism);
            return PalettePostProcessing.RemoveNearDuplicateByDeltaE(candidates, minDeltaE, maxCount: colorCount);
        }
        else if (selection == PaletteSelectionMode.Salient)
        {
            var multiplier = Math.Max(1, candidatePoolMultiplier ?? 5);
            var candidates = await extractor.ExtractColorPaletteAsync(imageData, colorCount * multiplier, maxDegreeOfParallelism);
            return PalettePostProcessing.SelectSalient(candidates, colorCount, minDeltaE);
        }
        else
        {
            var multiplier = Math.Max(1, candidatePoolMultiplier ?? 5);
            var candidates = await extractor.ExtractColorPaletteAsync(imageData, colorCount * multiplier, maxDegreeOfParallelism);
            return PalettePostProcessing.SelectDiverse(candidates, colorCount);
        }
    }
}