using OverTone;
using OverTone.Processing;
namespace OverTone.Algorithms;

/// <summary>
/// Wraps another IColorPaletteExtractor and post-processes its output to remove
/// perceptually near-duplicate colors using Delta-E, returning the top N distinct colors.
/// </summary>
/// <summary>
/// Wraps an <see cref="IColorPaletteExtractor"/> and post-processes the resulting palette
/// to remove perceptually near-duplicate colors using Delta‑E.
/// </summary>
/// <param name="inner">The underlying extractor used to produce an initial palette.</param>
/// <param name="minDeltaE">Minimum Delta‑E (CIE76) distance required between returned colors. Lower values
/// allow more similar colors; higher values increase distinctness. Default is 10.0.</param>
public class DedupeColorExtractor(IColorPaletteExtractor inner, double minDeltaE = 10.0) : IColorPaletteExtractor
{
    /// <summary>
    /// Identifies this extractor as a dedupe wrapper.
    /// </summary>
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.Dedupe;

    private readonly IColorPaletteExtractor _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    /// Runs the inner extractor to obtain a candidate palette, then removes perceptually
    /// similar colors (using Delta‑E in Lab space) until at most <paramref name="colorCount"/>
    /// distinct colors remain.
    /// </summary>
    /// <param name="imageData">Raw image bytes (for example a PNG or JPEG).</param>
    /// <param name="colorCount">Desired number of output colors.</param>
    /// <returns>A list of up to <paramref name="colorCount"/> perceptually distinct <see cref="ColorPalette"/> entries,
    /// ordered by pixel frequency (higher counts first).</returns>
    /// <remarks>
    /// The wrapper requests extra candidates from the inner extractor (at least 2× the requested
    /// <paramref name="colorCount"/>) to provide material for deduplication. If deduplication
    /// removes too many colors, the result is padded with the next most frequent colors from the
    /// inner palette until <paramref name="colorCount"/> entries are returned or no more candidates
    /// remain.
    /// </remarks>
    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        // Request a larger candidate set to give dedupe room to pick distinct colors.
        var candidateCount = Math.Max(colorCount, colorCount * 2);
        var basePalette = await _inner.ExtractColorPaletteAsync(imageData, candidateCount);

        // Apply perceptual dedupe, asking for up to colorCount entries
        var deduped = PalettePostProcessing.RemoveNearDuplicateByDeltaE(basePalette, minDeltaE, colorCount);

        // If dedupe produced fewer than requested, pad with the next most frequent
        if (deduped.Count >= colorCount)
            return deduped;

        foreach (var p in basePalette.Where(p => !deduped.Contains(p)))
        {
            deduped.Add(p);

            if (deduped.Count >= colorCount)
                break;
        }

        return deduped;
    }
}
