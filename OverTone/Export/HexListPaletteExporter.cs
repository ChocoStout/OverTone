using OverTone;

namespace OverTone.Export;

/// <summary>
/// Exports a palette as plain text with one <c>#RRGGBB</c> value per line. No header is written so
/// the output round-trips cleanly into other tools.
/// </summary>
public sealed class HexListPaletteExporter : IPaletteExporter
{
    /// <inheritdoc />
    public PaletteExportFormat Format => PaletteExportFormat.HexList;

    /// <inheritdoc />
    public string FileExtension => "txt";

    /// <inheritdoc />
    public string Export(IReadOnlyList<ColorPalette> palette, PaletteExportOptions options) =>
        string.Join(Environment.NewLine, palette.Select(c => c.AsHex));
}
