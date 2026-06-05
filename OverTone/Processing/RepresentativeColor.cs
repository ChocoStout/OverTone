using System.Runtime.InteropServices;
using OverTone;

namespace OverTone.Processing;

/// <summary>
/// Accumulates pixels and resolves a single representative ("peak") color for them — the modal color,
/// gently biased toward chroma — instead of the arithmetic mean. Averaging a region's pixels drifts to
/// a dull midpoint (the desaturation problem the image-space migration targets); the modal/peak color
/// preserves the region's actual, saturated identity. Deterministic, and resistant to stray noise
/// pixels because colors are binned before the vote.
/// </summary>
public sealed class RepresentativeColorAccumulator
{
    // 4 bits per channel: 16 levels each, up to 16×16×16 = 4096 bins. Stored sparsely (only populated
    // bins exist), so a small region costs little and a large one is bounded at 4096 entries.
    private readonly Dictionary<int, Bin> _bins = new();
    private int _count;

    private struct Bin
    {
        public int Count;
        public long SumR;
        public long SumG;
        public long SumB;
    }

    /// <summary>Adds one pixel to the accumulator.</summary>
    public void Add(byte r, byte g, byte b)
    {
        var key = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
        ref var bin = ref CollectionsMarshal.GetValueRefOrAddDefault(_bins, key, out _);
        bin.Count++;
        bin.SumR += r;
        bin.SumG += g;
        bin.SumB += b;
        _count++;
    }

    /// <summary>Number of pixels added so far.</summary>
    public int PixelCount => _count;

    /// <summary>True when no pixels have been added.</summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Resolves the representative color. Among the <paramref name="topBins"/> most-populous color bins
    /// it picks the one maximizing <c>population × (1 + chromaWeight × normalizedChroma)</c> — so a
    /// vivid bin can win, but only when it is also well-populated (a single stray neon pixel cannot) —
    /// then returns the mean of that winning bin's actual pixels (a real, smooth color, not the
    /// quantized bin center). Ties break deterministically on the bin key.
    /// </summary>
    /// <param name="topBins">How many of the most-populous bins to consider.</param>
    /// <param name="chromaWeight">Strength of the chroma bias (0 = pure mode, larger = more vivid).</param>
    public (byte R, byte G, byte B) Resolve(int topBins = 5, double chromaWeight = 0.5)
    {
        if (_count == 0)
            return (0, 0, 0);

        // Deterministic order: most-populous first, ties broken by bin key ascending.
        var ordered = _bins
            .OrderByDescending(kvp => kvp.Value.Count)
            .ThenBy(kvp => kvp.Key)
            .Take(topBins);

        var bestScore = double.NegativeInfinity;
        var bestKey = int.MaxValue;
        Bin best = default;

        foreach (var (key, bin) in ordered)
        {
            var r = (byte)(bin.SumR / bin.Count);
            var g = (byte)(bin.SumG / bin.Count);
            var b = (byte)(bin.SumB / bin.Count);

            var chroma = ColorMetrics.LabChroma(r, g, b);
            var chromaNorm = Math.Clamp(chroma / 90.0, 0.0, 1.0);
            var score = bin.Count * (1.0 + chromaWeight * chromaNorm);

            if (score > bestScore || (score == bestScore && key < bestKey))
            {
                bestScore = score;
                bestKey = key;
                best = bin;
            }
        }

        return (
            (byte)(best.SumR / best.Count),
            (byte)(best.SumG / best.Count),
            (byte)(best.SumB / best.Count));
    }

    /// <summary>Convenience: the representative color of a sequence of pixels.</summary>
    public static (byte R, byte G, byte B) Of(
        IEnumerable<(byte R, byte G, byte B)> pixels, int topBins = 5, double chromaWeight = 0.5)
    {
        var acc = new RepresentativeColorAccumulator();
        foreach (var (r, g, b) in pixels)
            acc.Add(r, g, b);
        return acc.Resolve(topBins, chromaWeight);
    }
}
