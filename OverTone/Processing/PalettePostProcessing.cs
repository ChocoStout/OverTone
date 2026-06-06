using OverTone;

namespace OverTone.Processing;

/// <summary>
/// Post-processing that narrows an extractor's raw candidate colors into a final palette: perceptual
/// near-duplicate removal (CIE76 or OkLab) and the selection strategies behind
/// <see cref="PaletteSelectionMode"/> — diverse (farthest-point) and salient (chroma × area).
/// </summary>
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
            var labCandidate = ColorMetrics.RgbToLab(candidate.R, candidate.G, candidate.B);
            var isDistinct = result
                .Select(accepted => ColorMetrics.RgbToLab(accepted.R, accepted.G, accepted.B))
                .All(labAccepted => !(ColorMetrics.DeltaE76(labCandidate, labAccepted) < minDeltaE));

            if (isDistinct)
                result.Add(candidate);

            if (maxCount.HasValue && result.Count >= maxCount.Value)
                break;
        }

        return result;
    }

    /// <summary>
    /// Remove near-duplicate colors using perceptual distance in OkLab space. OkLab is more uniform than
    /// CIELAB (especially in the blues), so this groups true look-alikes better — used to return
    /// <em>distinct</em> colors (not five shades of cream). Note the threshold scale: ~0.045, not ~12.
    /// </summary>
    /// <param name="input">Candidate palette entries.</param>
    /// <param name="minDistance">Minimum OkLab ΔE required between returned colors.</param>
    /// <param name="maxCount">Optional maximum number of colors to return.</param>
    public static List<ColorPalette> RemoveNearDuplicateByOkLab(List<ColorPalette> input, double minDistance = 0.045, int? maxCount = null)
    {
        var result = new List<ColorPalette>();

        foreach (var candidate in input.OrderByDescending(p => p.PixelCount))
        {
            var okCandidate = ColorMetrics.RgbToOkLab(candidate.R, candidate.G, candidate.B);
            var isDistinct = result
                .Select(accepted => ColorMetrics.RgbToOkLab(accepted.R, accepted.G, accepted.B))
                .All(okAccepted => ColorMetrics.DeltaEOk(okCandidate, okAccepted) >= minDistance);

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
            .Select(c => ColorMetrics.RgbToLab(c.R, c.G, c.B))
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
            minDistTo[i] = ColorMetrics.DeltaE76(labs[seed], labs[i]);

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

            // Stop if no candidate remains, or the farthest one is an exact perceptual duplicate of
            // an already-selected color (Lab distance 0). This happens when the image has fewer
            // distinct colors than requested — return fewer colors rather than emitting duplicates.
            if (best == -1 || bestDist <= 0.0) break;

            selected.Add(best);

            // Update running min-distances using the newly added color.
            for (var i = 0; i < candidates.Count; i++)
            {
                var d = ColorMetrics.DeltaE76(labs[best], labs[i]);
                if (d < minDistTo[i])
                    minDistTo[i] = d;
            }
        }

        return [.. selected.Select(i => candidates[i])];
    }

    /// <summary>
    /// Selects up to <paramref name="count"/> colors by <em>saliency</em> — a blend of chroma and area
    /// tuned so a small but vivid region (e.g. lips) can outrank a large dull one (e.g. sky), while a
    /// dominant neutral still surfaces. Candidates are ranked by saliency, then near-duplicates are
    /// dropped by perceptual ΔE. This is the selection that surfaces "the colors a person would name",
    /// and it relies on the extractor having emitted <em>representative</em> (peak) colors, not means.
    /// </summary>
    /// <param name="candidates">Candidate colors, each with a real <see cref="ColorPalette.PixelCount"/>.</param>
    /// <param name="count">Maximum number of colors to return.</param>
    /// <param name="minDeltaE">Minimum CIE76 ΔE required between returned colors.</param>
    /// <param name="chromaWeight">Exponent on normalized chroma (default 0.6).</param>
    /// <param name="areaWeight">Exponent on area fraction (default 0.5 — sub-linear, so huge regions saturate).</param>
    public static List<ColorPalette> SelectSalient(
        List<ColorPalette> candidates, int count,
        double minDeltaE = 12.0, double chromaWeight = 0.6, double areaWeight = 0.5)
    {
        if (candidates.Count == 0 || count <= 0)
            return [];

        double totalArea = candidates.Sum(c => (long)c.PixelCount);
        if (totalArea <= 0) totalArea = 1;

        var ranked = candidates
            .OrderByDescending(c => Saliency(c, totalArea, chromaWeight, areaWeight))
            .ThenByDescending(c => c.PixelCount)
            .ToList();

        var result = new List<ColorPalette>(Math.Min(count, ranked.Count));
        foreach (var candidate in ranked)
        {
            var labCandidate = ColorMetrics.RgbToLab(candidate.R, candidate.G, candidate.B);
            var distinct = result.All(accepted =>
                ColorMetrics.DeltaE76(labCandidate, ColorMetrics.RgbToLab(accepted.R, accepted.G, accepted.B)) >= minDeltaE);

            if (distinct)
                result.Add(candidate);

            if (result.Count >= count)
                break;
        }

        return result;
    }

    /// <summary>
    /// Saliency score: <c>(chromaNorm + ε)^chromaWeight · areaFrac^areaWeight + 0.15 · areaFrac</c>.
    /// The additive term is a neutral floor: a dominant achromatic region (black/white) still scores in
    /// proportion to its area even when its chroma is ~0 — without it, multiplying by zero chroma would
    /// erase neutrals from the palette entirely.
    /// </summary>
    public static double Saliency(ColorPalette c, double totalArea, double chromaWeight = 0.6, double areaWeight = 0.5)
    {
        var chromaNorm = Math.Clamp(ColorMetrics.LabChroma(c.R, c.G, c.B) / 90.0, 0.0, 1.0);
        var areaFrac = c.PixelCount / totalArea;
        return Math.Pow(chromaNorm + 0.05, chromaWeight) * Math.Pow(areaFrac, areaWeight) + 0.15 * areaFrac;
    }
}
