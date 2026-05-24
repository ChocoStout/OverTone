using OverTone;
namespace OverTone.Algorithms;

/// <summary>
/// Shared utilities for small clustering algorithms used by color extractors.
/// These helpers are intentionally simple and documented to aid readability.
/// </summary>
internal static class ClusteringHelpers
{
    /// <summary>
    /// Computes squared Euclidean distance between two RGB byte vectors.
    /// Returns an integer because inputs are byte values.
    /// </summary>
    private static int SquaredEuclideanDistance(byte[] a, byte[] b)
    {
        var dr = a[0] - b[0];
        var dg = a[1] - b[1];
        var db = a[2] - b[2];
        return dr * dr + dg * dg + db * db;
    }

    /// <summary>
    /// Computes squared Euclidean distance between an RGB byte vector and an RGB double vector.
    /// This overload is useful when centroids are stored as doubles.
    /// </summary>
    public static double SquaredEuclideanDistance(byte[] a, double[] b)
    {
        var dr = a[0] - b[0];
        var dg = a[1] - b[1];
        var db = a[2] - b[2];
        return dr * dr + dg * dg + db * db;
    }

    /// <summary>
    /// Returns the index of the nearest centroid (from a list of byte[] centroids) to the supplied point.
    /// </summary>
    public static int NearestCentroid(byte[] point, List<byte[]> centroids)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;

        for (var centroidIndex = 0; centroidIndex < centroids.Count; centroidIndex++)
        {
            var distance = SquaredEuclideanDistance(point, centroids[centroidIndex]);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestIndex = centroidIndex;
        }

        return bestIndex;
    }

    /// <summary>
    /// Assigns each point to the nearest centroid and returns the clusters.
    /// </summary>
    public static List<List<byte[]>> AssignPointsToClusters(List<byte[]> points, List<byte[]> centroids)
    {
        var k = centroids.Count;
        var clusters = new List<List<byte[]>>(k);

        for (var i = 0; i < k; i++)
            clusters.Add([]);

        foreach (var point in points)
        {
            var nearest = NearestCentroid(point, centroids);
            clusters[nearest].Add(point);
        }

        return clusters;
    }

    /// <summary>
    /// Computes the centroid (average RGB) for a cluster of points and returns it as a byte[3].
    /// </summary>
    public static byte[] ComputeCentroidFromCluster(List<byte[]> cluster)
    {
        double sumR = 0, sumG = 0, sumB = 0;

        foreach (var c in cluster)
        {
            sumR += c[0];
            sumG += c[1];
            sumB += c[2];
        }

        var avgR = (int)(sumR / cluster.Count);
        var avgG = (int)(sumG / cluster.Count);
        var avgB = (int)(sumB / cluster.Count);

        return [(byte)avgR, (byte)avgG, (byte)avgB];
    }

    /// <summary>
    /// Initialize a membership matrix for fuzzy clustering with random values normalized per point.
    /// </summary>
    public static double[][] InitializeMemberships(int pointCount, int clusterCount, Random random)
    {
        var memberships = new double[pointCount][];

        for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            memberships[pointIndex] = new double[clusterCount];
            var sum = 0.0;

            for (var clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
            {
                memberships[pointIndex][clusterIndex] = random.NextDouble();
                sum += memberships[pointIndex][clusterIndex];
            }

            for (var clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
                memberships[pointIndex][clusterIndex] /= sum;
        }

        return memberships;
    }

    /// <summary>
    /// Compute centroids (double[3]) from memberships using fuzziness factor.
    /// </summary>
    public static double[][] ComputeCentroidsFromMemberships(List<byte[]> points, double[][] memberships, double fuzziness, Random random)
    {
        var pointCount = points.Count;
        var clusterCount = memberships[0].Length;
        var centroids = new double[clusterCount][];

        for (var clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
        {
            var weightedRSum = 0.0;
            var weightedGSum = 0.0;
            var weightedBSum = 0.0;
            var weightedSumDenominator = 0.0;

            for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                var membershipRaised = Math.Pow(memberships[pointIndex][clusterIndex], fuzziness);
                weightedRSum += membershipRaised * points[pointIndex][0];
                weightedGSum += membershipRaised * points[pointIndex][1];
                weightedBSum += membershipRaised * points[pointIndex][2];
                weightedSumDenominator += membershipRaised;
            }

            if (weightedSumDenominator == 0.0)
            {
                var fallbackPoint = points[random.Next(pointCount)];
                centroids[clusterIndex] = [fallbackPoint[0], fallbackPoint[1], fallbackPoint[2]];
            }
            else
            {
                centroids[clusterIndex] =
                [
                    weightedRSum / weightedSumDenominator,
                    weightedGSum / weightedSumDenominator,
                    weightedBSum / weightedSumDenominator
                ];
            }
        }

        return centroids;
    }

    /// <summary>
    /// Update memberships in place given centroids and fuzziness. Returns the maximum absolute change observed.
    /// </summary>
    public static double UpdateMemberships(List<byte[]> points, double[][] centroids, double fuzziness, double[][] memberships)
    {
        var pointCount = points.Count;
        var clusterCount = centroids.Length;
        var maxChange = 0.0;

        for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            for (var clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
            {
                var distToCluster = SquaredEuclideanDistance(points[pointIndex], centroids[clusterIndex]);

                if (distToCluster == 0.0)
                {
                    for (var k = 0; k < clusterCount; k++)
                    {
                        var old = memberships[pointIndex][k];
                        memberships[pointIndex][k] = k == clusterIndex ? 1.0 : 0.0;
                        maxChange = Math.Max(maxChange, Math.Abs(memberships[pointIndex][k] - old));
                    }

                    break;
                }

                var denominatorSum = 0.0;
                for (var k = 0; k < clusterCount; k++)
                {
                    var distToOther = SquaredEuclideanDistance(points[pointIndex], centroids[k]);

                    if (distToOther == 0.0)
                    {
                        denominatorSum += 1e-10;
                    }
                    else
                    {
                        denominatorSum += Math.Pow(distToCluster / distToOther, 1.0 / (fuzziness - 1.0));
                    }
                }

                var newMembership = 1.0 / denominatorSum;
                maxChange = Math.Max(maxChange, Math.Abs(memberships[pointIndex][clusterIndex] - newMembership));
                memberships[pointIndex][clusterIndex] = newMembership;
            }
        }

        return maxChange;
    }

    /// <summary>
    /// Build final color palette from memberships and fuzziness.
    /// </summary>
    public static List<ColorPalette> BuildPaletteFromMemberships(List<byte[]> points, double[][] memberships, double fuzziness, Random random)
    {
        var pointCount = points.Count;
        var clusterCount = memberships[0].Length;
        var palettes = new List<ColorPalette>(clusterCount);

        for (var clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
        {
            var weightedSum = 0.0;
            var weightedRAccum = 0.0;
            var weightedGAccum = 0.0;
            var weightedBAccum = 0.0;

            for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                var weight = Math.Pow(memberships[pointIndex][clusterIndex], fuzziness);
                weightedSum += weight;
                weightedRAccum += weight * points[pointIndex][0];
                weightedGAccum += weight * points[pointIndex][1];
                weightedBAccum += weight * points[pointIndex][2];
            }

            if (weightedSum == 0.0)
            {
                var fallback = points[random.Next(pointCount)];
                palettes.Add(new ColorPalette
                {
                    R = fallback[0],
                    G = fallback[1],
                    B = fallback[2],
                    PixelCount = 0
                });
                continue;
            }

            var red = (byte)Math.Clamp((int)(weightedRAccum / weightedSum), 0, 255);
            var green = (byte)Math.Clamp((int)(weightedGAccum / weightedSum), 0, 255);
            var blue = (byte)Math.Clamp((int)(weightedBAccum / weightedSum), 0, 255);

            var approximatePixelCount = 0.0;
            for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
                approximatePixelCount += memberships[pointIndex][clusterIndex];

            palettes.Add(new ColorPalette
            {
                R = red,
                G = green,
                B = blue,
                PixelCount = (int)Math.Round(approximatePixelCount)
            });
        }

        return palettes.OrderByDescending(p => p.PixelCount).ToList();
    }
}
