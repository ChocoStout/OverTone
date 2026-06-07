using OverTone.Processing;

namespace OverTone;

/// <summary>
/// Represents a single color in a generated palette, including its RGB components
/// and the number of pixels in the image that were assigned to this color cluster.
/// </summary>
public class ColorPalette
{
    /// <summary>
    /// Red channel value (0-255).
    /// </summary>
    public byte R { get; set; }

    /// <summary>
    /// Green channel value (0-255).
    /// </summary>
    public byte G { get; set; }

    /// <summary>
    /// Blue channel value (0-255).
    /// </summary>
    public byte B { get; set; }

    /// <summary>
    /// Number of pixels from the source image that were assigned to this color cluster.
    /// </summary>
    public int PixelCount { get; set; }

    /// <summary>
    /// Gets the color formatted as a hexadecimal string (#RRGGBB).
    /// </summary>
    public string AsHex => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>
    /// Gets the color as a 32-bit ARGB integer with an opaque alpha (<c>0xFFRRGGBB</c>), matching the
    /// <c>System.Drawing.Color.ToArgb()</c> layout. Convenient for interop with platform color types.
    /// </summary>
    public int ToArgb() => unchecked((int)(0xFF000000u | ((uint)R << 16) | ((uint)G << 8) | B));

    /// <summary>
    /// Converts the color to HSL — hue in degrees (0..360), saturation and lightness in 0..1.
    /// </summary>
    public (double H, double S, double L) ToHsl() => ColorMetrics.RgbToHsl(R, G, B);

    /// <summary>WCAG relative luminance of the color (0 = black, 1 = white).</summary>
    public double RelativeLuminance => ColorMetrics.RelativeLuminance(R, G, B);

    /// <summary>
    /// True when the color reads as "dark" — i.e. white text is more legible on it than black, using the
    /// WCAG luminance crossover (≈0.179). Handy for choosing a light-vs-dark theme from a dominant color.
    /// </summary>
    public bool IsDark => RelativeLuminance < 0.179129;
}