using OverTone;

namespace OverTone.Processing;

/// <summary>
/// Turns an extracted palette into an ordered color ramp — a "spectrum" of <see cref="GradientStop"/>s
/// suitable for a gradient or dynamic background. Near-neutral colors are dropped, nearby hues are merged
/// into a single stop (folding their cover-areas together so a hue family is represented once, not
/// double-counted), the most prominent stops are kept, and the result is swept low→high hue.
/// Parallel in spirit to <see cref="OverTone.Theming.SchemeBuilder"/>, but for ramps instead of themes.
/// </summary>
public static class SpectrumBuilder
{
    /// <summary>The default minimum HSL chroma a color needs to count as vivid (below this it's "near-neutral").</summary>
    public const double DefaultChromaFloor = 0.10;

    /// <summary>The default hue window, in degrees, within which colors are merged into one stop.</summary>
    public const double DefaultHueMergeDegrees = 22.0;

    /// <summary>The default cap on the number of distinct stops returned.</summary>
    public const int DefaultMaxStops = 6;

    /// <summary>
    /// Builds an ordered spectrum from a palette (e.g. the output of palette extraction).
    /// </summary>
    /// <param name="palette">The extracted colors. Each carries R/G/B and a <see cref="ColorPalette.PixelCount"/>.</param>
    /// <param name="chromaFloor">
    /// Colors whose <see cref="ColorPalette.HslChroma"/> is below this (0..1) are dropped as near-neutral.
    /// Defaults to <see cref="DefaultChromaFloor"/> (~0.10).
    /// </param>
    /// <param name="hueMergeDegrees">
    /// Colors whose hues are within this many degrees of an already-formed stop are folded into it.
    /// Defaults to <see cref="DefaultHueMergeDegrees"/> (~22°).
    /// </param>
    /// <param name="maxStops">
    /// The maximum number of stops to return; the most prominent (largest summed area) are kept.
    /// Defaults to <see cref="DefaultMaxStops"/> (~6).
    /// </param>
    /// <returns>
    /// Stops ordered low→high hue. Each stop's color is the most prominent member of its hue family, and
    /// its <see cref="GradientStop.Weight"/> is the summed cover-area of the whole family. Empty when no
    /// color clears the chroma floor.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="palette"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxStops"/> is less than 1.</exception>
    public static IReadOnlyList<GradientStop> Build(
        IReadOnlyList<ColorPalette> palette,
        double chromaFloor = DefaultChromaFloor,
        double hueMergeDegrees = DefaultHueMergeDegrees,
        int maxStops = DefaultMaxStops)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStops, 1);

        // Greedily cluster the vivid colors by hue, processing the most prominent first so that each
        // cluster's representative color (and hue) is its highest-area member. Later members of the same
        // hue family only fold their area in — single-link to the representative keeps it deterministic.
        var clusters = new List<HueCluster>();
        foreach (var color in palette
                     .Where(c => c.HslChroma >= chromaFloor)
                     .OrderByDescending(c => c.PixelCount))
        {
            var (hue, _, _) = color.ToHsl();

            HueCluster? match = null;
            foreach (var cluster in clusters)
            {
                if (ColorMetrics.HueDistance(cluster.Hue, hue) <= hueMergeDegrees)
                {
                    match = cluster;
                    break;
                }
            }

            if (match is null)
                clusters.Add(new HueCluster(hue, color.R, color.G, color.B, color.PixelCount));
            else
                match.Weight += color.PixelCount;
        }

        // Keep the most prominent stops, then sweep the survivors low→high hue.
        return clusters
            .OrderByDescending(c => c.Weight)
            .Take(maxStops)
            .OrderBy(c => c.Hue)
            .Select(c => new GradientStop(c.R, c.G, c.B, c.Weight))
            .ToList();
    }

    /// <summary>A mutable hue family: a fixed representative color/hue plus an accumulating cover-area.</summary>
    private sealed class HueCluster(double hue, byte r, byte g, byte b, long weight)
    {
        public double Hue { get; } = hue;
        public byte R { get; } = r;
        public byte G { get; } = g;
        public byte B { get; } = b;
        public long Weight { get; set; } = weight;
    }
}
