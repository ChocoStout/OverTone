namespace OverTone;

/// <summary>
/// A decoded image: a tightly-packed RGBA pixel buffer plus its dimensions. Spatial extractors need
/// the width and height to map a pixel index back to its <c>(x, y)</c> position — information the old
/// color-space pipeline discarded.
/// </summary>
/// <param name="Rgba">Row-major RGBA bytes, 4 per pixel (length = <see cref="Width"/> × <see cref="Height"/> × 4).</param>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
public readonly record struct DecodedImage(byte[] Rgba, int Width, int Height)
{
    /// <summary>Total pixel count (<see cref="Width"/> × <see cref="Height"/>).</summary>
    public int PixelCount => Width * Height;
}
