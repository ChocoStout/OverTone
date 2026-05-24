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
}