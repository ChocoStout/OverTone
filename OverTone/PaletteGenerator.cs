using OverTone.Algorithms;
using OverTone.Processing;
using OverTone.Theming;

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
        _colorPaletteExtractors = [];
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
    /// Maximum worker threads for parallel extraction (currently honored by the spatial extractors). 1 (default) runs
    /// sequentially; larger values parallelize the work and produce identical palettes, just faster.
    /// </param>
    /// <param name="cancellationToken">A token to observe; cancels the download and aborts extraction between iterations.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> entries, ordered by frequency.</returns>
    /// <exception cref="NotSupportedException">Thrown when the requested algorithm is not implemented.</exception>
    /// <exception cref="System.IO.IOException">Thrown when reading the source image fails.</exception>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(string source, int colorCount, bool isUrl = false,
        PaletteAlgorithm algorithm = PaletteAlgorithm.Slic,
        PaletteSelectionMode selection = PaletteSelectionMode.Diverse,
        int? candidatePoolMultiplier = null,
        double minDeltaE = 12.0,
        int maxDegreeOfParallelism = 1,
        CancellationToken cancellationToken = default)
    {
        var imageData = isUrl
            ? await _httpClient.GetByteArrayAsync(source, cancellationToken)
            : await File.ReadAllBytesAsync(source, cancellationToken);

        return await ExtractColorPaletteAsync(imageData, colorCount, algorithm, selection,
            candidatePoolMultiplier, minDeltaE, maxDegreeOfParallelism, cancellationToken);
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
    /// <param name="candidatePoolMultiplier">Candidates per color before narrowing; <c>null</c> uses a per-mode default.</param>
    /// <param name="minDeltaE">Minimum CIE76 Delta-E between colors kept by <see cref="PaletteSelectionMode.Dominant"/>.</param>
    /// <param name="maxDegreeOfParallelism">Maximum worker threads (honored by the spatial extractors); 1 = sequential. Larger values yield identical palettes, just faster.</param>
    /// <param name="cancellationToken">A token to observe; aborts extraction between iterations.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> entries, ordered by frequency.</returns>
    /// <exception cref="NotSupportedException">Thrown when the requested algorithm is not implemented.</exception>
    /// <exception cref="UnsupportedImageFormatException">Thrown when the data is not a recognized image.</exception>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount,
        PaletteAlgorithm algorithm = PaletteAlgorithm.Slic,
        PaletteSelectionMode selection = PaletteSelectionMode.Diverse,
        int? candidatePoolMultiplier = null,
        double minDeltaE = 12.0,
        int maxDegreeOfParallelism = 1,
        CancellationToken cancellationToken = default)
    {
        // Reject anything that isn't a recognized image before the decoder touches it.
        ImageValidation.EnsureSupportedImage(imageData);

        if (!_colorPaletteExtractors.TryGetValue(algorithm, out var extractor))
            throw new NotSupportedException($"Algorithm: {algorithm} is not implemented");

        // Each mode pulls a larger candidate pool, then narrows it. Dominant keeps the most frequent
        // colors with near-duplicates merged; Diverse spreads picks across the chromatic range.
        if (selection == PaletteSelectionMode.Dominant)
        {
            var multiplier = Math.Max(1, candidatePoolMultiplier ?? 4);
            var candidates = await extractor.ExtractColorPaletteAsync(imageData, colorCount * multiplier, maxDegreeOfParallelism, cancellationToken);
            return PalettePostProcessing.RemoveNearDuplicateByDeltaE(candidates, minDeltaE, maxCount: colorCount);
        }
        else if (selection == PaletteSelectionMode.Salient)
        {
            var multiplier = Math.Max(1, candidatePoolMultiplier ?? 5);
            var candidates = await extractor.ExtractColorPaletteAsync(imageData, colorCount * multiplier, maxDegreeOfParallelism, cancellationToken);
            return PalettePostProcessing.SelectSalient(candidates, colorCount, minDeltaE);
        }
        else
        {
            var multiplier = Math.Max(1, candidatePoolMultiplier ?? 5);
            var candidates = await extractor.ExtractColorPaletteAsync(imageData, colorCount * multiplier, maxDegreeOfParallelism, cancellationToken);
            return PalettePostProcessing.SelectDiverse(candidates, colorCount);
        }
    }

    /// <summary>
    /// The no-config entry point: "just give me the <paramref name="colorCount"/> main colors of this
    /// image." No algorithm, selection mode, or threshold to choose. Runs the region-aware segmentation
    /// pipeline with sensible defaults — SLIC superpixels merged into regions, each contributing its
    /// representative (peak) color — then groups perceptual look-alikes in OkLab, ranks by saliency
    /// (so a small vivid accent can beat a large dull region while a dominant neutral still shows), and
    /// finally reassigns every pixel to its nearest returned color so the sizes reflect true coverage.
    /// </summary>
    /// <param name="imageData">The raw, encoded image bytes (PNG, JPEG, BMP, …).</param>
    /// <param name="colorCount">How many colors to return.</param>
    /// <param name="maxDegreeOfParallelism">Maximum worker threads; 1 (default) runs sequentially.</param>
    /// <param name="cancellationToken">A token to observe; aborts extraction between iterations.</param>
    /// <returns>The main colors, ordered by image coverage (largest first).</returns>
    /// <exception cref="UnsupportedImageFormatException">Thrown when the data is not a recognized image.</exception>
    public async Task<List<ColorPalette>> GetColorsAsync(byte[] imageData, int colorCount = 6,
        int maxDegreeOfParallelism = 1, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(colorCount);
        ImageValidation.EnsureSupportedImage(imageData);

        var extractor = _colorPaletteExtractors.TryGetValue(PaletteAlgorithm.Slic, out var slic)
            ? slic
            : new SlicColorExtractor();

        // SLIC segments by its own superpixel budget (colorCount is not used for segmentation) and merges
        // adjacent same-color superpixels into regions, each carrying a representative (peak) color.
        var regions = await extractor.ExtractColorPaletteAsync(imageData, colorCount, maxDegreeOfParallelism, cancellationToken);

        // Distinct colors (OkLab), ranked by saliency, narrowed to the requested count.
        var distinct = PalettePostProcessing.RemoveNearDuplicateByOkLab(regions);
        double totalArea = distinct.Sum(c => (long)c.PixelCount);
        if (totalArea <= 0) totalArea = 1;
        var selected = distinct
            .OrderByDescending(c => PalettePostProcessing.Saliency(c, totalArea))
            .Take(colorCount)
            .ToList();

        // Honest coverage: reassign every visible pixel to its nearest returned color.
        return PaletteQuality.AssignCoverage(imageData, selected, maxDegreeOfParallelism, cancellationToken);
    }

    /// <summary>
    /// The no-config entry point for an image file or URL. See
    /// <see cref="GetColorsAsync(byte[], int, int, CancellationToken)"/> for the pipeline.
    /// </summary>
    /// <param name="source">A file path or URL to the image.</param>
    /// <param name="colorCount">How many colors to return.</param>
    /// <param name="isUrl">True when <paramref name="source"/> is a URL; false for a local file path.</param>
    /// <param name="maxDegreeOfParallelism">Maximum worker threads; 1 (default) runs sequentially.</param>
    /// <param name="cancellationToken">A token to observe; cancels the download and aborts extraction between iterations.</param>
    public async Task<List<ColorPalette>> GetColorsAsync(string source, int colorCount = 6, bool isUrl = false,
        int maxDegreeOfParallelism = 1, CancellationToken cancellationToken = default)
    {
        var imageData = isUrl
            ? await _httpClient.GetByteArrayAsync(source, cancellationToken)
            : await File.ReadAllBytesAsync(source, cancellationToken);

        return await GetColorsAsync(imageData, colorCount, maxDegreeOfParallelism, cancellationToken);
    }

    /// <summary>
    /// One call from image to a ready-to-use <see cref="ColorScheme"/>: extracts the main colors (see
    /// <see cref="GetColorsAsync(byte[], int, int, CancellationToken)"/>), seeds the theme from the most
    /// dominant color and offers the rest as accents, then synthesizes a WCAG-aware scheme — semantic
    /// roles, matching "on" colors, and (optionally) tonal ramps. This is the "harmonize" path: colors may
    /// be normalized into a usable range, unlike raw extraction which reports exactly what's in the pixels.
    /// </summary>
    /// <param name="imageData">The raw, encoded image bytes (PNG, JPEG, BMP, …).</param>
    /// <param name="options">Scheme tuning (mode, contrast target, harmony, ramps); <c>null</c> uses defaults.</param>
    /// <param name="colorCount">How many colors to extract as the seed + accent pool.</param>
    /// <param name="maxDegreeOfParallelism">Maximum worker threads; 1 (default) runs sequentially.</param>
    /// <param name="cancellationToken">A token to observe while extracting.</param>
    public async Task<ColorScheme> GetThemeAsync(byte[] imageData, SchemeOptions? options = null,
        int colorCount = 6, int maxDegreeOfParallelism = 1, CancellationToken cancellationToken = default)
    {
        var colors = await GetColorsAsync(imageData, colorCount, maxDegreeOfParallelism, cancellationToken);
        return colors.BuildScheme(options);
    }

    /// <summary>
    /// One call from an image file or URL to a <see cref="ColorScheme"/>. See
    /// <see cref="GetThemeAsync(byte[], SchemeOptions, int, int, CancellationToken)"/>.
    /// </summary>
    public async Task<ColorScheme> GetThemeAsync(string source, SchemeOptions? options = null,
        int colorCount = 6, bool isUrl = false, int maxDegreeOfParallelism = 1, CancellationToken cancellationToken = default)
    {
        var colors = await GetColorsAsync(source, colorCount, isUrl, maxDegreeOfParallelism, cancellationToken);
        return colors.BuildScheme(options);
    }

    /// <summary>
    /// One call from image to a matching light + dark <see cref="ThemePair"/>, seeded from the image's
    /// colors. See <see cref="GetThemeAsync(byte[], SchemeOptions, int, int, CancellationToken)"/>.
    /// </summary>
    public async Task<ThemePair> GetThemePairAsync(byte[] imageData, SchemeOptions? options = null,
        int colorCount = 6, int maxDegreeOfParallelism = 1, CancellationToken cancellationToken = default)
    {
        var colors = await GetColorsAsync(imageData, colorCount, maxDegreeOfParallelism, cancellationToken);
        return colors.BuildThemePair(options);
    }

    /// <summary>One call from an image file or URL to a matching light + dark <see cref="ThemePair"/>.</summary>
    public async Task<ThemePair> GetThemePairAsync(string source, SchemeOptions? options = null,
        int colorCount = 6, bool isUrl = false, int maxDegreeOfParallelism = 1, CancellationToken cancellationToken = default)
    {
        var colors = await GetColorsAsync(source, colorCount, isUrl, maxDegreeOfParallelism, cancellationToken);
        return colors.BuildThemePair(options);
    }
}