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
    /// The algorithm identifier implemented by this extractor.
    /// </summary>
    PaletteAlgorithm Algorithm { get; }
}