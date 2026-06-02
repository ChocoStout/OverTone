using System.Collections.Concurrent;
using OverTone;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts a dominant color palette using K-Means clustering with k-means++ seeding.
/// </summary>
/// <remarks>Processes visible pixels (alpha &gt; 128), stride-sampled to ≤ 10k for speed, and groups
/// them into the requested number of clusters. Deterministic (fixed seed); results are sorted by
/// pixel frequency.</remarks>
public sealed class KMeansColorExtractor : ColorPaletteExtractorBase
{
    // Below this many points, parallel overhead outweighs the benefit; run sequentially.
    private const int ParallelThreshold = 1024;
    private const int MaxSamples = 10_000;

    private readonly int _seed;
    private readonly int _maxIterations;

    /// <summary>Creates the extractor with the given options (defaults when <c>null</c>).</summary>
    public KMeansColorExtractor(KMeansOptions? options = null)
    {
        var o = options ?? new KMeansOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(o.MaxIterations);
        _seed = o.Seed;
        _maxIterations = o.MaxIterations;
    }

    /// <inheritdoc />
    public override PaletteAlgorithm Algorithm => PaletteAlgorithm.KMeans;

    /// <inheritdoc />
    protected override List<ColorPalette> ExtractCore(byte[] rgba, int colorCount, int maxDegreeOfParallelism)
    {
        var points = ExtractVisiblePixels(rgba, MaxSamples);
        return RunKMeans(points, colorCount, maxDegreeOfParallelism);
    }

    private List<ColorPalette> RunKMeans(List<byte[]> points, int k, int maxDegreeOfParallelism)
    {
        // A fresh, fixed-seed generator per extraction guarantees the same image yields the same palette.
        var rng = new Random(_seed);

        // k-means++ seeds centroids with probability proportional to their squared distance from the
        // nearest already-chosen centroid — this spreads seeds across the color space and gives small
        // but chromatically distinct regions (accent colors) a real chance to seed their own cluster.
        List<byte[]> centroids;

        if (k >= points.Count)
        {
            centroids = new List<byte[]>(k);
            centroids.AddRange(points.Select(p => (byte[])p.Clone()));
            while (centroids.Count < k)
                centroids.Add((byte[])points[rng.Next(points.Count)].Clone());
        }
        else
        {
            centroids = InitCentroidsPlusPlus(points, k, rng);
        }

        // Lloyd's iterations: assign each point to its nearest centroid and accumulate per-cluster
        // channel sums + counts (parallelized when requested), then recompute centroids.
        var counts = new int[k];

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            var (sumR, sumG, sumB, count) = AssignAndAccumulate(points, centroids, maxDegreeOfParallelism);
            counts = count;

            var newCentroids = new List<byte[]>(k);
            var centroidsChanged = false;

            for (var clusterIndex = 0; clusterIndex < k; clusterIndex++)
            {
                if (count[clusterIndex] == 0)
                {
                    newCentroids.Add(centroids[clusterIndex]);
                    continue;
                }

                byte[] computed =
                [
                    (byte)(int)((double)sumR[clusterIndex] / count[clusterIndex]),
                    (byte)(int)((double)sumG[clusterIndex] / count[clusterIndex]),
                    (byte)(int)((double)sumB[clusterIndex] / count[clusterIndex]),
                ];

                if (!computed.AsSpan().SequenceEqual(centroids[clusterIndex]))
                    centroidsChanged = true;

                newCentroids.Add(computed);
            }

            centroids = newCentroids;

            if (!centroidsChanged)
                break;
        }

        var result = new List<ColorPalette>(k);
        for (var clusterIndex = 0; clusterIndex < k; clusterIndex++)
        {
            result.Add(new ColorPalette
            {
                R = centroids[clusterIndex][0],
                G = centroids[clusterIndex][1],
                B = centroids[clusterIndex][2],
                PixelCount = counts[clusterIndex]
            });
        }

        return result.OrderByDescending(c => c.PixelCount).ToList();
    }

    /// <summary>
    /// Chooses <paramref name="k"/> initial centroids using k-means++: the first is uniform-random,
    /// each subsequent one is chosen with probability proportional to its squared distance from the
    /// nearest centroid already selected.
    /// </summary>
    private static List<byte[]> InitCentroidsPlusPlus(List<byte[]> points, int k, Random rng)
    {
        var centroids = new List<byte[]>(k)
        {
            (byte[])points[rng.Next(points.Count)].Clone(),
        };

        var nearestDistSq = new double[points.Count];
        for (var i = 0; i < points.Count; i++)
            nearestDistSq[i] = SquaredDistance(points[i], centroids[0]);

        while (centroids.Count < k)
        {
            var total = 0.0;
            foreach (var d in nearestDistSq)
                total += d;

            byte[] next;
            if (total <= 0.0)
            {
                next = (byte[])points[rng.Next(points.Count)].Clone();
            }
            else
            {
                var threshold = rng.NextDouble() * total;
                var index = 0;
                var cumulative = 0.0;
                for (; index < nearestDistSq.Length; index++)
                {
                    cumulative += nearestDistSq[index];
                    if (cumulative >= threshold)
                        break;
                }

                if (index >= points.Count)
                    index = points.Count - 1;

                next = (byte[])points[index].Clone();
            }

            centroids.Add(next);

            for (var i = 0; i < points.Count; i++)
            {
                var d = SquaredDistance(points[i], next);
                if (d < nearestDistSq[i])
                    nearestDistSq[i] = d;
            }
        }

        return centroids;

        static double SquaredDistance(byte[] a, byte[] b)
        {
            double dr = a[0] - b[0], dg = a[1] - b[1], db = a[2] - b[2];
            return dr * dr + dg * dg + db * db;
        }
    }

    /// <summary>
    /// Assigns each point to its nearest centroid and accumulates per-cluster channel sums and counts.
    /// Runs in parallel when <paramref name="maxDegreeOfParallelism"/> &gt; 1 and the input is large
    /// enough. Because the channel sums are integers, parallel and sequential results are identical.
    /// </summary>
    private static (long[] SumR, long[] SumG, long[] SumB, int[] Count) AssignAndAccumulate(
        List<byte[]> points, List<byte[]> centroids, int maxDegreeOfParallelism)
    {
        var k = centroids.Count;

        if (maxDegreeOfParallelism <= 1 || points.Count < ParallelThreshold)
        {
            var sumR = new long[k];
            var sumG = new long[k];
            var sumB = new long[k];
            var count = new int[k];

            foreach (var p in points)
            {
                var c = ClusteringHelpers.NearestCentroid(p, centroids);
                sumR[c] += p[0];
                sumG[c] += p[1];
                sumB[c] += p[2];
                count[c]++;
            }

            return (sumR, sumG, sumB, count);
        }

        var totalR = new long[k];
        var totalG = new long[k];
        var totalB = new long[k];
        var totalCount = new int[k];
        var sync = new object();
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };

        Parallel.ForEach(
            Partitioner.Create(0, points.Count),
            options,
            () => (sr: new long[k], sg: new long[k], sb: new long[k], ct: new int[k]),
            (range, _, local) =>
            {
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    var p = points[i];
                    var c = ClusteringHelpers.NearestCentroid(p, centroids);
                    local.sr[c] += p[0];
                    local.sg[c] += p[1];
                    local.sb[c] += p[2];
                    local.ct[c]++;
                }

                return local;
            },
            local =>
            {
                lock (sync)
                {
                    for (var j = 0; j < k; j++)
                    {
                        totalR[j] += local.sr[j];
                        totalG[j] += local.sg[j];
                        totalB[j] += local.sb[j];
                        totalCount[j] += local.ct[j];
                    }
                }
            });

        return (totalR, totalG, totalB, totalCount);
    }
}
