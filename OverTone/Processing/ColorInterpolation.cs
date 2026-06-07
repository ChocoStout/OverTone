using OverTone;

namespace OverTone.Processing;

/// <summary>
/// Perceptual interpolation between colors and palettes. Blends happen in OkLab (via
/// <see cref="ColorMetrics.LerpOkLab"/>), so a cross-fade stays even and on-hue — useful for smoothly
/// transitioning UI colors when the source image changes (e.g. a new track's album art).
/// </summary>
public static class ColorInterpolation
{
    /// <summary>
    /// Blends two colors at <paramref name="t"/> (0..1) in OkLab, returning a new <see cref="ColorPalette"/>.
    /// <see cref="ColorPalette.PixelCount"/> is linearly interpolated too, so a blended palette keeps
    /// plausible weights.
    /// </summary>
    public static ColorPalette Lerp(ColorPalette a, ColorPalette b, double t)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        t = Math.Clamp(t, 0.0, 1.0);

        var (r, g, bl) = ColorMetrics.LerpOkLab(a.R, a.G, a.B, b.R, b.G, b.B, t);
        return new ColorPalette
        {
            R = r,
            G = g,
            B = bl,
            PixelCount = (int)Math.Round(a.PixelCount + (b.PixelCount - a.PixelCount) * t),
        };
    }

    /// <summary>
    /// Blends two palettes pairwise by index at <paramref name="t"/> (0..1). The result has the length of
    /// the shorter input; pad beforehand if you need a fixed count. Both palettes are assumed to be ordered
    /// the same way (e.g. both by coverage), so index <c>i</c> in one corresponds to index <c>i</c> in the other.
    /// </summary>
    public static List<ColorPalette> Lerp(IReadOnlyList<ColorPalette> a, IReadOnlyList<ColorPalette> b, double t)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var n = Math.Min(a.Count, b.Count);
        var result = new List<ColorPalette>(n);
        for (var i = 0; i < n; i++)
            result.Add(Lerp(a[i], b[i], t));
        return result;
    }
}
