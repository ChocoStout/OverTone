using OverTone;

namespace OverTone.Processing;

public static class PalettePostProcessing
{
    /// <summary>
    /// Remove near-duplicate colors from a palette using Euclidean distance in RGB space.
    /// Preserves entries with higher pixel counts when duplicates are found.
    /// </summary>
    /// <param name="input">Candidate palette entries (ordered or unordered).</param>
    /// <param name="minRgbDistance">Minimum Euclidean RGB distance required between returned colors.</param>
    /// <returns>A list with near-duplicates removed.</returns>
    private static List<ColorPalette> RemoveNearDuplicateByRgb(List<ColorPalette> input, double minRgbDistance = 20.0)
    {
        var result = new List<ColorPalette>();

        foreach (var candidate in input.OrderByDescending(p => p.PixelCount))
        {
            var isDistinct = result.All(accepted => !(ColorMetrics.EuclideanRgbDistance(candidate, accepted) < minRgbDistance));

            if (isDistinct) 
                result.Add(candidate);

            if (result.Count == input.Count) 
                break;
        }

        return result;
    }

    /// <summary>
    /// Remove near-duplicate colors using perceptual Delta-E in Lab space (CIE76).
    /// Preserves the highest-count colors and returns up to <paramref name="maxCount"/> if provided.
    /// </summary>
    /// <param name="input">Candidate palette entries.</param>
    /// <param name="minDeltaE">Minimum Delta-E required between returned colors.</param>
    /// <param name="maxCount">Optional maximum number of colors to return.</param>
    public static List<ColorPalette> RemoveNearDuplicateByDeltaE(List<ColorPalette> input, double minDeltaE = 10.0, int? maxCount = null)
    {
        var result = new List<ColorPalette>();

        foreach (var candidate in input.OrderByDescending(p => p.PixelCount))
        {
            var labCandidate = ConvertRgbToLab(candidate.R, candidate.G, candidate.B);
            var isDistinct = result.Select(accepted => ConvertRgbToLab(accepted.R, accepted.G, accepted.B)).All(labAccepted => !(ComputeDeltaE(labCandidate, labAccepted) < minDeltaE));

            if (isDistinct) 
                result.Add(candidate);

            if (maxCount.HasValue && result.Count >= maxCount.Value) 
                break;
        }

        return result;
    }

    /// <summary>
    /// Selects <paramref name="count"/> perceptually diverse colors from <paramref name="candidates"/>
    /// using weighted farthest-point sampling in Lab space.
    ///
    /// The first color is the one with the highest pixel count (the dominant color).
    /// Each subsequent pick is the candidate that maximises the minimum Lab distance to
    /// all already-selected colors, breaking ties in favor of higher pixel counts.
    /// This ensures the final palette spans the chromatic range of the image rather than
    /// clustering around its most common neutral/dark tones.
    /// </summary>
    /// <param name="candidates">Pool of candidate colors to choose from.</param>
    /// <param name="count">Number of colors to return.</param>
    public static List<ColorPalette> SelectDiverse(List<ColorPalette> candidates, int count)
    {
        if (candidates.Count <= count)
            return [..candidates];

        // Pre-compute Lab for every candidate once.
        var labs = candidates
            .Select(c => ConvertRgbToLab(c.R, c.G, c.B))
            .ToArray();

        var selected  = new List<int>(count);
        var minDistTo = new double[candidates.Count];
        Array.Fill(minDistTo, double.MaxValue);

        // Seed: highest pixel-count color.
        var seed = 0;
        for (var i = 1; i < candidates.Count; i++)
            if (candidates[i].PixelCount > candidates[seed].PixelCount)
                seed = i;

        selected.Add(seed);

        // Update min-distances from the seed.
        for (var i = 0; i < candidates.Count; i++)
            minDistTo[i] = ComputeDeltaE(labs[seed], labs[i]);

        while (selected.Count < count)
        {
            // Pick the candidate that is farthest (in Lab) from all selected so far.
            var best     = -1;
            var bestDist = -1.0;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (selected.Contains(i)) continue;
                if (minDistTo[i] > bestDist ||
                    (minDistTo[i] == bestDist && candidates[i].PixelCount > candidates[best].PixelCount))
                {
                    bestDist = minDistTo[i];
                    best     = i;
                }
            }

            if (best == -1) break;

            selected.Add(best);

            // Update running min-distances using the newly added color.
            for (var i = 0; i < candidates.Count; i++)
            {
                var d = ComputeDeltaE(labs[best], labs[i]);
                if (d < minDistTo[i])
                    minDistTo[i] = d;
            }
        }

        return selected.Select(i => candidates[i]).ToList();
    }


    private static (double L, double a, double b) ConvertRgbToLab(byte r8, byte g8, byte b8)
    {
        var r = SrgbByteToLinear(r8);
        var g = SrgbByteToLinear(g8);
        var b = SrgbByteToLinear(b8);

        // Convert linear RGB to XYZ (D65)
        var x = r * 0.4124564 + g * 0.3575761 + b * 0.1804375;
        var y = r * 0.2126729 + g * 0.7151522 + b * 0.0721750;
        var z = r * 0.0193339 + g * 0.1191920 + b * 0.9503041;

        // Normalize for D65 white point
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

        double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : (7.787037 * t + 16.0 / 116.0);
    }

    /// <summary>
    /// Compute CIE76 Delta-E between two Lab values.
    /// </summary>
    private static double ComputeDeltaE((double L, double a, double b) lab1, (double L, double a, double b) lab2)
    {
        var dL = lab1.L - lab2.L;
        var da = lab1.a - lab2.a;
        var db = lab1.b - lab2.b;
        return Math.Sqrt(dL * dL + da * da + db * db);
    }

    /// <summary>
    /// Convert an sRGB byte component (0..255) to linear RGB (0..1).
    /// </summary>
    private static double SrgbByteToLinear(byte v)
    {
        var s = v / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
