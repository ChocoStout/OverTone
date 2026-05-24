using OverTone;
namespace OverTone.Algorithms;

/// <summary>
/// Represents a container (box) of color points used by the Median Cut algorithm.
/// The box tracks the color bounds and can be split into two boxes at the median
/// of the channel with the largest range.
/// </summary>
public class ColorBox
{
    /// <summary>
    /// The list of color points contained in this box. Each point is a 3-element byte[]: [R, G, B].
    /// </summary>
    public List<byte[]> ColorPoints { get; }

    private int RedMin { get; set; }
    private int RedMax { get; set; }

    private int GreenMin { get; set; }
    private int GreenMax { get; set; }

    private int BlueMin { get; set; }
    private int BlueMax { get; set; }

    /// <summary>
    /// The largest per-channel range within this box (used to decide split axis).
    /// </summary>
    public int ColorRange => Math.Max(RedMax - RedMin, Math.Max(GreenMax - GreenMin, BlueMax - BlueMin));

    /// <summary>
    /// Average red component for the points in the box.
    /// </summary>
    public byte AverageRed { get; private set; }

    /// <summary>
    /// Average green component for the points in the box.
    /// </summary>
    public byte AverageGreen { get; private set; }

    /// <summary>
    /// Average blue component for the points in the box.
    /// </summary>
    public byte AverageBlue { get; private set; }

    /// <summary>
    /// Creates a new <see cref="ColorBox"/> containing the provided color points.
    /// </summary>
    /// <param name="colorPoints">A list of RGB color points (each a byte[3]).</param>
    public ColorBox(List<byte[]>? colorPoints)
    {
        ColorPoints = colorPoints ?? [];
        UpdateBoundsAndAverages();
    }

    // Recalculate bounds and averages for the current set of points
    private void UpdateBoundsAndAverages()
    {
        if (ColorPoints.Count == 0)
        {
            RedMin = GreenMin = BlueMin = 0;
            RedMax = GreenMax = BlueMax = 0;
            AverageRed = AverageGreen = AverageBlue = 0;
            return;
        }

        int redMin = 255, redMax = 0, greenMin = 255, greenMax = 0, blueMin = 255, blueMax = 0;
        long sumRed = 0, sumGreen = 0, sumBlue = 0;

        foreach (var p in ColorPoints)
        {
            var red = p[0];
            var green = p[1];
            var blue = p[2];

            if (red < redMin) 
                redMin = red;
            if (red > redMax)
                redMax = red;
            if (green < greenMin) 
                greenMin = green;
            if (green > greenMax)
                greenMax = green;
            if (blue < blueMin) 
                blueMin = blue;
            if (blue > blueMax) 
                blueMax = blue;

            sumRed += red;
            sumGreen += green;
            sumBlue += blue;
        }

        RedMin = redMin; RedMax = redMax; GreenMin = greenMin; GreenMax = greenMax; BlueMin = blueMin; BlueMax = blueMax;

        AverageRed = (byte)(sumRed / ColorPoints.Count);
        AverageGreen = (byte)(sumGreen / ColorPoints.Count);
        AverageBlue = (byte)(sumBlue / ColorPoints.Count);
    }

    /// <summary>
    /// Splits the current box into two boxes along the channel with the largest range.
    /// The split is performed at the median of the chosen channel.
    /// </summary>
    /// <returns>A tuple containing the left and right child boxes.</returns>
    public (ColorBox left, ColorBox right) Split()
    {
        if (ColorPoints.Count <= 1)
            return (new ColorBox([..ColorPoints]), new ColorBox([]));

        // Determine per-channel ranges
        var rRange = RedMax - RedMin;
        var gRange = GreenMax - GreenMin;
        var bRange = BlueMax - BlueMin;

        // Choose the channel with the largest spread to split on
        var channel = 0; // 0 = R, 1 = G, 2 = B

        if (gRange >= rRange && gRange >= bRange)
            channel = 1;
        else if (bRange >= rRange && bRange >= gRange)
            channel = 2;

        // Sort points by the chosen channel and split at the median
        var sorted = ColorPoints.OrderBy(p => p[channel]).ToList();
        var mid = sorted.Count / 2;

        var leftPoints = sorted.Take(mid).ToList();
        var rightPoints = sorted.Skip(mid).ToList();

        var leftBox = new ColorBox(leftPoints);
        var rightBox = new ColorBox(rightPoints);

        return (leftBox, rightBox);
    }
}
