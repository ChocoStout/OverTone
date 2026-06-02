namespace OverTone;

/// <summary>
/// Tuning options for the K-Means extractor. Injectable via DI or passed to the constructor.
/// </summary>
/// <param name="Seed">Fixed seed for k-means++ initialisation; a fixed value keeps results deterministic.</param>
/// <param name="MaxIterations">Maximum number of Lloyd iterations. Must be greater than zero.</param>
public record KMeansOptions(int Seed = 1989, int MaxIterations = 20);
