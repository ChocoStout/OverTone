namespace OverTone;

/// <summary>
/// Validates that a byte buffer begins with a recognized image signature ("magic bytes") for a format
/// the decoder actually supports, <b>before</b> the bytes are handed to the decoder. This is a cheap,
/// defense-in-depth guard: it rejects mislabeled or hostile input (a renamed executable/script, a
/// truncated file, an HTML error page returned from a URL) rather than trusting a file extension or
/// letting the native decoder parse arbitrary data.
/// </summary>
public static class ImageValidation
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="data"/> starts with a supported image signature
    /// (PNG, JPEG, GIF, BMP, PSD, Radiance HDR, or PNM).
    /// </summary>
    /// <remarks>
    /// Signature detection cannot validate Targa (TGA), which has no reliable magic number, so TGA input
    /// is reported as unsupported even though the decoder can read it.
    /// </remarks>
    public static bool IsSupportedImage(ReadOnlySpan<byte> data)
    {
        // PNG: 89 'P' 'N' 'G' 0D 0A 1A 0A
        if (data.Length >= 8 &&
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            return true;

        // JPEG: FF D8 FF
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return true;

        // GIF: "GIF87a" or "GIF89a"
        if (data.Length >= 6 &&
            data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'8' &&
            (data[4] == (byte)'7' || data[4] == (byte)'9') && data[5] == (byte)'a')
            return true;

        // BMP: "BM"
        if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M')
            return true;

        // Photoshop PSD: "8BPS"
        if (data.Length >= 4 &&
            data[0] == (byte)'8' && data[1] == (byte)'B' && data[2] == (byte)'P' && data[3] == (byte)'S')
            return true;

        // Radiance HDR: "#?RADIANCE" / "#?RGBE"
        if (data.Length >= 2 && data[0] == (byte)'#' && data[1] == (byte)'?')
            return true;

        // PNM family (PBM/PGM/PPM): 'P' followed by a digit 1-6.
        if (data.Length >= 2 && data[0] == (byte)'P' && data[1] >= (byte)'1' && data[1] <= (byte)'6')
            return true;

        return false;
    }

    /// <summary>
    /// Throws <see cref="UnsupportedImageFormatException"/> unless <paramref name="data"/> starts with a
    /// supported image signature (see <see cref="IsSupportedImage"/>).
    /// </summary>
    /// <exception cref="UnsupportedImageFormatException">The data is empty or not a recognized image.</exception>
    public static void EnsureSupportedImage(ReadOnlySpan<byte> data)
    {
        if (IsSupportedImage(data))
            return;

        var preview = data.Length == 0
            ? "(empty)"
            : Convert.ToHexString(data[..Math.Min(8, data.Length)]);

        throw new UnsupportedImageFormatException(
            $"The input is not a recognized image (supported: PNG, JPEG, GIF, BMP, PSD, HDR, PNM). " +
            $"Leading bytes: {preview}.");
    }
}
