namespace OverTone.Sample;

/// <summary>
/// A built-in, fully synthetic test image with a fixed, documented palette. Because the colors are
/// known, it's ideal for sanity-checking extractor output and comparing algorithms by mean ΔE — there
/// is a "right answer" to measure against. Rendered as 12 equal-width vivid vertical bands.
/// </summary>
internal static class TestCard
{
    /// <summary>The exact ground-truth colors, left to right.</summary>
    public static readonly (string Name, byte R, byte G, byte B)[] Colors =
    [
        ("Red",     220,  20,  60),
        ("Orange",  240, 130,  30),
        ("Yellow",  245, 220,  40),
        ("Lime",    120, 200,  40),
        ("Green",    30, 160,  70),
        ("Teal",     20, 150, 150),
        ("Cyan",     40, 200, 220),
        ("Blue",     40,  90, 210),
        ("Indigo",   75,  40, 150),
        ("Magenta", 200,  40, 160),
        ("White",   245, 245, 240),
        ("Black",    25,  25,  30),
    ];

    private const int Width = 360;
    private const int Height = 120;

    /// <summary>Builds the test card as an in-memory 24-bit BMP (decodable by StbImageSharp).</summary>
    public static byte[] CreateBmp()
    {
        var bandWidth = Width / Colors.Length;
        return EncodeBmp24(Width, Height, x =>
        {
            var band = Math.Min(x / bandWidth, Colors.Length - 1);
            var (Name, R, G, B) = Colors[band];
            return (R, G, B);
        });
    }

    private static byte[] EncodeBmp24(int width, int height, Func<int, (byte R, byte G, byte B)> columnColor)
    {
        var rowStride = (width * 3 + 3) / 4 * 4;
        var pixelDataSize = rowStride * height;
        const int headerSize = 14 + 40;
        var buffer = new byte[headerSize + pixelDataSize];

        buffer[0] = (byte)'B';
        buffer[1] = (byte)'M';
        WriteInt32(buffer, 2, buffer.Length);
        WriteInt32(buffer, 10, headerSize);
        WriteInt32(buffer, 14, 40);
        WriteInt32(buffer, 18, width);
        WriteInt32(buffer, 22, height);
        WriteInt16(buffer, 26, 1);
        WriteInt16(buffer, 28, 24);
        WriteInt32(buffer, 34, pixelDataSize);
        WriteInt32(buffer, 38, 2835);
        WriteInt32(buffer, 42, 2835);

        for (var y = 0; y < height; y++)
        {
            var rowStart = headerSize + (height - 1 - y) * rowStride; // BMP rows are bottom-up
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = columnColor(x);
                var o = rowStart + x * 3;
                buffer[o] = b;
                buffer[o + 1] = g;
                buffer[o + 2] = r;
            }
        }

        return buffer;

        static void WriteInt32(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)value;
            buf[offset + 1] = (byte)(value >> 8);
            buf[offset + 2] = (byte)(value >> 16);
            buf[offset + 3] = (byte)(value >> 24);
        }

        static void WriteInt16(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)value;
            buf[offset + 1] = (byte)(value >> 8);
        }
    }
}
