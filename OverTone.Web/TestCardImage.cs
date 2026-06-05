namespace OverTone.Web;

/// <summary>
/// A built-in, fully synthetic image with a fixed, documented palette — 12 vivid vertical bands.
/// Because the colors are known, it lets a visitor see "the right answer" without uploading anything.
/// (Mirrors the console sample's test card; kept self-contained so the web project needs no extra refs.)
/// </summary>
internal static class TestCardImage
{
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

    /// <summary>Builds the test card as an in-memory 24-bit BMP (decodable by StbImageSharp and browsers).</summary>
    public static byte[] CreateBmp()
    {
        var bandWidth = Width / Colors.Length;
        var rowStride = (Width * 3 + 3) / 4 * 4;
        var pixelDataSize = rowStride * Height;
        const int headerSize = 14 + 40;
        var buffer = new byte[headerSize + pixelDataSize];

        buffer[0] = (byte)'B';
        buffer[1] = (byte)'M';
        WriteInt32(buffer, 2, buffer.Length);
        WriteInt32(buffer, 10, headerSize);
        WriteInt32(buffer, 14, 40);
        WriteInt32(buffer, 18, Width);
        WriteInt32(buffer, 22, Height);
        WriteInt16(buffer, 26, 1);
        WriteInt16(buffer, 28, 24);
        WriteInt32(buffer, 34, pixelDataSize);
        WriteInt32(buffer, 38, 2835);
        WriteInt32(buffer, 42, 2835);

        for (var y = 0; y < Height; y++)
        {
            var rowStart = headerSize + (Height - 1 - y) * rowStride; // BMP rows are bottom-up
            for (var x = 0; x < Width; x++)
            {
                var band = Math.Min(x / bandWidth, Colors.Length - 1);
                var c = Colors[band];
                var o = rowStart + x * 3;
                buffer[o] = c.B;
                buffer[o + 1] = c.G;
                buffer[o + 2] = c.R;
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
