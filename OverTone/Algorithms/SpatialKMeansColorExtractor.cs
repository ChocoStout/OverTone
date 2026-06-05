using System.Collections.Concurrent;
using OverTone;
using OverTone.Processing;

namespace OverTone.Algorithms;

/// <summary>
/// Spatial (5D) K-Means clustering. Each pixel is a point <c>(L, a, b, x, y)</c>, so clusters become
/// spatially-coherent blobs rather than purely global color bins — the spatial dimensions pull
/// same-colored-but-far-apart pixels into different clusters and keep an object's pixels together.
/// </summary>
/// <remarks>
/// The <c>SpatialWeight</c> dial scales position relative to color: <c>0</c> reproduces the legacy
/// color-only K-Means exactly; higher values make geometry matter more. Output colors are each
/// cluster's <em>representative</em> (peak) color, not the desaturated centroid mean. Deterministic:
/// a fixed-seed k-means++ start, parallel assignment, and a sequential (order-independent) update mean
/// the palette is identical regardless of the parallelism setting.
/// </remarks>
public sealed class SpatialKMeansColorExtractor : ColorPaletteExtractorBase
{
    private const int ParallelThreshold = 4096;
    private const double PositionScale = 100.0; // puts normalized position on roughly the same footing as L.

    private readonly int _seed;
    private readonly int _maxIterations;
    private readonly double _spatialWeight;
    private readonly int _maxPixels;

    /// <summary>Creates the extractor with the given options (defaults when <c>null</c>).</summary>
    public SpatialKMeansColorExtractor(SpatialKMeansOptions? options = null)
    {
        var o = options ?? new SpatialKMeansOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(o.MaxIterations);
        _seed = o.Seed;
        _maxIterations = o.MaxIterations;
        _spatialWeight = Math.Max(0.0, o.SpatialWeight);
        _maxPixels = o.MaxPixels;
    }

    /// <inheritdoc />
    public override PaletteAlgorithm Algorithm => PaletteAlgorithm.SpatialKMeans;

    /// <inheritdoc />
    protected override List<ColorPalette> ExtractCore(DecodedImage image, int colorCount, int maxDegreeOfParallelism)
    {
        var img = DownscaleToMaxPixels(image, _maxPixels);
        int w = img.Width, h = img.Height, n = w * h;
        var rgba = img.Rgba;

        var m = 0;
        for (var i = 0; i < n; i++)
            if (rgba[i * 4 + 3] > 128) m++;
        if (m == 0)
            throw new InvalidOperationException("No visible pixels found in the image.");

        // 5D features for visible pixels: (L, a, b, w·xn·C, w·yn·C). Position is normalized by the image
        // diagonal (resolution-independent) and scaled by SpatialWeight; w = 0 zeroes x/y → pure color.
        var diag = Math.Sqrt((double)w * w + (double)h * h);
        var posK = diag > 0 ? _spatialWeight * PositionScale / diag : 0.0;

        var fL = new double[m];
        var fA = new double[m];
        var fB = new double[m];
        var fX = new double[m];
        var fY = new double[m];
        var srcIndex = new int[m];
        var p = 0;
        for (var i = 0; i < n; i++)
        {
            if (rgba[i * 4 + 3] <= 128) continue;
            var (l, a, b) = ColorMetrics.RgbToLab(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]);
            fL[p] = l; fA[p] = a; fB[p] = b;
            fX[p] = (i % w) * posK;
            fY[p] = (i / w) * posK;
            srcIndex[p] = i;
            p++;
        }

        var k = Math.Min(colorCount, m);
        var centroids = InitCentroidsPlusPlus(fL, fA, fB, fX, fY, k, new Random(_seed));
        var assign = new int[m];

        void AssignRange(int start, int end)
        {
            for (var i = start; i < end; i++)
                assign[i] = Nearest(fL[i], fA[i], fB[i], fX[i], fY[i], centroids, k);
        }

        for (var iter = 0; iter < _maxIterations; iter++)
        {
            if (maxDegreeOfParallelism > 1 && m >= ParallelThreshold)
            {
                Parallel.ForEach(
                    Partitioner.Create(0, m),
                    new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                    range => AssignRange(range.Item1, range.Item2));
            }
            else
            {
                AssignRange(0, m);
            }

            // Sequential accumulation → identical sums whatever the thread count.
            var sumL = new double[k];
            var sumA = new double[k];
            var sumB = new double[k];
            var sumX = new double[k];
            var sumY = new double[k];
            var cnt = new int[k];
            for (var i = 0; i < m; i++)
            {
                var c = assign[i];
                sumL[c] += fL[i]; sumA[c] += fA[i]; sumB[c] += fB[i];
                sumX[c] += fX[i]; sumY[c] += fY[i];
                cnt[c]++;
            }

            var changed = false;
            for (var c = 0; c < k; c++)
            {
                if (cnt[c] == 0) continue;
                double nl = sumL[c] / cnt[c], na = sumA[c] / cnt[c], nb = sumB[c] / cnt[c],
                       nx = sumX[c] / cnt[c], ny = sumY[c] / cnt[c];
                var cc = centroids[c];
                if (nl != cc[0] || na != cc[1] || nb != cc[2] || nx != cc[3] || ny != cc[4])
                    changed = true;
                cc[0] = nl; cc[1] = na; cc[2] = nb; cc[3] = nx; cc[4] = ny;
            }

            if (!changed) break;
        }

        // Map clusters back onto the full grid (transparent pixels stay -1) and build a peak-color palette.
        var clusterId = new int[n];
        Array.Fill(clusterId, -1);
        for (var i = 0; i < m; i++)
            clusterId[srcIndex[i]] = assign[i];

        return RegionPaletteBuilder.FromClusterLabels(clusterId, k, rgba);
    }

    private static int Nearest(double l, double a, double b, double x, double y, double[][] centroids, int k)
    {
        var best = 0;
        var bestD = double.MaxValue;
        for (var c = 0; c < k; c++)
        {
            var cc = centroids[c];
            double dl = l - cc[0], da = a - cc[1], db = b - cc[2], dx = x - cc[3], dy = y - cc[4];
            var d = dl * dl + da * da + db * db + dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    /// <summary>
    /// k-means++ seeding in 5D: the first centroid is a uniform-random point; each subsequent one is
    /// chosen with probability proportional to its squared distance from the nearest centroid so far.
    /// </summary>
    private static double[][] InitCentroidsPlusPlus(
        double[] fL, double[] fA, double[] fB, double[] fX, double[] fY, int k, Random rng)
    {
        var m = fL.Length;
        var centroids = new double[k][];
        var first = rng.Next(m);
        centroids[0] = [fL[first], fA[first], fB[first], fX[first], fY[first]];

        var nearestSq = new double[m];
        for (var i = 0; i < m; i++)
            nearestSq[i] = Dist(i, centroids[0]);

        for (var picked = 1; picked < k; picked++)
        {
            var total = 0.0;
            for (var i = 0; i < m; i++) total += nearestSq[i];

            int chosen;
            if (total <= 0.0)
            {
                chosen = rng.Next(m);
            }
            else
            {
                var threshold = rng.NextDouble() * total;
                var cumulative = 0.0;
                chosen = m - 1;
                for (var i = 0; i < m; i++)
                {
                    cumulative += nearestSq[i];
                    if (cumulative >= threshold) { chosen = i; break; }
                }
            }

            centroids[picked] = [fL[chosen], fA[chosen], fB[chosen], fX[chosen], fY[chosen]];
            for (var i = 0; i < m; i++)
            {
                var d = Dist(i, centroids[picked]);
                if (d < nearestSq[i]) nearestSq[i] = d;
            }
        }

        return centroids;

        double Dist(int i, double[] c)
        {
            double dl = fL[i] - c[0], da = fA[i] - c[1], db = fB[i] - c[2], dx = fX[i] - c[3], dy = fY[i] - c[4];
            return dl * dl + da * da + db * db + dx * dx + dy * dy;
        }
    }
}
