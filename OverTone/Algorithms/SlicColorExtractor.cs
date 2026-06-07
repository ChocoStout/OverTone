using System.Collections.Concurrent;
using OverTone;
using OverTone.Processing;

namespace OverTone.Algorithms;

/// <summary>
/// SLIC (Simple Linear Iterative Clustering) superpixel extractor — the primary, region-aware
/// algorithm. It over-segments the image into compact superpixels (k-means in the joint color+space
/// domain with a localized search window), merges adjacent superpixels of near-identical color into
/// regions, then emits one representative (peak) color per region weighted by its area.
/// </summary>
/// <remarks>
/// This is "image space": pixel position participates in clustering, so an object (a sweatshirt, the
/// sky) becomes a region and contributes one color, the way a person would name it. Deterministic:
/// regular-grid seeds, a fixed iteration count, parallel per-pixel assignment, and a sequential
/// (order-independent) center update yield identical results regardless of the parallelism setting.
/// </remarks>
public sealed class SlicColorExtractor : ColorPaletteExtractorBase
{
    private const int ParallelThreshold = 8192;
    private const byte AlphaThreshold = 128;

    private readonly int _superpixelCount;
    private readonly double _compactness;
    private readonly int _iterations;
    private readonly double _mergeDeltaE;
    private readonly int _maxPixels;

    /// <summary>Creates the extractor with the given options (defaults when <c>null</c>).</summary>
    public SlicColorExtractor(SlicOptions? options = null)
    {
        var o = options ?? new SlicOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(o.SuperpixelCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(o.Iterations);
        _superpixelCount = o.SuperpixelCount;
        _compactness = Math.Max(1e-6, o.Compactness);
        _iterations = o.Iterations;
        _mergeDeltaE = o.RegionMergeDeltaE;
        _maxPixels = o.MaxPixels;
    }

    /// <inheritdoc />
    public override PaletteAlgorithm Algorithm => PaletteAlgorithm.Slic;

    /// <inheritdoc />
    protected override List<ColorPalette> ExtractCore(DecodedImage image, int colorCount, int maxDegreeOfParallelism)
        => ExtractCore(image, colorCount, maxDegreeOfParallelism, CancellationToken.None);

    /// <inheritdoc />
    protected override List<ColorPalette> ExtractCore(
        DecodedImage image, int colorCount, int maxDegreeOfParallelism, CancellationToken cancellationToken)
    {
        var img = DownscaleToMaxPixels(image, _maxPixels);
        var labels = Segment(img, maxDegreeOfParallelism, cancellationToken, out var superpixelCount);
        cancellationToken.ThrowIfCancellationRequested();
        return RegionPaletteBuilder.FromSlicLabels(
            labels, superpixelCount, img.Rgba, img.Width, img.Height, _mergeDeltaE);
    }

    /// <summary>Runs SLIC and returns a superpixel label per pixel (-1 for transparent/unassigned).</summary>
    private int[] Segment(DecodedImage img, int maxDegreeOfParallelism, CancellationToken cancellationToken, out int kActual)
    {
        int w = img.Width, h = img.Height, n = w * h;
        var rgba = img.Rgba;

        // Precompute Lab + visibility per grid pixel.
        var labL = new double[n];
        var labA = new double[n];
        var labB = new double[n];
        var visible = new bool[n];
        var visibleCount = 0;
        for (var i = 0; i < n; i++)
        {
            if (rgba[i * 4 + 3] <= AlphaThreshold) continue;
            var (l, a, b) = ColorMetrics.RgbToLab(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]);
            labL[i] = l; labA[i] = a; labB[i] = b;
            visible[i] = true;
            visibleCount++;
        }

        if (visibleCount == 0)
            throw new InvalidOperationException("No visible pixels found in the image.");

        // Seed grid: S ≈ √(N / K) spacing → cols×rows evenly distributed seeds.
        var k = Math.Min(_superpixelCount, visibleCount);
        var s = Math.Max(1, (int)Math.Round(Math.Sqrt((double)n / k)));
        var cols = Math.Max(1, (w + s - 1) / s);
        var rows = Math.Max(1, (h + s - 1) / s);
        kActual = cols * rows;
        var stepX = (double)w / cols;
        var stepY = (double)h / rows;
        var sDist = Math.Sqrt(stepX * stepY);
        var invS2 = _compactness * _compactness / (sDist * sDist); // (m/S)² spatial-vs-color weight.

        var sL = new double[kActual];
        var sA = new double[kActual];
        var sB = new double[kActual];
        var sX = new double[kActual];
        var sY = new double[kActual];
        var active = new bool[kActual];

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var seed = row * cols + col;
                var cx = Math.Min(w - 1, (int)((col + 0.5) * stepX));
                var cy = Math.Min(h - 1, (int)((row + 0.5) * stepY));
                var idx = FindVisibleNear(cx, cy, w, h, visible);
                if (idx < 0)
                {
                    sX[seed] = cx; sY[seed] = cy; // inactive: skipped during assignment.
                    continue;
                }
                active[seed] = true;
                sL[seed] = labL[idx]; sA[seed] = labA[idx]; sB[seed] = labB[idx];
                sX[seed] = idx % w; sY[seed] = idx / w;
            }
        }

        var labels = new int[n];
        Array.Fill(labels, -1);

        // One pixel's nearest seed among its grid cell and the 8 neighboring cells (the localized 2S
        // window, reframed per-pixel). Writes only labels[i], so this is safe to run in parallel.
        void Assign(int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (!visible[i]) { labels[i] = -1; continue; }
                var px = i % w;
                var py = i / w;
                var pc = Math.Min(cols - 1, (int)(px / stepX));
                var pr = Math.Min(rows - 1, (int)(py / stepY));

                var bestD = double.MaxValue;
                var bestSeed = -1;
                for (var dr = -1; dr <= 1; dr++)
                {
                    var rr = pr + dr;
                    if (rr < 0 || rr >= rows) continue;
                    for (var dc = -1; dc <= 1; dc++)
                    {
                        var cc = pc + dc;
                        if (cc < 0 || cc >= cols) continue;
                        var seed = rr * cols + cc;
                        if (!active[seed]) continue;

                        double dl = labL[i] - sL[seed], da = labA[i] - sA[seed], db = labB[i] - sB[seed];
                        double dx = px - sX[seed], dy = py - sY[seed];
                        var d = dl * dl + da * da + db * db + invS2 * (dx * dx + dy * dy);
                        if (d < bestD) { bestD = d; bestSeed = seed; }
                    }
                }
                labels[i] = bestSeed;
            }
        }

        for (var iter = 0; iter < _iterations; iter++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maxDegreeOfParallelism > 1 && n >= ParallelThreshold)
            {
                Parallel.ForEach(
                    Partitioner.Create(0, n),
                    new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
                    range => Assign(range.Item1, range.Item2));
            }
            else
            {
                Assign(0, n);
            }

            // Recompute seed centers as the mean of their members (sequential → deterministic).
            Array.Clear(sL); Array.Clear(sA); Array.Clear(sB); Array.Clear(sX); Array.Clear(sY);
            var cnt = new int[kActual];
            for (var i = 0; i < n; i++)
            {
                var seed = labels[i];
                if (seed < 0) continue;
                sL[seed] += labL[i]; sA[seed] += labA[i]; sB[seed] += labB[i];
                sX[seed] += i % w; sY[seed] += i / w;
                cnt[seed]++;
            }
            for (var seed = 0; seed < kActual; seed++)
            {
                if (cnt[seed] == 0) { active[seed] = false; continue; }
                active[seed] = true;
                sL[seed] /= cnt[seed]; sA[seed] /= cnt[seed]; sB[seed] /= cnt[seed];
                sX[seed] /= cnt[seed]; sY[seed] /= cnt[seed];
            }
        }

        return labels;
    }

    /// <summary>Returns the index of a visible pixel at or near (cx, cy), or -1 if none within radius 3.</summary>
    private static int FindVisibleNear(int cx, int cy, int w, int h, bool[] visible)
    {
        var idx = cy * w + cx;
        if (visible[idx]) return idx;

        for (var radius = 1; radius <= 3; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var ny = cy + dy;
                if (ny < 0 || ny >= h) continue;
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var nx = cx + dx;
                    if (nx < 0 || nx >= w) continue;
                    var nIdx = ny * w + nx;
                    if (visible[nIdx]) return nIdx;
                }
            }
        }
        return -1;
    }
}
