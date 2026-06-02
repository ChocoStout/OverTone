namespace OverTone;

/// <summary>
/// Tuning options for the Fuzzy C-Means extractor. Injectable via DI or passed to the constructor.
/// </summary>
/// <param name="Seed">Fixed seed for membership initialisation; a fixed value keeps results deterministic.</param>
/// <param name="MaxIterations">Maximum number of refinement iterations. Must be greater than zero.</param>
/// <param name="Fuzziness">The fuzziness exponent <c>m</c>. Must be greater than 1.0.</param>
public record FuzzyCMeansOptions(int Seed = 1989, int MaxIterations = 100, double Fuzziness = 2.0);
