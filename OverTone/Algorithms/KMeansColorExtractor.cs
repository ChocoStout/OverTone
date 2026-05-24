using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// Extracts a dominant color palette from image data using K-Means clustering.
/// </summary>
/// <remarks>Processes visible pixels (alpha > 128) and groups them into the specified number of clusters.
/// Results are sorted by pixel frequency in descending order.</remarks>
public class KMeansColorExtractor : IColorPaletteExtractor
{
    /// <inheritdoc />
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.KMeans;

    // Random number generator used to seed initial centroids. Can be injected for testability.
    private readonly Random _random;

    // Maximum number of iterations for the K-Means algorithm. Made configurable for tuning.
    private readonly int _maxIterations;

    /// <summary>
    /// Initializes a new instance of <see cref="KMeansColorExtractor"/> with default settings.
    /// </summary>
    public KMeansColorExtractor() : this(new Random(), 20) { }

    /// <summary>
    /// Initializes a new instance of <see cref="KMeansColorExtractor"/> with the provided random generator
    /// and maximum iteration count. Providing a Random instance improves testability and reproducibility.
    /// </summary>
    /// <param name="random">Random instance to use when selecting initial centroids.</param>
    /// <param name="maxIterations">Maximum number of K-Means iterations to perform. Must be greater than zero.</param>
    private KMeansColorExtractor(Random random, int maxIterations)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));

        if (maxIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxIterations), "maxIterations must be greater than zero");

        _maxIterations = maxIterations;
    }

    /// <summary>
    /// Extracts a palette from raw image bytes using the K-Means clustering algorithm.
    /// Only pixels with an alpha value greater than 128 are considered visible and used
    /// for clustering.
    /// </summary>
    /// <param name="imageData">Raw image bytes (PNG, JPEG, etc.).</param>
    /// <param name="colorCount">The number of desired color clusters.</param>
    /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> ordered by pixel frequency.</returns>
    /// <exception cref="System.Exception">Thrown when no visible pixels are found in the image.</exception>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        var image = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
        var pixels = image.Data;

        // Collect RGB vectors, ignoring transparent pixels.
        // Each point is a 3-element byte[] representing [R, G, B].
        // Transparent pixels (alpha <= 128) are considered invisible and skipped.
        var points = new List<byte[]>();

        for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex += 4)
        {
            var red = pixels[pixelIndex];
            var green = pixels[pixelIndex + 1];
            var blue = pixels[pixelIndex + 2];
            var alpha = pixels[pixelIndex + 3];

            if (alpha > 128)
                points.Add([red, green, blue]);
        }

        if (points.Count == 0)
            throw new Exception("No visible pixels found on image");

        // Subsample to a fixed cap to keep K-Means fast on large images.
        // Stride sampling is used (deterministic, no extra Random call) rather than
        // shuffling the whole list so memory stays bounded.
        const int maxSamples = 10_000;
        if (points.Count > maxSamples)
        {
            var stride  = points.Count / maxSamples;
            var sampled = new List<byte[]>(maxSamples);
            for (var s = 0; s < points.Count; s += stride)
                sampled.Add(points[s]);
            points = sampled;
        }

        var palette = RunKMeans(points, colorCount);

        return await Task.FromResult(palette);
    }

    /// <summary>
    /// Performs K-Means clustering on RGB color data to generate a dominant color palette.
    /// </summary>
    /// <remarks>The algorithm runs for a maximum number of iterations or until centroids converge.
    /// Initial centroids are randomly selected from the input points.</remarks>
    /// <param name="points">The collection of RGB color values represented as byte arrays, where each array contains three bytes for
    /// red, green, and blue components.</param>
    /// <param name="k">The number of color clusters to generate.</param>
    /// <returns>A list of color palettes sorted by pixel count in descending order, containing RGB values and the number of
    /// pixels assigned to each cluster.</returns>
    private List<ColorPalette> RunKMeans(List<byte[]> points, int k)
    {
        // Select k unique random initial centroids.
        // Initial centroid selection greatly affects convergence and results. We use a
        // partial Fisher-Yates shuffle over indices to sample k unique points without replacement.
        // This is more efficient and clearer than ordering the full list by a random key.
        var centroids = new List<byte[]>(k);

        if (k >= points.Count)
        {
            // If the requested number of clusters is greater than or equal to the number of
            // available color points, use each distinct point as a centroid. If k is larger
            // than the unique points, randomly duplicate some points to reach k centroids.
            centroids.AddRange(points.Select(p => (byte[])p.Clone()));

            // If k is larger than available points, fill remaining centroids by random sampling with replacement
            while (centroids.Count < k)
                centroids.Add((byte[])points[_random.Next(points.Count)].Clone());
        }
        else
        {
            // Create a list of indices and perform a partial Fisher-Yates shuffle to pick k unique indices.
            // The partial shuffle ensures the first k entries of 'indices' are unique random indices
            // in the range [0, points.Count).
            var indices = new List<int>(points.Count);
            for (var i = 0; i < points.Count; i++)
                indices.Add(i);

            for (var i = 0; i < k; i++)
            {
                var swapIndex = _random.Next(i, indices.Count);

                (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);

                centroids.Add((byte[])points[indices[i]].Clone());
            }
        }

        // We'll keep the last computed clusters available after the loop, so we don't need
        // to recompute assignments when building the final results.
        List<List<byte[]>>? clusters = null;

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            // E-step: assign points to nearest centroid
            clusters = ClusteringHelpers.AssignPointsToClusters(points, centroids);

            // M-step: recompute centroids from clusters
            var newCentroids = new List<byte[]>();
            var centroidsChanged = false;

            for (var clusterIndex = 0; clusterIndex < k; clusterIndex++)
            {
                var cluster = clusters[clusterIndex];
                if (cluster.Count == 0)
                {
                    // Preserve old centroid when cluster is empty
                    newCentroids.Add(centroids[clusterIndex]);
                    continue;
                }

                var computed = ClusteringHelpers.ComputeCentroidFromCluster(cluster);

                // If the computed centroid differs, note change. Use Span.SequenceEqual for
                // a concise element-wise comparison instead of repeated index access.
                var oldCentroid = centroids[clusterIndex];

                if (!computed.AsSpan().SequenceEqual(oldCentroid))
                    centroidsChanged = true;

                newCentroids.Add(computed);
            }

            centroids = newCentroids;

            if (!centroidsChanged)
                break;
        }

        var clusterPixelCounts = new int[k];

        // Use the last computed cluster assignments to populate counts
        if (clusters != null)
        {
            for (var clusterIndex = 0; clusterIndex < k; clusterIndex++)
                clusterPixelCounts[clusterIndex] = clusters[clusterIndex].Count;
        }
        else
        {
            // Fallback: assign points once if for some reason the loop body didn't run
            foreach (var centroidIndex in points.Select(point => ClusteringHelpers.NearestCentroid(point, centroids)))
                clusterPixelCounts[centroidIndex]++;
        }

        var result = new List<ColorPalette>();

        for (var clusterIndex = 0; clusterIndex < k; clusterIndex++)
        {
            result.Add(new ColorPalette
            {
                R = centroids[clusterIndex][0],
                G = centroids[clusterIndex][1],
                B = centroids[clusterIndex][2],
                PixelCount = clusterPixelCounts[clusterIndex]
            });
        }

        return result.OrderByDescending(c => c.PixelCount).ToList();
    }
}