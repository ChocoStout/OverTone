using StbImageSharp;

namespace OverTone
{
    /// <summary>
    /// Provides methods to extract a color palette from an image source.
    /// Supports extracting from local files or remote URLs and delegates the
    /// actual extraction work to an <see cref="IColorPaletteExtractor"/> implementation.
    /// </summary>
    public class PaletteGenerator
    {
        private readonly HttpClient _httpClient = new();

        // ToDo: automate this
        private readonly Dictionary<PaletteAlgorithm, IColorPaletteExtractor> _colorPaletteExtractors = new()
        {
            { PaletteAlgorithm.KMeans, new KMeansColorExtractor() }
        };

        /// <summary>
        /// Extracts a list of dominant colors from the given image source.
        /// </summary>
        /// <param name="source">A file path or URL to the image.</param>
        /// <param name="colorCount">The number of colors to return in the palette.</param>
        /// <param name="isUrl">True when <paramref name="source"/> is a URL; false when it is a local file path.</param>
        /// <param name="algorithm">The clustering algorithm to use for extraction.</param>
        /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> entries, ordered by frequency.</returns>
        /// <exception cref="NotSupportedException">Thrown when the requested algorithm is not implemented.</exception>
        /// <exception cref="System.IO.IOException">Thrown when reading the source image fails.</exception>
        public async Task<List<ColorPalette>> ExtractColorPaletteAsync(string source, int colorCount, bool isUrl,
            PaletteAlgorithm algorithm = PaletteAlgorithm.KMeans)
        {
            byte[] imageData;

            if (isUrl)
                imageData = await _httpClient.GetByteArrayAsync(source);
            else
                imageData = await File.ReadAllBytesAsync(source);

            if (!_colorPaletteExtractors.TryGetValue(algorithm, out var extractor))
                throw new NotSupportedException($"Algorithm: {algorithm} is not implemented");

            return await extractor.ExtractColorPaletteAsync(imageData, colorCount);
        }
    }

    /// <summary>
    /// Represents a single color in a generated palette, including its RGB components
    /// and the number of pixels in the image that were assigned to this color cluster.
    /// </summary>
    public class ColorPalette
    {
        /// <summary>
        /// Red channel value (0-255).
        /// </summary>
        public byte R { get; set; }

        /// <summary>
        /// Green channel value (0-255).
        /// </summary>
        public byte G { get; set; }

        /// <summary>
        /// Blue channel value (0-255).
        /// </summary>
        public byte B { get; set; }

        /// <summary>
        /// Number of pixels from the source image that were assigned to this color cluster.
        /// </summary>
        public int PixelCount { get; set; }

        /// <summary>
        /// Gets the color formatted as a hexadecimal string (#RRGGBB).
        /// </summary>
        public string AsHex => $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>
    /// Specifies the algorithm used to extract a color palette from an image.
    /// </summary>
    public enum PaletteAlgorithm
    {
        /// <summary>
        /// 
        /// </summary>
        KMeans
    }

    /// <summary>
    /// Defines the contract that image palette extractors must implement. Implementations
    /// accept raw image bytes and return a list of dominant colors found in the image.
    /// </summary>
    public interface IColorPaletteExtractor
    {
        /// <summary>
        /// Extracts a palette of dominant colors from the provided image data.
        /// </summary>
        /// <param name="imageData">The raw image bytes (for example, PNG or JPEG contents).</param>
        /// <param name="colorCount">The number of colors to include in the returned palette.</param>
        /// <returns>A task that resolves to a list of <see cref="ColorPalette"/> items.</returns>
        Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount);
    }

    /// <summary>
    /// Extracts a dominant color palette from image data using K-Means clustering.
    /// </summary>
    /// <remarks>Processes visible pixels (alpha > 128) and groups them into the specified number of clusters.
    /// Results are sorted by pixel frequency in descending order.</remarks>
    public class KMeansColorExtractor : IColorPaletteExtractor
    {
        // ToDo: allow injection of random?
        private readonly Random _random = new();

        // ToDo: why 20?
        private const int MaxIterations = 20;

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

            // Collect RGB vectors, ignoring transparent pixels
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
            // ToDo: too much syntax sugar?
            var centroids = points.OrderBy(_ => _random.Next()).Take(k).Select(p => (byte[])p.Clone()).ToList();

            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                // Assign each point to nearest centroid
                var clusters = new List<List<byte[]>>(k);

                for (var clusterIndex = 0; clusterIndex < k; clusterIndex++)
                    clusters.Add([]);

                foreach (var point in points)
                {
                    var centroidIndex = NearestCentroid(point, centroids);
                    clusters[centroidIndex].Add(point);
                }

                // Recompute centroids
                var newCentroids = new List<byte[]>();
                var centroidsChanged = false;

                for (var clusterIndex = 0; clusterIndex < k; clusterIndex++)
                {
                    if (clusters[clusterIndex].Count == 0)
                    {
                        newCentroids.Add(centroids[clusterIndex]);
                        continue;
                    }

                    double sumRed = 0, sumGreen = 0, sumBlue = 0;

                    foreach (var pixelColor in clusters[clusterIndex])
                    {
                        sumRed += pixelColor[0];
                        sumGreen += pixelColor[1];
                        sumBlue += pixelColor[2];
                    }

                    var avgRed = (int)(sumRed / clusters[clusterIndex].Count);
                    var avgGreen = (int)(sumGreen / clusters[clusterIndex].Count);
                    var avgBlue = (int)(sumBlue / clusters[clusterIndex].Count);

                    var centroidRed = (byte)avgRed;
                    var centroidGreen = (byte)avgGreen;
                    var centroidBlue = (byte)avgBlue;

                    if (centroidRed != centroids[clusterIndex][0] || centroidGreen != centroids[clusterIndex][1] || centroidBlue != centroids[clusterIndex][2])
                        centroidsChanged = true;

                    newCentroids.Add([centroidRed, centroidGreen, centroidBlue]);
                }

                centroids = newCentroids;

                if (!centroidsChanged)
                    break;
            }

            var clusterPixelCounts = new int[k];

            foreach (var centroidIndex in points.Select(point => NearestCentroid(point, centroids)))
                clusterPixelCounts[centroidIndex]++;

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

        /// <summary>
        /// Finds the index of the nearest centroid to the specified RGB point using squared Euclidean distance.
        /// </summary>
        /// <param name="point">The RGB color point as a byte array (R, G, B).</param>
        /// <param name="centroids">The list of centroid RGB points.</param>
        /// <returns>The index of the nearest centroid.</returns>
        private static int NearestCentroid(byte[] point, List<byte[]> centroids)
        {
            var bestIndex = 0;
            var bestDistance = int.MaxValue;

            for (var centroidIndex = 0; centroidIndex < centroids.Count; centroidIndex++)
            {
                var deltaR = point[0] - centroids[centroidIndex][0];
                var deltaG = point[1] - centroids[centroidIndex][1];
                var deltaB = point[2] - centroids[centroidIndex][2];

                var distance = deltaR * deltaR + deltaG * deltaG + deltaB * deltaB;

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = centroidIndex;
            }

            return bestIndex;
        }
    }
}
