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

    /// <summary>
    /// Builds a <see cref="ColorPalette"/> from an HSL color — the inverse of <see cref="ToHsl"/>. Hue is
    /// in degrees (normalized into 0..360), saturation and lightness in 0..1. <paramref name="pixelCount"/>
    /// defaults to 0 since a synthesized color carries no extraction area.
    /// </summary>
    public static ColorPalette FromHsl(double h, double s, double l, int pixelCount = 0)
    {
        var (r, g, b) = ColorMetrics.HslToRgb(h, s, l);
        return new ColorPalette { R = r, G = g, B = b, PixelCount = pixelCount };
    }

    /// <summary>
    /// HSL "chroma" (colorfulness) of the color in 0..1 — <c>(1 - |2L - 1|) · S</c>. A vividness proxy that
    /// down-weights pale pastels and near-blacks better than raw saturation. See <see cref="ColorMetrics.HslChroma"/>.
    /// </summary>
    public double HslChroma
    {
        get
        {
            var (_, s, l) = ToHsl();
            return ColorMetrics.HslChroma(s, l);
        }
    }

    /// <summary>WCAG relative luminance of the color (0 = black, 1 = white).</summary>
    public double RelativeLuminance => ColorMetrics.RelativeLuminance(R, G, B);

    /// <summary>
    /// True when the color reads as "dark" — i.e. white text is more legible on it than black, using the
    /// WCAG luminance crossover (≈0.179). Handy for choosing a light-vs-dark theme from a dominant color.
    /// </summary>
    public bool IsDark => RelativeLuminance < 0.179129;
}