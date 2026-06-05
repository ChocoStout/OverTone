using OverTone;

namespace OverTone.Processing;

/// <summary>
/// Utility helpers for measuring distances between colors.
/// Centralizes color distance calculations so they can be reused across the library.
/// </summary>
public static class ColorMetrics
{
    /// <summary>
    /// Computes the Euclidean distance between two colors in RGB space.
    /// </summary>
    /// <param name="a">First color.</param>
    /// <param name="b">Second color.</param>
    /// <returns>Euclidean distance (0..~441.67).</returns>
    public static double EuclideanRgbDistance(ColorPalette a, ColorPalette b)
    {
        return EuclideanRgbDistance(a.R, a.G, a.B, b.R, b.G, b.B);
    }

    /// <summary>
    /// Computes the Euclidean distance between two RGB triplets.
    /// </summary>
    public static double EuclideanRgbDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var dr = r1 - r2;
        var dg = g1 - g2;
        var db = b1 - b2;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>
    /// Computes the squared Euclidean distance between two colors (avoids the sqrt when only comparisons are needed).
    /// </summary>
    public static double EuclideanRgbDistanceSquared(ColorPalette a, ColorPalette b)
        => EuclideanRgbDistanceSquared(a.R, a.G, a.B, b.R, b.G, b.B);

    /// <summary>
    /// Computes the squared Euclidean distance between two RGB triplets.
    /// </summary>
    public static double EuclideanRgbDistanceSquared(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var dr = r1 - r2;
        var dg = g1 - g2;
        var db = b1 - b2;
        return dr * dr + dg * dg + db * db;
    }

    /// <summary>
    /// Converts an sRGB color to CIELAB (D65 white point). Used for perceptual distance
    /// calculations, which are far more uniform than raw RGB distance.
    /// </summary>
    public static (double L, double a, double b) RgbToLab(byte r8, byte g8, byte b8)
    {
        var r = SrgbByteToLinear(r8);
        var g = SrgbByteToLinear(g8);
        var b = SrgbByteToLinear(b8);

        // Linear RGB to XYZ (D65).
        var x = r * 0.4124564 + g * 0.3575761 + b * 0.1804375;
        var y = r * 0.2126729 + g * 0.7151522 + b * 0.0721750;
        var z = r * 0.0193339 + g * 0.1191920 + b * 0.9503041;

        // Normalize for the D65 white point.
        var xn = x / 0.95047;
        var yn = y / 1.00000;
        var zn = z / 1.08883;

        var fx = F(xn);
        var fy = F(yn);
        var fz = F(zn);

        var l = 116.0 * fy - 16.0;
        var a = 500.0 * (fx - fy);
        var bLab = 200.0 * (fy - fz);

        return (l, a, bLab);

        static double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : (7.787037 * t + 16.0 / 116.0);
    }

    /// <summary>
    /// Computes the CIE76 Delta-E (Euclidean distance in CIELAB) between two colors.
    /// </summary>
    public static double DeltaE76(ColorPalette a, ColorPalette b) =>
        DeltaE76(RgbToLab(a.R, a.G, a.B), RgbToLab(b.R, b.G, b.B));

    /// <summary>
    /// Computes the CIE76 Delta-E (Euclidean distance in CIELAB) between two Lab values.
    /// </summary>
    public static double DeltaE76((double L, double a, double b) lab1, (double L, double a, double b) lab2)
    {
        var dL = lab1.L - lab2.L;
        var da = lab1.a - lab2.a;
        var db = lab1.b - lab2.b;
        return Math.Sqrt(dL * dL + da * da + db * db);
    }

    /// <summary>
    /// Converts an sRGB color to the OkLab perceptual color space (Björn Ottosson, 2020). OkLab is a
    /// modern, closed-form space that is more perceptually uniform than CIELAB — especially across the
    /// blues — and cheap to compute, which makes it a good basis for perceptual de-duplication.
    /// </summary>
    public static (double L, double a, double b) RgbToOkLab(byte r8, byte g8, byte b8)
    {
        var r = SrgbByteToLinear(r8);
        var g = SrgbByteToLinear(g8);
        var b = SrgbByteToLinear(b8);

        var l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
        var m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
        var s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;

        var lc = Math.Cbrt(l);
        var mc = Math.Cbrt(m);
        var sc = Math.Cbrt(s);

        return (
            0.2104542553 * lc + 0.7936177850 * mc - 0.0040720468 * sc,
            1.9779984951 * lc - 2.4285922050 * mc + 0.4505937099 * sc,
            0.0259040371 * lc + 0.7827717662 * mc - 0.8086757660 * sc);
    }

    /// <summary>
    /// Computes the OkLab Delta-E (Euclidean distance in OkLab) between two colors. Note the scale is
    /// far smaller than CIE76 ΔE — a typical "distinct" threshold is ~0.04, not ~12.
    /// </summary>
    public static double DeltaEOk(ColorPalette a, ColorPalette b) =>
        DeltaEOk(RgbToOkLab(a.R, a.G, a.B), RgbToOkLab(b.R, b.G, b.B));

    /// <summary>Computes the OkLab Delta-E between two OkLab values.</summary>
    public static double DeltaEOk((double L, double a, double b) lab1, (double L, double a, double b) lab2)
    {
        var dL = lab1.L - lab2.L;
        var da = lab1.a - lab2.a;
        var db = lab1.b - lab2.b;
        return Math.Sqrt(dL * dL + da * da + db * db);
    }

    /// <summary>
    /// CIELAB chroma (colorfulness): a color's distance from the neutral axis, <c>sqrt(a² + b²)</c>.
    /// Near 0 for grays; ~90+ for vivid colors. Used to rank a small vivid region above a large dull one.
    /// </summary>
    public static double LabChroma(byte r, byte g, byte b)
    {
        var (_, a, bb) = RgbToLab(r, g, b);
        return Math.Sqrt(a * a + bb * bb);
    }

    /// <summary>
    /// Converts an sRGB byte component (0..255) to linear RGB (0..1).
    /// </summary>
    private static double SrgbByteToLinear(byte v)
    {
        var s = v / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
