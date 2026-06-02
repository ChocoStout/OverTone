using Xunit;

namespace OverTone.Tests;

public class ImageValidationTests
{
    private static readonly PaletteGenerator Generator = new();

    [Fact]
    public void Recognizes_a_real_bmp()
    {
        var bmp = SyntheticImage.VerticalStripes(8, 8, ((10, 20, 30), 1.0));
        Assert.True(ImageValidation.IsSupportedImage(bmp));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]                       // PNG
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })]                                                // JPEG
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' })]      // GIF89a
    [InlineData(new byte[] { (byte)'B', (byte)'M', 0, 0 })]                                            // BMP
    public void Accepts_known_signatures(byte[] data) =>
        Assert.True(ImageValidation.IsSupportedImage(data));

    [Theory]
    [InlineData(new byte[] { })]                                                                       // empty
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 })]                                                // "MZ" — Windows executable
    [InlineData(new byte[] { (byte)'<', (byte)'h', (byte)'t', (byte)'m', (byte)'l' })]                 // HTML error page
    [InlineData(new byte[] { (byte)'#', (byte)'!', (byte)'/', (byte)'b' })]                            // shell script (#! ≠ #? HDR)
    public void Rejects_non_images(byte[] data) =>
        Assert.False(ImageValidation.IsSupportedImage(data));

    [Fact]
    public async Task Extraction_rejects_non_image_bytes()
    {
        // A renamed "image" that's really a script must be rejected before the decoder runs.
        var notAnImage = "#!/bin/sh\nrm -rf /\n"u8.ToArray();

        await Assert.ThrowsAsync<UnsupportedImageFormatException>(
            () => Generator.ExtractColorPaletteAsync(notAnImage, colorCount: 5));
    }
}
