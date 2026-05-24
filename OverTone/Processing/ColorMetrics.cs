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
}
