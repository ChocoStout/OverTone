using System.Text;
using OverTone;

namespace OverTone.Export;

/// <summary>
/// Exports a palette as a Tailwind CSS color config snippet ready to merge into
/// <c>tailwind.config.js</c> under <c>theme.extend.colors</c>. Keys are named
/// <c>{prefix}-1</c>, <c>{prefix}-2</c>, … using <see cref="PaletteExportOptions.Prefix"/>.
/// </summary>
public sealed class TailwindPaletteExporter : IPaletteExporter
{
    /// <inheritdoc />
    public PaletteExportFormat Format => PaletteExportFormat.Tailwind;

    /// <inheritdoc />
    public string FileExtension => "js";

    /// <inheritdoc />
    public string Export(IReadOnlyList<ColorPalette> palette, PaletteExportOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"/** {options.PaletteName} — merge into tailwind.config.js */");
        sb.AppendLine("module.exports = {");
        sb.AppendLine("    theme: {");
        sb.AppendLine("        extend: {");
        sb.AppendLine("            colors: {");
        for (var i = 0; i < palette.Count; i++)
            sb.AppendLine($"                '{options.Prefix}-{i + 1}': '{palette[i].AsHex}',");
        sb.AppendLine("            },");
        sb.AppendLine("        },");
        sb.AppendLine("    },");
        sb.AppendLine("};");
        return sb.ToString();
    }
}
