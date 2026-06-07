namespace OverTone;

/// <summary>
/// Defines the contract that image palette extractors must implement. Implementations
/// accept raw image bytes and return a list of dominant colors found in the image.
/// </summary>
public interface IColorPaletteExtractor
{
    /// <summary>
    /// Extracts a palette of dominant colors from the provided image data.
    /// </summary>
    /// <param name="imageData">The raw image bytes (for example, PNG or JPEG contents).</param>
    /// <param name="colorCount">The number of colors to include in the returned palette.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> items.</returns>
    Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount);

    /// <summary>
    /// Extracts a palette, optionally using multiple threads. The default implementation ignores
    /// <paramref name="maxDegreeOfParallelism"/> and runs the sequential overload — extractors that
    /// benefit from parallelism (e.g. K-Means) override this. Implementers only need to provide the
    /// two-argument overload above; overriding this one is optional.
    /// </summary>
    /// <param name="imageData">The raw image bytes (for example, PNG or JPEG contents).</param>
    /// <param name="colorCount">The number of colors to include in the returned palette.</param>
    /// <param name="maxDegreeOfParallelism">Maximum worker threads; values &lt;= 1 run sequentially.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> items.</returns>
    Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount, int maxDegreeOfParallelism)
        => ExtractColorPaletteAsync(imageData, colorCount);

    /// <summary>
    /// Extracts a palette, optionally in parallel, observing a <see cref="CancellationToken"/>. The default
    /// implementation ignores the token and runs the synchronous overload — built-in extractors override
    /// this to check the token between iterations, so a caller can abandon a long run (e.g. when the user
    /// skips to the next track). Implementers only need the two-argument overload; overriding this is optional.
    /// </summary>
    /// <param name="imageData">The raw image bytes (for example, PNG or JPEG contents).</param>
    /// <param name="colorCount">The number of colors to include in the returned palette.</param>
    /// <param name="maxDegreeOfParallelism">Maximum worker threads; values &lt;= 1 run sequentially.</param>
    /// <param name="cancellationToken">A token to observe while extracting.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> items.</returns>
    Task<List<ColorPalette>> ExtractColorPaletteAsync(
        byte[] imageData, int colorCount, int maxDegreeOfParallelism, CancellationToken cancellationToken)
        => ExtractColorPaletteAsync(imageData, colorCount, maxDegreeOfParallelism);

    /// <summary>
    /// The algorithm identifier implemented by this extractor.
    /// </summary>
    PaletteAlgorithm Algorithm { get; }
}