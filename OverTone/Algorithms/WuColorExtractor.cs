using OverTone;
using StbImageSharp;

namespace OverTone.Algorithms;

/// <summary>
/// Implements Wu's color quantization algorithm (variance-based color cube splitting).
/// Produces compact palettes that preserve dominant colors with good perceptual quality.
/// </summary>
public class WuColorExtractor : IColorPaletteExtractor
{
    public PaletteAlgorithm Algorithm => PaletteAlgorithm.Wu;

    // Number of bits per channel used for the internal histogram (5 gives 32 bins per channel).
    private const int BitsPerChannel = 5;
    private const int SideSize = 1 << BitsPerChannel; // 32
    private const int TableSize = SideSize + 1; // 33 (for cumulative tables)
    private const int Shift = 8 - BitsPerChannel;

    public async Task<List<ColorPalette>> ExtractColorPaletteAsync(byte[] imageData, int colorCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(colorCount);

        var image = ImageResult.FromMemory(imageData, ColorComponents.RedGreenBlueAlpha);
        var pixels = image.Data;

        // Histogram and moment arrays (names chosen for clarity)
        const int tableCount = TableSize * TableSize * TableSize;

        var histogramWeight = new int[tableCount];
        var momentR = new int[tableCount];
        var momentG = new int[tableCount];
        var momentB = new int[tableCount];
        var momentSquared = new double[tableCount];

        // Build histogram from image pixels
        BuildHistogram(pixels, Shift, histogramWeight, momentR, momentG, momentB, momentSquared);

        // Convert histogram into cumulative moments for fast box queries
        ComputeCumulativeMoments(histogramWeight, momentR, momentG, momentB, momentSquared);

        // Create initial box list and perform splitting based on variance
        var boxes = new Box[colorCount];

        boxes[0] = new Box
        {
            R0 = 1, 
            R1 = SideSize, 
            G0 = 1, 
            G1 = SideSize, 
            B0 = 1, 
            B1 = SideSize
        };

        var boxVariances = new double[colorCount];
        boxVariances[0] = Variance(boxes[0], histogramWeight, momentR, momentG, momentB, momentSquared);

        PerformSplitting(boxes, boxVariances, colorCount, histogramWeight, momentR, momentG, momentB, momentSquared);

        // Generate final palette entries from boxes
        var palette = GeneratePaletteFromBoxes(boxes, histogramWeight, momentR, momentG, momentB, colorCount);
        return await Task.FromResult(palette);
    }

    /// <summary>
    /// Build histogram and first-order moments from the raw image pixel data.
    /// </summary>
    /// <param name="pixels">Image pixel buffer in RGBA byte order.</param>
    /// <param name="shift">Right-shift applied to 8-bit components to reduce precision (bits per channel reduction).</param>
    /// <param name="histogramWeight">Output histogram counts per reduced color bin (flattened).</param>
    /// <param name="momentR">Output cumulative red channel sums per bin (first-order moment).</param>
    /// <param name="momentG">Output cumulative green channel sums per bin (first-order moment).</param>
    /// <param name="momentB">Output cumulative blue channel sums per bin (first-order moment).</param>
    /// <param name="momentSquared">Output sum-of-squares per bin (used for variance computation).</param>
    private static void BuildHistogram(byte[] pixels, int shift, int[] histogramWeight, int[] momentR, int[] momentG, int[] momentB, double[] momentSquared)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];

            if (alpha <= 128) 
                continue;

            var r = pixels[i] >> shift;
            var g = pixels[i + 1] >> shift;
            var b = pixels[i + 2] >> shift;

            var idx = Index(r + 1, g + 1, b + 1);

            histogramWeight[idx]++;

            momentR[idx] += pixels[i];
            momentG[idx] += pixels[i + 1];
            momentB[idx] += pixels[i + 2];

            momentSquared[idx] += pixels[i] * (double)pixels[i] + pixels[i + 1] * (double)pixels[i + 1] + pixels[i + 2] * (double)pixels[i + 2];
        }
    }

    /// <summary>
    /// Compute cumulative (integral) moments across the 3D histogram so that box queries become O(1).
    /// </summary>
    /// <param name="histogramWeight">Histogram counts per reduced color bin (flattened). The array is updated in-place
    /// to contain cumulative counts.</param>
    /// <param name="momentR">Red channel moments; updated in-place to cumulative moments.</param>
    /// <param name="momentG">Green channel moments; updated in-place to cumulative moments.</param>
    /// <param name="momentB">Blue channel moments; updated in-place to cumulative moments.</param>
    /// <param name="momentSquared">Sum-of-squares moments; updated in-place to cumulative moments.</param>
    private static void ComputeCumulativeMoments(int[] histogramWeight, int[] momentR, int[] momentG, int[] momentB, double[] momentSquared)
    {
        for (var r = 1; r <= SideSize; r++)
        {
            for (var g = 1; g <= SideSize; g++)
            {
                var sumW = 0;
                var sumR = 0;
                var sumG = 0;
                var sumB = 0;
                var sumM2 = 0.0;

                for (var b = 1; b <= SideSize; b++)
                {
                    var idx = Index(r, g, b);

                    sumW += histogramWeight[idx];
                    sumR += momentR[idx];
                    sumG += momentG[idx];
                    sumB += momentB[idx];
                    sumM2 += momentSquared[idx];

                    var prev = Index(r - 1, g, b);

                    histogramWeight[idx] = histogramWeight[prev] + sumW;

                    momentR[idx] = momentR[prev] + sumR;
                    momentG[idx] = momentG[prev] + sumG;
                    momentB[idx] = momentB[prev] + sumB;

                    momentSquared[idx] = momentSquared[prev] + sumM2;
                }
            }
        }
    }

    /// <summary>
    /// Perform iterative splitting of boxes to reach up to the requested box count.
    /// </summary>
    /// <param name="boxes">Array holding the partition boxes; boxes[0] contains the initial cube and subsequent entries will be populated.</param>
    /// <param name="boxVariances">Array of variances corresponding to each box; updated in-place during splitting.</param>
    /// <param name="targetBoxes">Desired number of boxes (colors) to produce.</param>
    /// <param name="weight">Cumulative histogram counts (cumulative moments).</param>
    /// <param name="mr">Cumulative red moments.</param>
    /// <param name="mg">Cumulative green moments.</param>
    /// <param name="mb">Cumulative blue moments.</param>
    /// <param name="m2"></param>
    private static void PerformSplitting(Box[] boxes, double[] boxVariances, int targetBoxes, int[] weight, int[] mr, int[] mg, int[] mb, double[] m2)
    {
        for (var i = 1; i < targetBoxes; i++)
        {
            var maxVariance = 0.0;
            var splitIndex = -1;

            for (var j = 0; j < i; j++)
            {
                if (!(boxVariances[j] > maxVariance)) 
                    continue;

                maxVariance = boxVariances[j];
                splitIndex = j;
            }

            if (splitIndex == -1 || maxVariance <= 0.0)
                break;

            var boxA = boxes[splitIndex];
            var boxB = new Box();

            if (!TrySplitBox(boxA, ref boxB, weight, mr, mg, mb))
                break;

            boxVariances[splitIndex] = Variance(boxA, weight, mr, mg, mb, m2);
            boxes[splitIndex] = boxA;
            boxes[i] = boxB;
            boxVariances[i] = Variance(boxB, weight, mr, mg, mb, m2);
        }
    }

    /// <summary>
    /// Generate ColorPalette entries from the final partition boxes.
    /// </summary>
    /// <param name="boxes">Partition boxes produced by splitting.</param>
    /// <param name="weight">Cumulative histogram counts.</param>
    /// <param name="mr">Cumulative red moments.</param>
    /// <param name="mg">Cumulative green moments.</param>
    /// <param name="mb">Cumulative blue moments.</param>
    /// <param name="colorCount">Number of palette entries requested.</param>
    /// <returns>List of ColorPalette entries ordered by pixel frequency.</returns>
    private static List<ColorPalette> GeneratePaletteFromBoxes(Box[] boxes, int[] weight, int[] mr, int[] mg, int[] mb, int colorCount)
    {
        var palette = new List<ColorPalette>();
        for (var i = 0; i < colorCount; i++)
        {
            var box = boxes[i];

            var wt = Volume(weight, box);

            if (wt == 0)
            {
                palette.Add(new ColorPalette { R = 0, G = 0, B = 0, PixelCount = 0 });
                continue;
            }

            var rsum = Volume(mr, box);
            var gsum = Volume(mg, box);
            var bsum = Volume(mb, box);

            var r8 = (byte)Math.Clamp((int)Math.Round(rsum / (double)wt), 0, 255);
            var g8 = (byte)Math.Clamp((int)Math.Round(gsum / (double)wt), 0, 255);
            var b8 = (byte)Math.Clamp((int)Math.Round(bsum / (double)wt), 0, 255);

            palette.Add(new ColorPalette { R = r8, G = g8, B = b8, PixelCount = wt });
        }

        return palette.OrderByDescending(p => p.PixelCount).Take(colorCount).ToList();
    }

    /// <summary>
    /// Compute a flattened 1D array index for the 3D histogram table coordinates.
    /// Coordinates are expected to be in the range [0..TableSize-1].
    /// </summary>
    private static int Index(int r, int g, int b) => (r * TableSize + g) * TableSize + b;

    /// <summary>
    /// Query the summed integer moment (for example histogram count or channel sum)
    /// inside the inclusive 3D box using the cumulative moments table.
    /// </summary>
    private static int Volume(int[] moment, Box box)
    {
        return moment[Index(box.R1, box.G1, box.B1)]
            - moment[Index(box.R1, box.G1, box.B0 - 1)]
            - moment[Index(box.R1, box.G0 - 1, box.B1)]
            + moment[Index(box.R1, box.G0 - 1, box.B0 - 1)]
            - moment[Index(box.R0 - 1, box.G1, box.B1)]
            + moment[Index(box.R0 - 1, box.G1, box.B0 - 1)]
            + moment[Index(box.R0 - 1, box.G0 - 1, box.B1)]
            - moment[Index(box.R0 - 1, box.G0 - 1, box.B0 - 1)];
    }

    /// <summary>
    /// Query the summed floating-point moment (for example sum-of-squares) inside the inclusive 3D box
    /// using the cumulative moments table.
    /// </summary>
    private static double Volume(double[] moment, Box box)
    {
        return moment[Index(box.R1, box.G1, box.B1)]
            - moment[Index(box.R1, box.G1, box.B0 - 1)]
            - moment[Index(box.R1, box.G0 - 1, box.B1)]
            + moment[Index(box.R1, box.G0 - 1, box.B0 - 1)]
            - moment[Index(box.R0 - 1, box.G1, box.B1)]
            + moment[Index(box.R0 - 1, box.G1, box.B0 - 1)]
            + moment[Index(box.R0 - 1, box.G0 - 1, box.B1)]
            - moment[Index(box.R0 - 1, box.G0 - 1, box.B0 - 1)];
    }

    /// <summary>
    /// Compute the variance of colors inside the given box using precomputed moments.
    /// Returns a non-negative double representing the sum-of-squares variance used by Wu's algorithm.
    /// </summary>
    private static double Variance(Box box, int[] weightMoment, int[] momentR, int[] momentG, int[] momentB, double[] momentSquared)
    {
        // Retrieve summed moments for the region defined by the box.
        var sumR = Volume(momentR, box);
        var sumG = Volume(momentG, box);
        var sumB = Volume(momentB, box);
        var sumSquares = Volume(momentSquared, box);
        var pixelCount = Volume(weightMoment, box);

        if (pixelCount <= 0)
            return 0.0;

        // Use double for intermediate arithmetic to avoid integer overflow and improve precision.
        var dr = (double)sumR;
        var dg = (double)sumG;
        var db = (double)sumB;

        // variance = sumSquares - (sumR^2 + sumG^2 + sumB^2) / pixelCount
        var numerator = dr * dr + dg * dg + db * db;
        var variance = sumSquares - numerator / pixelCount;

        // Numerical safety: variance should not be negative.
        return variance < 0 ? 0.0 : variance;
    }

    private static bool TrySplitBox(Box box, ref Box newBox, int[] wt, int[] mr, int[] mg, int[] mb)
    {
        var varR = Maximize(box, Axis.R, out var cutR, wt, mr, mg, mb);
        var varG = Maximize(box, Axis.G, out var cutG, wt, mr, mg, mb);
        var varB = Maximize(box, Axis.B, out var cutB, wt, mr, mg, mb);

        Axis chosen;
        int cut;

        if (varR >= varG && varR >= varB)
        {
            chosen = Axis.R; cut = cutR;
        }
        else if (varG >= varR && varG >= varB)
        {
            chosen = Axis.G; cut = cutG;
        }
        else
        {
            chosen = Axis.B; cut = cutB;
        }

        if (cut < 0) return false;

        newBox = new Box();

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (chosen)
        {
            case Axis.R:
                newBox.R0 = cut + 1; newBox.R1 = box.R1;
                newBox.G0 = box.G0; newBox.G1 = box.G1;
                newBox.B0 = box.B0; newBox.B1 = box.B1;
                box.R1 = cut;
                break;
            case Axis.G:
                newBox.G0 = cut + 1; newBox.G1 = box.G1;
                newBox.R0 = box.R0; newBox.R1 = box.R1;
                newBox.B0 = box.B0; newBox.B1 = box.B1;
                box.G1 = cut;
                break;
            default:
            {
                newBox.B0 = cut + 1; newBox.B1 = box.B1;
                newBox.R0 = box.R0; newBox.R1 = box.R1;
                newBox.G0 = box.G0; newBox.G1 = box.G1;
                box.B1 = cut;
                break;
            }
        }

        return true;
    }

    private static double Maximize(Box box, Axis axis, out int bestCut, int[] wt, int[] mr, int[] mg, int[] mb)
    {
        bestCut = -1;
        var bestScore = 0.0;

        var wholeR = Volume(mr, box);
        var wholeG = Volume(mg, box);
        var wholeB = Volume(mb, box);
        var wholeW = Volume(wt, box);

        if (wholeW == 0) 
            return 0.0;

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (axis)
        {
            case Axis.R:
            {
                var r0 = box.R0; var r1 = box.R1;
                for (var r = r0; r < r1; r++)
                {
                    // Temporarily split at r to query lower/upper half volumes.
                    box.R1 = r;
                    box.R0 = r0;
                    var wr = Volume(wt, box);

                    box.R0 = r + 1;
                    box.R1 = r1;
                    var wr2 = wholeW - wr;
                    if (wr == 0 || wr2 == 0) continue;

                    box.R1 = r;
                    box.R0 = r0;
                    var rsum1 = Volume(mr, box);
                    var gsum1 = Volume(mg, box);
                    var bsum1 = Volume(mb, box);

                    var rsum2 = wholeR - rsum1;
                    var gsum2 = wholeG - gsum1;
                    var bsum2 = wholeB - bsum1;

                    var score = (rsum1 * rsum1 + gsum1 * gsum1 + bsum1 * bsum1) / wr
                                + (rsum2 * rsum2 + gsum2 * gsum2 + bsum2 * bsum2) / wr2;

                    if (!(score > bestScore)) 
                        continue;

                    bestScore = score;
                    bestCut = r;
                }
                // Restore original bounds.
                box.R0 = r0; box.R1 = r1;
                break;
            }
            case Axis.G:
            {
                var g0 = box.G0; var g1 = box.G1;
                for (var g = g0; g < g1; g++)
                {
                    box.G1 = g;
                    box.G0 = g0;
                    var w1 = Volume(wt, box);

                    box.G0 = g + 1;
                    box.G1 = g1;
                    var w2 = wholeW - w1;
                    if (w1 == 0 || w2 == 0) continue;

                    box.G1 = g;
                    box.G0 = g0;
                    var r1v = Volume(mr, box); var g1v = Volume(mg, box); var b1v = Volume(mb, box);
                    var r2v = wholeR - r1v; var g2v = wholeG - g1v; var b2v = wholeB - b1v;

                    var score = (r1v * r1v + g1v * g1v + b1v * b1v) / w1 + (r2v * r2v + g2v * g2v + b2v * b2v) / w2;

                    if (!(score > bestScore)) 
                        continue;
                    bestScore = score;
                    bestCut = g;
                }
                box.G0 = g0; box.G1 = g1;
                break;
            }
            // axis B
            default:
            {
                var b0 = box.B0; var b1 = box.B1;
                for (var b = b0; b < b1; b++)
                {
                    box.B1 = b;
                    box.B0 = b0;
                    var w1 = Volume(wt, box);

                    box.B0 = b + 1;
                    box.B1 = b1;
                    var w2 = wholeW - w1;
                    if (w1 == 0 || w2 == 0) continue;

                    box.B1 = b;
                    box.B0 = b0;
                    var r1v = Volume(mr, box); var g1v = Volume(mg, box); var b1v = Volume(mb, box);
                    var r2v = wholeR - r1v; var g2v = wholeG - g1v; var b2v = wholeB - b1v;

                    var score = (r1v * r1v + g1v * g1v + b1v * b1v) / w1 + (r2v * r2v + g2v * g2v + b2v * b2v) / w2;

                    if (!(score > bestScore)) 
                        continue;

                    bestScore = score;
                    bestCut = b;
                }
                box.B0 = b0; box.B1 = b1;
                break;
            }
        }

        return bestScore;
    }
}
