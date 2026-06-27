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
    public static double EuclideanRgbDistance(ColorPalette a, ColorPalette b) => EuclideanRgbDistance(a.R, a.G, a.B, b.R, b.G, b.B);

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

    /// <summary>Converts an sRGB color to OkLCh — lightness, chroma, and hue in degrees (0..360).</summary>
    public static (double L, double C, double h) RgbToOkLch(byte r, byte g, byte b)
    {
        var (l, a, bb) = RgbToOkLab(r, g, b);
        var c = Math.Sqrt(a * a + bb * bb);
        var h = Math.Atan2(bb, a) * 180.0 / Math.PI;
        if (h < 0) h += 360.0;
        return (l, c, h);
    }

    /// <summary>
    /// Converts OkLCh back to an sRGB color, gamut-mapping by reducing chroma (holding lightness and hue)
    /// until the color fits sRGB — so synthesized colors keep their hue and lightness instead of
    /// distorting, which a naive per-channel clamp would cause.
    /// </summary>
    public static (byte R, byte G, byte B) OkLchToRgb(double l, double c, double h)
    {
        c = GamutMapChroma(l, c, h);
        var hr = h * Math.PI / 180.0;
        return OkLabToRgb(l, c * Math.Cos(hr), c * Math.Sin(hr));
    }

    /// <summary>
    /// Returns the largest chroma ≤ <paramref name="c"/> that keeps the color <c>(L, ·, h)</c> inside the
    /// sRGB gamut, via binary search. Hue and lightness are preserved exactly; only colorfulness yields.
    /// </summary>
    public static double GamutMapChroma(double l, double c, double h)
    {
        var hr = h * Math.PI / 180.0;
        double cos = Math.Cos(hr), sin = Math.Sin(hr);
        if (InGamut(l, c * cos, c * sin)) return c;

        double lo = 0.0, hi = c;
        for (var i = 0; i < 24; i++)
        {
            var mid = (lo + hi) / 2.0;
            if (InGamut(l, mid * cos, mid * sin)) lo = mid; else hi = mid;
        }
        return lo;
    }

    /// <summary>Converts an OkLab value to an sRGB color (each channel clamped into range).</summary>
    public static (byte R, byte G, byte B) OkLabToRgb(double l, double a, double b)
    {
        var (lr, lg, lb) = OkLabToLinear(l, a, b);
        return (LinearToSrgbByte(lr), LinearToSrgbByte(lg), LinearToSrgbByte(lb));
    }

    /// <summary>WCAG relative luminance (0..1) of an sRGB color.</summary>
    public static double RelativeLuminance(byte r, byte g, byte b)
        => 0.2126 * SrgbByteToLinear(r) + 0.7152 * SrgbByteToLinear(g) + 0.0722 * SrgbByteToLinear(b);

    /// <summary>WCAG contrast ratio (1..21) between two sRGB colors.</summary>
    public static double ContrastRatio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var l1 = RelativeLuminance(r1, g1, b1);
        var l2 = RelativeLuminance(r2, g2, b2);
        var hi = Math.Max(l1, l2);
        var lo = Math.Min(l1, l2);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>Returns black or white — whichever has the higher WCAG contrast against the background.</summary>
    public static (byte R, byte G, byte B) BestOnColor(byte r, byte g, byte b)
        => ContrastRatio(255, 255, 255, r, g, b) >= ContrastRatio(0, 0, 0, r, g, b)
            ? ((byte)255, (byte)255, (byte)255)
            : ((byte)0, (byte)0, (byte)0);

    /// <summary>
    /// Adjusts a foreground color's lightness (holding its hue and chroma) until it meets a target WCAG
    /// contrast ratio against the background. Contrast versus lightness is <em>V-shaped</em> against
    /// mid-tone backgrounds, so both directions are searched and the lightness nearest the original wins;
    /// if neither side reaches the target within the gamut, falls back to <see cref="BestOnColor"/>.
    /// </summary>
    public static (byte R, byte G, byte B) EnsureContrast(
        byte fr, byte fg, byte fb, byte br, byte bg, byte bb, double target)
    {
        var (l0, c, h) = RgbToOkLch(fr, fg, fb);

        double? up = null, down = null;
        for (var l = l0; l <= 1.0; l += 0.01)
        {
            var (r, g, b) = OkLchToRgb(l, c, h);
            if (ContrastRatio(r, g, b, br, bg, bb) >= target) { up = l; break; }
        }
        for (var l = l0; l >= 0.0; l -= 0.01)
        {
            var (r, g, b) = OkLchToRgb(l, c, h);
            if (ContrastRatio(r, g, b, br, bg, bb) >= target) { down = l; break; }
        }

        double chosen;
        if (up is { } u && down is { } d) chosen = u - l0 <= l0 - d ? u : d;
        else if (up is { } u2) chosen = u2;
        else if (down is { } d2) chosen = d2;
        else return BestOnColor(br, bg, bb);

        return OkLchToRgb(chosen, c, h);
    }

    /// <summary>
    /// Converts an sRGB color to HSL — hue in degrees (0..360), saturation and lightness in 0..1.
    /// </summary>
    public static (double H, double S, double L) RgbToHsl(byte r8, byte g8, byte b8)
    {
        var r = r8 / 255.0;
        var g = g8 / 255.0;
        var b = b8 / 255.0;

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

        return (h * 360.0, s, l);
    }

    /// <summary>
    /// Converts an HSL color back to sRGB — the inverse of <see cref="RgbToHsl"/>. Hue is in degrees
    /// (any value; normalized into 0..360), saturation and lightness in 0..1 (clamped). Channels are
    /// rounded to the nearest byte, so <c>RgbToHsl ∘ HslToRgb</c> round-trips a color to within ±1.
    /// </summary>
    public static (byte R, byte G, byte B) HslToRgb(double h, double s, double l)
    {
        s = Math.Clamp(s, 0.0, 1.0);
        l = Math.Clamp(l, 0.0, 1.0);

        h %= 360.0;
        if (h < 0) h += 360.0;

        var c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        var hp = h / 60.0;
        var x = c * (1.0 - Math.Abs(hp % 2.0 - 1.0));

        double r1, g1, b1;
        switch ((int)hp)
        {
            case 0: (r1, g1, b1) = (c, x, 0.0); break;
            case 1: (r1, g1, b1) = (x, c, 0.0); break;
            case 2: (r1, g1, b1) = (0.0, c, x); break;
            case 3: (r1, g1, b1) = (0.0, x, c); break;
            case 4: (r1, g1, b1) = (x, 0.0, c); break;
            default: (r1, g1, b1) = (c, 0.0, x); break; // sextant 5 (and the h==360→0 edge)
        }

        var m = l - c / 2.0;
        return (Channel(r1 + m), Channel(g1 + m), Channel(b1 + m));

        static byte Channel(double v) => (byte)Math.Round(Math.Clamp(v, 0.0, 1.0) * 255.0);
    }

    /// <summary>
    /// Smallest angular distance between two hues, in degrees (0..180). Wraps around the color wheel, so
    /// 350° and 10° are 20° apart, not 340°.
    /// </summary>
    public static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b) % 360.0;
        return d > 180.0 ? 360.0 - d : d;
    }

    /// <summary>
    /// HSL "chroma" (colorfulness): <c>(1 - |2L - 1|) · S</c>, in 0..1. A better "is this color vivid"
    /// proxy than raw saturation because it down-weights both pale pastels (high L) and near-blacks
    /// (low L), which can carry a misleadingly high saturation. Near 0 for neutrals, ~1 for pure hues.
    /// </summary>
    public static double HslChroma(double s, double l) => (1.0 - Math.Abs(2.0 * l - 1.0)) * s;

    /// <summary>
    /// Linearly interpolates between two sRGB colors in OkLab space, returning the blend at
    /// <paramref name="t"/> (clamped to 0..1; <c>0</c> = the first color, <c>1</c> = the second).
    /// Interpolating in OkLab keeps the path perceptually even and avoids the muddy mid-tones and hue
    /// skew of a naive per-channel sRGB blend — ideal for cross-fading UI colors between images.
    /// </summary>
    public static (byte R, byte G, byte B) LerpOkLab(
        byte r1, byte g1, byte b1, byte r2, byte g2, byte b2, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        var (l1, a1, bb1) = RgbToOkLab(r1, g1, b1);
        var (l2, a2, bb2) = RgbToOkLab(r2, g2, b2);
        return OkLabToRgb(
            l1 + (l2 - l1) * t,
            a1 + (a2 - a1) * t,
            bb1 + (bb2 - bb1) * t);
    }

    /// <summary>
    /// Converts an OkLab value to linear RGB (0..1, unclamped) using Ottosson's inverse matrices.
    /// </summary>
    private static (double R, double G, double B) OkLabToLinear(double l, double a, double b)
    {
        var lp = l + 0.3963377774 * a + 0.2158037573 * b;
        var mp = l - 0.1055613458 * a - 0.0638541728 * b;
        var sp = l - 0.0894841775 * a - 1.2914855480 * b;

        var lc = lp * lp * lp;
        var mc = mp * mp * mp;
        var sc = sp * sp * sp;

        return (
            4.0767416621 * lc - 3.3077115913 * mc + 0.2309699292 * sc,
            -1.2684380046 * lc + 2.6097574011 * mc - 0.3413193965 * sc,
            -0.0041960863 * lc - 0.7034186147 * mc + 1.7076147010 * sc);
    }

    /// <summary>True when the OkLab value lies inside the sRGB gamut (all linear channels in [0,1]).</summary>
    private static bool InGamut(double l, double a, double b)
    {
        var (lr, lg, lb) = OkLabToLinear(l, a, b);
        const double eps = 1e-4;
        return lr >= -eps && lr <= 1 + eps
            && lg >= -eps && lg <= 1 + eps
            && lb >= -eps && lb <= 1 + eps;
    }

    /// <summary>
    /// Converts an sRGB byte component (0..255) to linear RGB (0..1).
    /// </summary>
    private static double SrgbByteToLinear(byte v)
    {
        var s = v / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    /// <summary>Converts a linear-RGB channel (0..1) to an sRGB byte (0..255), clamped into range.</summary>
    private static byte LinearToSrgbByte(double c)
    {
        c = Math.Clamp(c, 0.0, 1.0);
        var s = c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
        return (byte)Math.Round(Math.Clamp(s, 0.0, 1.0) * 255.0);
    }
}
