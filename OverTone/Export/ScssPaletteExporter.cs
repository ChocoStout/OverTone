using System.Text;
using OverTone;

namespace OverTone.Export;

/// <summary>
/// Exports a palette as SCSS: one <c>${prefix}-N</c> variable per color (using
/// <see cref="PaletteExportOptions.Prefix"/>) followed by a <c>$palette</c> Sass list of all colors.
/// </summary>
public sealed class ScssPaletteExporter : IPaletteExporter
{
    /// <inheritdoc />
    public PaletteExportFormat Format => PaletteExportFormat.Scss;

    /// <inheritdoc />
    public string FileExtension => "scss";

    /// <inheritdoc />
    public string Export(IReadOnlyList<ColorPalette> palette, PaletteExportOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// {options.PaletteName} — {palette.Count} color{(palette.Count == 1 ? "" : "s")}");
        for (var i = 0; i < palette.Count; i++)
            sb.AppendLine($"${options.Prefix}-{i + 1}: {palette[i].AsHex};");

        sb.AppendLine();
        sb.AppendLine($"$palette: ({string.Join(", ", palette.Select(c => c.AsHex))});");
        return sb.ToString();
    }
}
