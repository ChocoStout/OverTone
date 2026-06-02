namespace OverTone;

/// <summary>
/// Defines the contract for serializing a generated palette into a specific text format.
/// Implementations are discovered automatically by <see cref="PaletteExporter"/> via reflection,
/// mirroring how <see cref="IColorPaletteExtractor"/> implementations are discovered by
/// <see cref="PaletteGenerator"/>.
/// </summary>
public interface IPaletteExporter
{
    /// <summary>
    /// The format identifier implemented by this exporter.
    /// </summary>
    PaletteExportFormat Format { get; }

    /// <summary>
    /// The file extension associated with this format, without a leading dot
    /// (for example, <c>"json"</c>, <c>"css"</c>, or <c>"h"</c>).
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Serializes the given palette into this exporter's text format.
    /// </summary>
    /// <param name="palette">The colors to serialize, ordered as they should appear in the output.</param>
    /// <param name="options">Settings controlling naming and metadata inclusion.</param>
    /// <returns>The formatted palette as a string.</returns>
    string Export(IReadOnlyList<ColorPalette> palette, PaletteExportOptions options);
}
