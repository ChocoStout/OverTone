namespace OverTone.Tests;

/// <summary>
/// Generates fully synthetic test images (no external or licensed assets) as in-memory 24-bit BMP
/// bytes. StbImageSharp decodes BMP, so these feed straight into the extractors. Hand-rolling the BMP
/// encoder keeps the test project dependency-free.
/// </summary>
internal static class SyntheticImage
{
    /// <summary>
    /// Builds an image of vertical stripes whose widths are proportional to the given weights. Useful
    /// for modelling "dominant background + small accents" images with known ground-truth colors.
    /// </summary>
    public static byte[] VerticalStripes(int width, int height,
        params ((byte R, byte G, byte B) Color, double Weight)[] regions)
    {
        var totalWeight = regions.Sum(r => r.Weight);

        // Resolve stripe widths proportional to weight; the last stripe absorbs any rounding remainder.
        var widths = new int[regions.Length];
        var assigned = 0;
        for (var i = 0; i < regions.Length; i++)
        {
            widths[i] = i == regions.Length - 1
                ? width - assigned
                : (int)Math.Round(regions[i].Weight / totalWeight * width);
            assigned += widths[i];
        }

        // Map each column to its region color.
        var columnColor = new (byte R, byte G, byte B)[width];
        var x = 0;
        for (var i = 0; i < regions.Length; i++)
            for (var c = 0; c < widths[i] && x < width; c++, x++)
                columnColor[x] = regions[i].Color;
        for (; x < width; x++)
            columnColor[x] = regions[^1].Color;

        return EncodeBmp24(width, height, (px, _) => columnColor[px]);
    }

    /// <summary>
    /// Builds an image with many distinct colors (a pseudo-random per-column sweep), useful for
    /// exercising quantizers that must reduce a large color count.
    /// </summary>
    public static byte[] ManyColors(int width, int height) =>
        EncodeBmp24(width, height, (x, _) =>
            ((byte)((x * 7) % 256), (byte)((x * 13 + 40) % 256), (byte)((x * 23 + 80) % 256)));

    /// <summary>
    /// Encodes a 24-bit, uncompressed, bottom-up BMP from a per-pixel color function. All multi-byte
    /// fields are written little-endian by hand, so the output is correct regardless of host endianness.
    /// </summary>
    private static byte[] EncodeBmp24(int width, int height, Func<int, int, (byte R, byte G, byte B)> colorAt)
    {
        var rowStride = (width * 3 + 3) / 4 * 4;   // rows padded to a 4-byte boundary
        var pixelDataSize = rowStride * height;
        const int headerSize = 14 + 40;            // BITMAPFILEHEADER + BITMAPINFOHEADER
        var fileSize = headerSize + pixelDataSize;

        var buffer = new byte[fileSize];

        // BITMAPFILEHEADER
        buffer[0] = (byte)'B';
        buffer[1] = (byte)'M';
        WriteInt32(buffer, 2, fileSize);
        WriteInt32(buffer, 10, headerSize);        // offset to pixel data

        // BITMAPINFOHEADER
        WriteInt32(buffer, 14, 40);                // header size
        WriteInt32(buffer, 18, width);
        WriteInt32(buffer, 22, height);            // positive height = bottom-up rows
        WriteInt16(buffer, 26, 1);                 // color planes
        WriteInt16(buffer, 28, 24);                // bits per pixel
        WriteInt32(buffer, 30, 0);                 // BI_RGB (no compression)
        WriteInt32(buffer, 34, pixelDataSize);
        WriteInt32(buffer, 38, 2835);              // ~72 DPI horizontal
        WriteInt32(buffer, 42, 2835);              // ~72 DPI vertical

        // Pixel data: bottom-up rows, BGR order.
        for (var y = 0; y < height; y++)
        {
            var rowStart = headerSize + (height - 1 - y) * rowStride;
            for (var px = 0; px < width; px++)
            {
                var (r, g, b) = colorAt(px, y);
                var o = rowStart + px * 3;
                buffer[o] = b;
                buffer[o + 1] = g;
                buffer[o + 2] = r;
            }
        }

        return buffer;

        static void WriteInt32(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        static void WriteInt16(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
