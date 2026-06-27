using OverTone.Processing;

namespace OverTone;

/// <summary>
/// One stop of a color ramp produced by <see cref="SpectrumBuilder"/>: an sRGB color paired with the
/// total cover-area (<see cref="Weight"/>) of the hue family it represents. A spectrum is an ordered
/// list of these, swept low→high hue, ready to drive a gradient or visualizer background.
/// </summary>
/// <param name="R">Red channel value (0-255).</param>
/// <param name="G">Green channel value (0-255).</param>
/// <param name="B">Blue channel value (0-255).</param>
/// <param name="Weight">
/// Summed <see cref="ColorPalette.PixelCount"/> of every color folded into this stop — its share of the
/// image's covered area. Folding keeps a hue family's total prominence from being double-counted.
/// </param>
public readonly record struct GradientStop(byte R, byte G, byte B, long Weight)
{
    /// <summary>Gets the color formatted as a hexadecimal string (#RRGGBB).</summary>
    public string AsHex => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Converts the stop color to HSL — hue in degrees (0..360), saturation and lightness in 0..1.</summary>
    public (double H, double S, double L) ToHsl() => ColorMetrics.RgbToHsl(R, G, B);
}
