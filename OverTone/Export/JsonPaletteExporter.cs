using System.Text.Json;
using System.Text.Json.Nodes;
using OverTone;

namespace OverTone.Export;

/// <summary>
/// Exports a palette as structured, pretty-printed JSON. Each color always includes its hex, RGB,
/// HSL, and a nearest color name; when <see cref="PaletteExportOptions.IncludeMetadata"/> is enabled,
/// per-color pixel counts and percentages (plus a palette-level total) are included as well.
/// </summary>
public sealed class JsonPaletteExporter : IPaletteExporter
{
    /// <inheritdoc />
    public PaletteExportFormat Format => PaletteExportFormat.Json;

    /// <inheritdoc />
    public string FileExtension => "json";

    /// <inheritdoc />
    public string Export(IReadOnlyList<ColorPalette> palette, PaletteExportOptions options)
    {
        var totalPixels = palette.Sum(c => (long)c.PixelCount);

        var colors = new JsonArray();
        foreach (var c in palette)
        {
            var (h, s, l) = ExportFormatting.ToHsl(c);

            var entry = new JsonObject
            {
                ["hex"] = c.AsHex,
                ["rgb"] = new JsonObject { ["r"] = c.R, ["g"] = c.G, ["b"] = c.B },
                ["hsl"] = new JsonObject { ["h"] = h, ["s"] = s, ["l"] = l },
                ["name"] = ColorNaming.NearestName(c.R, c.G, c.B),
            };

            if (options.IncludeMetadata)
            {
                entry["pixelCount"] = c.PixelCount;
                entry["percentage"] = ExportFormatting.Percentage(c, totalPixels);
            }

            colors.Add(entry);
        }

        var root = new JsonObject
        {
            ["name"] = options.PaletteName,
            ["colorCount"] = palette.Count,
        };

        if (options.IncludeMetadata)
            root["totalPixels"] = totalPixels;

        root["colors"] = colors;

        // System.Text.Json writes numbers using the invariant culture, so output is locale-stable.
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
