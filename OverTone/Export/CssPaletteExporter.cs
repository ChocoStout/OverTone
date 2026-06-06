using System.Text;
using OverTone;

namespace OverTone.Export;

/// <summary>
/// Exports a palette as CSS custom properties inside a <c>:root</c> block, named
/// <c>--{prefix}-1</c>, <c>--{prefix}-2</c>, … using <see cref="PaletteExportOptions.Prefix"/>.
/// </summary>
public sealed class CssPaletteExporter : IPaletteExporter
{
    /// <inheritdoc />
    public PaletteExportFormat Format => PaletteExportFormat.Css;

    /// <inheritdoc />
    public string FileExtension => "css";

    /// <inheritdoc />
    public string Export(IReadOnlyList<ColorPalette> palette, PaletteExportOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/* {options.PaletteName} — {palette.Count} color{(palette.Count == 1 ? "" : "s")} */");
        sb.AppendLine(":root {");
        for (var i = 0; i < palette.Count; i++)
            sb.AppendLine($"    --{options.Prefix}-{i + 1}: {palette[i].AsHex};");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
