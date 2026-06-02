using OverTone;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts a dominant color palette from image data using the Fuzzy C-Means (FCM) algorithm.
/// </summary>
/// <remarks>
/// Plain-language overview:
/// This extractor groups similar colors together to produce a short list of "dominant" colors.
/// It treats each pixel as belonging to every group (cluster) to some degree rather than
/// forcing an exclusive assignment. Over several passes the algorithm adjusts the groups so
/// that pixels have higher membership in groups whose colors are closer to them.
///
/// The implementation follows a simple, easy-to-read approach:
/// - Load visible pixels from the image (alpha &gt; 128) and treat each as an RGB point.
/// - Start with random soft membership values that say how much each pixel belongs to each group.
/// - Repeat: compute each group's color based on weighted pixel contributions, then update
///   pixel memberships based on distances to group colors.
/// - Stop when memberships stop changing, or we reach the iteration limit, then produce
///   a final palette of colors ordered by how many pixels (approximately) belong to each.
///
/// The implementation purposefully favors clarity and maintainability over micro-optimizations.
/// </remarks>
public sealed class FuzzyCMeansColorExtractor : ColorPaletteExtractorBase
{
    private const int MaxSamples = 10_000;

    private readonly int _seed;
    private readonly int _maxIterations;
    private readonly double _fuzzinessFactor;

    /// <summary>Creates the extractor with the given options (defaults when <c>null</c>).</summary>
    public FuzzyCMeansColorExtractor(FuzzyCMeansOptions? options = null)
    {
        var o = options ?? new FuzzyCMeansOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(o.MaxIterations);
        if (o.Fuzziness <= 1.0)
            throw new ArgumentOutOfRangeException(nameof(options), "Fuzziness (m) must be > 1.0");

        _seed = o.Seed;
        _maxIterations = o.MaxIterations;
        _fuzzinessFactor = o.Fuzziness;
    }

    /// <inheritdoc />
    public override PaletteAlgorithm Algorithm => PaletteAlgorithm.FuzzyCMeans;

    /// <inheritdoc />
    protected override List<ColorPalette> ExtractCore(byte[] rgba, int colorCount, int maxDegreeOfParallelism)
    {
        var rgbPoints = ExtractVisiblePixels(rgba, MaxSamples);
        return RunFuzzyCMeans(rgbPoints, colorCount);
    }

    /// <summary>
    /// Runs the Fuzzy C-Means clustering on the supplied RGB points and returns
    /// a list of ColorPalette entries. The algorithm keeps soft (fractional)
    /// memberships for each pixel and iteratively refines cluster colors.
    /// </summary>
    /// <param name="points">A list of RGB points (byte[3]).</param>
    /// <param name="clusterCount">Number of clusters to produce.</param>
    private List<ColorPalette> RunFuzzyCMeans(List<byte[]> points, int clusterCount)
    {
        var pointCount = points.Count;

        // Fresh, fixed-seed generator per extraction → the same image yields the same palette.
        var rng = new Random(_seed);

        // Initialize memberships (pointCount x clusterCount) with random normalized values.
        var memberships = ClusteringHelpers.InitializeMemberships(pointCount, clusterCount, rng);

        var centroids = new double[clusterCount][]; // each centroid is a double[3]

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            // Update centroids using the current memberships. Each centroid coordinate is the
            // weighted average of pixel coordinates where the weights are memberships^m.
            for (var clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
            {
                var weightedRSum = 0.0;
                var weightedGSum = 0.0;
                var weightedBSum = 0.0;
                var weightedSumDenominator = 0.0;

                for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    var membershipRaised = Math.Pow(memberships[pointIndex][clusterIndex], _fuzzinessFactor);
                    weightedRSum += membershipRaised * points[pointIndex][0];
                    weightedGSum += membershipRaised * points[pointIndex][1];
                    weightedBSum += membershipRaised * points[pointIndex][2];
                    weightedSumDenominator += membershipRaised;
                }

                if (weightedSumDenominator == 0.0)
                {
                    // fallback: pick a random data point as centroid
                    var fallbackPoint = points[rng.Next(pointCount)];
                    centroids[clusterIndex] = [fallbackPoint[0], fallbackPoint[1], fallbackPoint[2]];
                }
                else
                {
                    centroids[clusterIndex] = [weightedRSum / weightedSumDenominator, weightedGSum / weightedSumDenominator, weightedBSum / weightedSumDenominator
                    ];
                }
            }

            // Update memberships based on distances to centroids. Track the largest change to test for convergence.
            var maxMembershipChange = 0.0;

            for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                for (var clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
                {
                    var distanceToCluster = ClusteringHelpers.SquaredEuclideanDistance(points[pointIndex], centroids[clusterIndex]);

                    // If a point sits exactly on a centroid, make it fully belong to that cluster.
                    if (distanceToCluster == 0.0)
                    {
                        for (var k = 0; k < clusterCount; k++)
                        {
                            var oldMembership = memberships[pointIndex][k];
                            memberships[pointIndex][k] = k == clusterIndex ? 1.0 : 0.0;
                            maxMembershipChange = Math.Max(maxMembershipChange, Math.Abs(memberships[pointIndex][k] - oldMembership));
                        }

                        break;
                    }

                    var denominatorSum = 0.0;
                    for (var k = 0; k < clusterCount; k++)
                    {
                        var distanceToOther = ClusteringHelpers.SquaredEuclideanDistance(points[pointIndex], centroids[k]);
                        if (distanceToOther == 0.0)
                        {
                            denominatorSum += 1e-10; // guard
                        }
                        else
                        {
                            denominatorSum += Math.Pow(distanceToCluster / distanceToOther, 1.0 / (_fuzzinessFactor - 1.0));
                        }
                    }

                    var newMembership = 1.0 / denominatorSum;
                    maxMembershipChange = Math.Max(maxMembershipChange, Math.Abs(memberships[pointIndex][clusterIndex] - newMembership));
                    memberships[pointIndex][clusterIndex] = newMembership;
                }
            }

            if (maxMembershipChange < 1e-5)
                break;
        }
        // Build the final palette. For each cluster, compute the color as the weighted average
        // of pixels using memberships^m (the same weighting used to compute centroids). We also
        // estimate a pixel count by summing memberships for that cluster.

        return ClusteringHelpers.BuildPaletteFromMemberships(points, memberships, _fuzzinessFactor, rng);
    }
}
