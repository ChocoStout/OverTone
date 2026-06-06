namespace OverTone;

/// <summary>
/// Tuning for the spatial (5D) K-Means extractor (<see cref="PaletteAlgorithm.SpatialKMeans"/>), which
/// clusters pixels as <c>(L, a, b, x, y)</c> rather than color alone.
/// </summary>
/// <param name="Seed">Deterministic RNG seed for k-means++ initialization (a fresh generator per run).</param>
/// <param name="MaxIterations">Maximum Lloyd iterations; the run also stops early once centroids settle.</param>
/// <param name="SpatialWeight">
/// How much a pixel's position matters relative to its color.
/// <list type="bullet">
/// <item><c>0</c> — pure color clustering (identical to the legacy color-space K-Means).</item>
/// <item><c>0.5</c> (default) — balanced: clusters cohere spatially but color still dominates.</item>
/// <item><c>~1</c> — position is about as influential as a full L-range color difference.</item>
/// </list>
/// </param>
/// <param name="MaxPixels">
/// The image is box-downscaled to at most this many pixels before clustering, keeping the spatial work
/// bounded on large images.
/// </param>
public record SpatialKMeansOptions(
    int Seed = 1989,
    int MaxIterations = 20,
    double SpatialWeight = 0.5,
    int MaxPixels = 180_000);
