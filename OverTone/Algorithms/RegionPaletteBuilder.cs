using OverTone;
using OverTone.Processing;

namespace OverTone.Algorithms;

/// <summary>
/// Turns a per-pixel segmentation labeling into a region-based palette. For SLIC it first merges
/// adjacent superpixels with near-identical color (a region-adjacency-graph union-find merge), so a
/// large object spanning many superpixels collapses to one region while a small vivid region stays a
/// single, high-saliency candidate. Each emitted color is the region's representative (peak) color and
/// its <see cref="ColorPalette.PixelCount"/> is the region's true visible-pixel area.
/// </summary>
internal static class RegionPaletteBuilder
{
    private const byte AlphaThreshold = 128;

    /// <summary>
    /// Builds a palette from a SLIC labeling over a <paramref name="width"/>×<paramref name="height"/>
    /// grid. Labels are <c>0..superpixelCount-1</c>, or <c>-1</c> for transparent/unassigned pixels.
    /// </summary>
    public static List<ColorPalette> FromSlicLabels(
        int[] labels, int superpixelCount, byte[] rgba, int width, int height, double mergeDeltaE)
    {
        // 1. Per-superpixel Lab sums + visible-pixel counts (the mean color is the merge key).
        var sumL = new double[superpixelCount];
        var sumA = new double[superpixelCount];
        var sumB = new double[superpixelCount];
        var count = new int[superpixelCount];

        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            if (label < 0 || rgba[i * 4 + 3] <= AlphaThreshold) continue;

            var (l, a, b) = ColorMetrics.RgbToLab(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]);
            sumL[label] += l;
            sumA[label] += a;
            sumB[label] += b;
            count[label]++;
        }

        // Union-find over superpixels; merged Lab sums + counts always live at the root.
        var parent = new int[superpixelCount];
        for (var i = 0; i < superpixelCount; i++) parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        (double, double, double) Mean(int root) => count[root] == 0
            ? (0.0, 0.0, 0.0)
            : (sumL[root] / count[root], sumA[root] / count[root], sumB[root] / count[root]);

        if (mergeDeltaE > 0)
        {
            // 2. Adjacency: scan each pixel's right + down neighbor; collect distinct superpixel pairs.
            var edges = new HashSet<long>();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var idx = y * width + x;
                    var la = labels[idx];
                    if (la < 0) continue;

                    if (x + 1 < width) AddEdge(edges, la, labels[idx + 1], superpixelCount);
                    if (y + 1 < height) AddEdge(edges, la, labels[idx + width], superpixelCount);
                }
            }

            // 3. Merge endpoints in ascending initial-ΔE order (deterministic; tie-break on key),
            //    each time re-checking the *current aggregate* mean colors against the threshold.
            var scored = new List<(double Delta, long Edge)>(edges.Count);
            foreach (var edge in edges)
            {
                var a = (int)(edge / superpixelCount);
                var b = (int)(edge % superpixelCount);
                var delta = count[a] == 0 || count[b] == 0
                    ? double.MaxValue
                    : ColorMetrics.DeltaE76(Mean(a), Mean(b));
                scored.Add((delta, edge));
            }

            scored.Sort((x, y) =>
            {
                var c = x.Delta.CompareTo(y.Delta);
                return c != 0 ? c : x.Edge.CompareTo(y.Edge);
            });

            foreach (var (_, edge) in scored)
            {
                var ra = Find((int)(edge / superpixelCount));
                var rb = Find((int)(edge % superpixelCount));
                if (ra == rb || count[ra] == 0 || count[rb] == 0) continue;

                if (ColorMetrics.DeltaE76(Mean(ra), Mean(rb)) < mergeDeltaE)
                {
                    // Merge the smaller region into the larger (stable roots), combining sums.
                    var (keep, drop) = count[ra] >= count[rb] ? (ra, rb) : (rb, ra);
                    parent[drop] = keep;
                    sumL[keep] += sumL[drop];
                    sumA[keep] += sumA[drop];
                    sumB[keep] += sumB[drop];
                    count[keep] += count[drop];
                }
            }
        }

        // 4. Compact roots to dense region ids; accumulate a representative color per region.
        var regionIndex = new Dictionary<int, int>();
        var accumulators = new List<RepresentativeColorAccumulator>();
        var areas = new List<int>();

        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            if (label < 0 || rgba[i * 4 + 3] <= AlphaThreshold) continue;

            var root = Find(label);
            if (!regionIndex.TryGetValue(root, out var region))
            {
                region = accumulators.Count;
                regionIndex[root] = region;
                accumulators.Add(new RepresentativeColorAccumulator());
                areas.Add(0);
            }

            accumulators[region].Add(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]);
            areas[region]++;
        }

        return BuildPalette(accumulators, areas);
    }

    /// <summary>
    /// Builds a palette from a flat (non-spatial) cluster labeling: one representative color per
    /// cluster, no adjacency or merging. Used by spatial K-Means, whose clusters are already whole
    /// groups rather than many small superpixels.
    /// </summary>
    public static List<ColorPalette> FromClusterLabels(int[] clusterId, int k, byte[] rgba)
    {
        var accumulators = new List<RepresentativeColorAccumulator>(k);
        var areas = new List<int>(k);
        for (var i = 0; i < k; i++)
        {
            accumulators.Add(new RepresentativeColorAccumulator());
            areas.Add(0);
        }

        for (var i = 0; i < clusterId.Length; i++)
        {
            var c = clusterId[i];
            if (c < 0 || rgba[i * 4 + 3] <= AlphaThreshold) continue;
            accumulators[c].Add(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]);
            areas[c]++;
        }

        return BuildPalette(accumulators, areas);
    }

    private static List<ColorPalette> BuildPalette(List<RepresentativeColorAccumulator> accumulators, List<int> areas)
    {
        var palette = new List<ColorPalette>(accumulators.Count);
        for (var r = 0; r < accumulators.Count; r++)
        {
            if (accumulators[r].IsEmpty) continue;
            var (cr, cg, cb) = accumulators[r].Resolve();
            palette.Add(new ColorPalette { R = cr, G = cg, B = cb, PixelCount = areas[r] });
        }

        return palette.OrderByDescending(p => p.PixelCount).ToList();
    }

    private static void AddEdge(HashSet<long> edges, int a, int b, int n)
    {
        if (b < 0 || a == b) return;
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        edges.Add((long)min * n + max);
    }
}
