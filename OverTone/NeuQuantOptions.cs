namespace OverTone;

/// <summary>
/// Configuration for the NeuQuant palette extractor.
/// Use <see cref="ForColorCount"/> to get sensible auto-scaled defaults,
/// or construct directly to override specific values.
/// </summary>
/// <param name="NeuronCount">
/// Number of neurons the network trains with. More neurons = finer color
/// discrimination but slower training. Must be &gt; 0.
/// </param>
/// <param name="TrainingIterations">
/// Number of full passes over the sampled pixels. More iterations = better
/// convergence but slower. Must be &gt; 0.
/// </param>
public record NeuQuantOptions(int NeuronCount, int TrainingIterations)
{
    /// <summary>
    /// Builds auto-scaled options for a given target palette size.
    /// Heuristics: <c>neuronCount = max(colorCount × 8, 64)</c>,
    /// <c>trainingIterations = max(colorCount × 10, 100)</c>.
    /// These give NeuQuant enough headroom to explore the color space
    /// and sufficient training to converge, regardless of how many
    /// final colors were requested.
    /// </summary>
    public static NeuQuantOptions ForColorCount(int colorCount) => new(
        NeuronCount:        Math.Max(colorCount * 8,  64),
        TrainingIterations: Math.Max(colorCount * 10, 100));
}
