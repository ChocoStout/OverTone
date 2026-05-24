using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// NeuQuant-like neural quantizer implementation (compact, simplified).
/// Produces a palette by training a small neural network on sampled pixels.
/// This is not a byte-for-byte port of the original NeuQuant but follows the
/// same idea: competitive learning to discover representative colors.
/// </summary>
/// <summary>
/// NeuQuant-like competitive-learning quantizer. Trains a small network of neurons
/// against sampled visible pixels and returns the most frequently assigned neurons
/// as palette entries.
/// </summary>
public class NeuQuantColorExtractor : IColorPaletteExtractor
{
    /// <inheritdoc />
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.NeuQuant;

    // Number of neurons (colors) the network maintains during training. The classic
    // NeuQuant uses 256, but this value is configurable for speed/quality tradeoffs.
    private readonly int _neuronCount;

    // Number of training iterations (passes over the sampled pixels).
    private readonly int _trainingIterations;

    /// <summary>
    /// Create a new NeuQuant-like extractor.
    /// </summary>
    /// <param name="neuronCount">Number of neurons used by the network (default 256).</param>
    /// <param name="trainingIterations">Number of training iterations (default 100).</param>
    public NeuQuantColorExtractor(int neuronCount = 256, int trainingIterations = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(neuronCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trainingIterations);

        _neuronCount = neuronCount;
        _trainingIterations = trainingIterations;
    }

    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(colorCount);

        var image = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
        var pixels = image.Data;

        // Collect a subsample of visible pixels (alpha > 128).
        var sampled = SampleVisiblePixels(pixels, maxSamples: 16000);

        if (sampled.Count == 0)
            throw new Exception("No visible pixels found on image");

        // Initialize network neurons
        var rng = new Random(0);

        // Initialize neuron vectors from random samples to bootstrap training
        var neurons = new double[_neuronCount][];
        for (var i = 0; i < _neuronCount; i++)
        {
            var p = sampled[rng.Next(sampled.Count)];
            neurons[i] = [p[0], p[1], p[2]];
        }

        const double initialLearningRate = 0.1; // fraction of adjustment applied per update
        var initialRadius = Math.Max(1, _neuronCount / 8);

        for (var iteration = 0; iteration < _trainingIterations; iteration++)
        {
            var progress = iteration / (double)_trainingIterations;
            var learningRate = initialLearningRate * (1.0 - progress);
            var radius = (int)Math.Round(initialRadius * (1.0 - progress));

            // Shuffle samples to avoid order bias during training
            Shuffle(sampled, rng);

            foreach (var sample in sampled)
            {
                var winnerIndex = FindBestNeuronIndex(sample, neurons);

                // Update winner neuron
                MoveNeuronTowards(neurons[winnerIndex], sample, learningRate);

                // Update neighbor neurons within the current radius with decreasing influence
                if (radius <= 0)
                    continue;

                var start = Math.Max(0, winnerIndex - radius);
                var end = Math.Min(_neuronCount - 1, winnerIndex + radius);

                for (var idx = start; idx <= end; idx++)
                {
                    if (idx == winnerIndex) continue;
                    var distance = Math.Abs(idx - winnerIndex);
                    var influence = learningRate * (1.0 - distance / (double)radius);
                    if (influence > 0)
                        MoveNeuronTowards(neurons[idx], sample, influence);
                }
            }
        }

        // Build frequency counts by mapping sampled pixels to nearest neurons
        var counts = new int[_neuronCount];

        foreach (var idx in sampled.Select(sample => FindBestNeuronIndex(sample, neurons))) 
            counts[idx]++;

        // Create palette entries
        var palette = new List<ColorPalette>(_neuronCount);

        for (var i = 0; i < _neuronCount; i++)
        {
            var r = (byte)Math.Clamp((int)Math.Round(neurons[i][0]), 0, 255);
            var g = (byte)Math.Clamp((int)Math.Round(neurons[i][1]), 0, 255);
            var b = (byte)Math.Clamp((int)Math.Round(neurons[i][2]), 0, 255);

            palette.Add(new ColorPalette { R = r, G = g, B = b, PixelCount = counts[i] });
        }

        // Order by frequency and return top colorCount
        var result = palette.OrderByDescending(p => p.PixelCount).Take(colorCount).ToList();

        return await Task.FromResult(result);
    }

    private static List<byte[]> SampleVisiblePixels(byte[] pixels, int maxSamples)
    {
        var points = new List<byte[]>();
        var total = pixels.Length / 4;
        var step = Math.Max(1, total / maxSamples);

        for (var i = 0; i < pixels.Length; i += 4 * step)
        {
            var alpha = pixels[i + 3];
            if (alpha <= 128) continue;
            points.Add([pixels[i], pixels[i + 1], pixels[i + 2]]);
        }

        // If we sampled nothing due to rounding, fall back to scanning sequentially
        if (points.Count == 0)
        {
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];

                if (alpha <= 128) 
                    continue;

                points.Add([pixels[i], pixels[i + 1], pixels[i + 2]]);

                if (points.Count >= maxSamples) 
                    break;
            }
        }

        return points;
    }

    private static int FindBestNeuronIndex(byte[] sample, double[][] neurons)
    {
        var bestIdx = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < neurons.Length; i++)
        {
            var d = DistanceSquared(sample, neurons[i]);

            if (!(d < bestDist)) 
                continue;

            bestDist = d;
            bestIdx = i;
        }

        return bestIdx;
    }

    private static double DistanceSquared(byte[] sample, double[] neuron)
    {
        var dr = sample[0] - neuron[0];
        var dg = sample[1] - neuron[1];
        var db = sample[2] - neuron[2];
        return dr * dr + dg * dg + db * db;
    }

    private static void MoveNeuronTowards(double[] neuron, byte[] sample, double learningRate)
    {
        neuron[0] += (sample[0] - neuron[0]) * learningRate;
        neuron[1] += (sample[1] - neuron[1]) * learningRate;
        neuron[2] += (sample[2] - neuron[2]) * learningRate;
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

