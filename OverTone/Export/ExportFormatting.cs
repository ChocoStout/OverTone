using OverTone;

namespace OverTone.Export;

/// <summary>
/// Internal formatting helpers shared by the palette exporters: color-space conversion,
/// percentage math, nearest-name lookup, and C-identifier sanitization. Keeping this logic
/// here lets each <see cref="IPaletteExporter"/> implementation stay small.
/// </summary>
internal static class ExportFormatting
{
    /// <summary>
    /// Converts an RGB color to HSL. Hue is returned in degrees (0–360), saturation and lightness
    /// as percentages (0–100), each rounded to the nearest integer.
    /// </summary>
    public static (int H, int S, int L) ToHsl(ColorPalette color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;

        double h = 0, s = 0;
        if (max > min)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == r)
                h = (g - b) / d + (g < b ? 6.0 : 0.0);
            else if (max == g)
                h = (b - r) / d + 2.0;
            else
                h = (r - g) / d + 4.0;

            h /= 6.0;
        }

        return ((int)Math.Round(h * 360.0), (int)Math.Round(s * 100.0), (int)Math.Round(l * 100.0));
    }

    /// <summary>
    /// Returns the share of <paramref name="totalPixels"/> represented by the given color, as a
    /// percentage rounded to two decimal places. Returns 0 when the total is non-positive.
    /// </summary>
    public static double Percentage(ColorPalette color, long totalPixels) =>
        totalPixels > 0 ? Math.Round(color.PixelCount * 100.0 / totalPixels, 2) : 0.0;

    /// <summary>
    /// Converts an arbitrary name into an upper-snake-case C identifier
    /// (for example, <c>"My Palette!"</c> becomes <c>"MY_PALETTE"</c>). Falls back to
    /// <paramref name="fallback"/> when the input contains no usable characters.
    /// </summary>
    public static string ToUpperSnakeIdentifier(string name, string fallback)
    {
        var chars = new char[name.Length + 1];
        var len = 0;
        var pendingSeparator = false;

        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSeparator && len > 0)
                    chars[len++] = '_';
                chars[len++] = char.ToUpperInvariant(ch);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        if (len == 0)
            return fallback;

        // A C identifier cannot start with a digit.
        var result = new string(chars, 0, len);
        return char.IsDigit(result[0]) ? "_" + result : result;
    }
}
