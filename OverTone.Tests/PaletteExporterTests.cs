using System.Globalization;
using System.Text.Json;
using Xunit;

namespace OverTone.Tests;

public class PaletteExporterTests
{
    private static readonly PaletteExporter Exporter = new();

    // Fixed fixture: navy-ish (100 px) + orange-ish (50 px) = 150 px total.
    private static List<ColorPalette> Fixture() =>
    [
        new() { R = 0x2B, G = 0x4F, B = 0x82, PixelCount = 100 },
        new() { R = 0xE8, G = 0xA2, B = 0x3C, PixelCount = 50 },
    ];

    [Fact]
    public void Discovers_all_six_formats()
    {
        var formats = Exporter.AvailableFormats;

        Assert.Equal(6, formats.Count);
        Assert.Contains(PaletteExportFormat.Json, formats);
        Assert.Contains(PaletteExportFormat.HexList, formats);
        Assert.Contains(PaletteExportFormat.CArray, formats);
        Assert.Contains(PaletteExportFormat.Css, formats);
        Assert.Contains(PaletteExportFormat.Scss, formats);
        Assert.Contains(PaletteExportFormat.Tailwind, formats);
    }

    [Theory]
    [InlineData(PaletteExportFormat.Json, "json")]
    [InlineData(PaletteExportFormat.HexList, "txt")]
    [InlineData(PaletteExportFormat.CArray, "h")]
    [InlineData(PaletteExportFormat.Css, "css")]
    [InlineData(PaletteExportFormat.Scss, "scss")]
    [InlineData(PaletteExportFormat.Tailwind, "js")]
    public void Reports_expected_file_extension(PaletteExportFormat format, string expected) =>
        Assert.Equal(expected, Exporter.GetFileExtension(format));

    [Fact]
    public void Json_has_expected_shape_and_values()
    {
        var json = Exporter.Export(Fixture(), PaletteExportFormat.Json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("OverTone Palette", root.GetProperty("name").GetString());
        Assert.Equal(2, root.GetProperty("colorCount").GetInt32());
        Assert.Equal(150, root.GetProperty("totalPixels").GetInt64());

        var colors = root.GetProperty("colors");
        Assert.Equal(2, colors.GetArrayLength());

        var first = colors[0];
        Assert.Equal("#2B4F82", first.GetProperty("hex").GetString());
        Assert.Equal(43, first.GetProperty("rgb").GetProperty("r").GetInt32());
        Assert.Equal(79, first.GetProperty("rgb").GetProperty("g").GetInt32());
        Assert.Equal(130, first.GetProperty("rgb").GetProperty("b").GetInt32());
        Assert.Equal(100, first.GetProperty("pixelCount").GetInt32());
        Assert.Equal(66.67, first.GetProperty("percentage").GetDouble(), 2);

        // HSL object and a non-empty color name are always present.
        Assert.True(first.GetProperty("hsl").TryGetProperty("l", out _));
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("name").GetString()));
    }

    [Fact]
    public void Json_without_metadata_omits_pixel_fields()
    {
        var json = Exporter.Export(Fixture(), PaletteExportFormat.Json,
            new PaletteExportOptions { IncludeMetadata = false });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("totalPixels", out _));

        var first = root.GetProperty("colors")[0];
        Assert.False(first.TryGetProperty("pixelCount", out _));
        Assert.False(first.TryGetProperty("percentage", out _));

        // Core color data is still present.
        Assert.Equal("#2B4F82", first.GetProperty("hex").GetString());
    }

    [Fact]
    public void HexList_is_one_hex_per_line()
    {
        var output = Exporter.Export(Fixture(), PaletteExportFormat.HexList);

        var lines = output.Split(Environment.NewLine);
        string[] expected = ["#2B4F82", "#E8A23C"];
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void CArray_contains_length_macro_and_padded_rows()
    {
        var output = Exporter.Export(Fixture(), PaletteExportFormat.CArray);

        Assert.Contains("#define OVERTONE_PALETTE_LEN 2", output);
        Assert.Contains("const uint8_t OVERTONE_PALETTE[OVERTONE_PALETTE_LEN][3]", output);
        Assert.Contains("{  43,  79, 130 }, // #2B4F82", output);
    }

    [Fact]
    public void Css_emits_root_custom_properties()
    {
        var output = Exporter.Export(Fixture(), PaletteExportFormat.Css);

        Assert.Contains(":root {", output);
        Assert.Contains("--color-1: #2B4F82;", output);
        Assert.Contains("--color-2: #E8A23C;", output);
    }

    [Fact]
    public void Scss_emits_variables_and_palette_list()
    {
        var output = Exporter.Export(Fixture(), PaletteExportFormat.Scss);

        Assert.Contains("$color-1: #2B4F82;", output);
        Assert.Contains("$palette: (#2B4F82, #E8A23C);", output);
    }

    [Fact]
    public void Tailwind_emits_color_keys()
    {
        var output = Exporter.Export(Fixture(), PaletteExportFormat.Tailwind);

        Assert.Contains("theme:", output);
        Assert.Contains("'color-1': '#2B4F82',", output);
    }

    [Fact]
    public void Prefix_option_renames_variables()
    {
        var output = Exporter.Export(Fixture(), PaletteExportFormat.Css,
            new PaletteExportOptions { Prefix = "brand" });

        Assert.Contains("--brand-1: #2B4F82;", output);
    }

    [Theory]
    [InlineData(PaletteExportFormat.Json)]
    [InlineData(PaletteExportFormat.Css)]
    public void Output_stays_culture_invariant(PaletteExportFormat format)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses ',' as its decimal separator; the output must not pick that up.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var output = Exporter.Export(Fixture(), format);

            Assert.Contains("#2B4F82", output);
            Assert.DoesNotContain("66,67", output);

            if (format == PaletteExportFormat.Json)
            {
                // Parsing would throw if a ',' decimal had leaked into the JSON.
                using var doc = JsonDocument.Parse(output);
                Assert.Equal(66.67,
                    doc.RootElement.GetProperty("colors")[0].GetProperty("percentage").GetDouble(), 2);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task ExportToFileAsync_writes_the_serialized_content()
    {
        var path = Path.Combine(Path.GetTempPath(), $"overtone-test-{Guid.NewGuid():N}.css");
        try
        {
            await Exporter.ExportToFileAsync(Fixture(), PaletteExportFormat.Css, path);

            var onDisk = await File.ReadAllTextAsync(path);
            Assert.Equal(Exporter.Export(Fixture(), PaletteExportFormat.Css), onDisk);
            Assert.Contains("--color-1: #2B4F82;", onDisk);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Unknown_format_throws_NotSupported() =>
        Assert.Throws<NotSupportedException>(() => Exporter.Export(Fixture(), (PaletteExportFormat)999));
}
