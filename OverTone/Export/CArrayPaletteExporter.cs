using System.Globalization;
using System.Text;
using OverTone;

namespace OverTone.Export;

/// <summary>
/// Exports a palette as a C/C++ header containing a <c>uint8_t[][3]</c> RGB array and a length macro.
/// Designed to drop straight into Arduino / FastLED sketches driving an LED strip. The array and
/// macro identifier are derived from <see cref="PaletteExportOptions.PaletteName"/>.
/// </summary>
public sealed class CArrayPaletteExporter : IPaletteExporter
{
    /// <inheritdoc />
    public PaletteExportFormat Format => PaletteExportFormat.CArray;

    /// <inheritdoc />
    public string FileExtension => "h";

    /// <inheritdoc />
    public string Export(IReadOnlyList<ColorPalette> palette, PaletteExportOptions options)
    {
        var id = ExportFormatting.ToUpperSnakeIdentifier(options.PaletteName, "PALETTE");

        var sb = new StringBuilder();
        sb.AppendLine($"// {options.PaletteName} — {palette.Count} color{Plural(palette.Count)}");
        sb.AppendLine($"#define {id}_LEN {palette.Count}");
        sb.AppendLine($"const uint8_t {id}[{id}_LEN][3] = {{");

        foreach (var c in palette)
        {
            var r = c.R.ToString(CultureInfo.InvariantCulture).PadLeft(3);
            var g = c.G.ToString(CultureInfo.InvariantCulture).PadLeft(3);
            var b = c.B.ToString(CultureInfo.InvariantCulture).PadLeft(3);
            sb.AppendLine($"    {{ {r}, {g}, {b} }}, // {c.AsHex}");
        }

        sb.AppendLine("};");
        return sb.ToString();
    }

    private static string Plural(int count) => count == 1 ? "" : "s";
}
