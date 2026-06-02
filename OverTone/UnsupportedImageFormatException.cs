namespace OverTone;

/// <summary>
/// Thrown when image data does not begin with a recognized, supported image signature. Validating the
/// magic bytes up front means untrusted or mislabeled input (a renamed script, a truncated upload, an
/// HTML error page returned by a URL, etc.) is rejected before it ever reaches the image decoder.
/// </summary>
public sealed class UnsupportedImageFormatException : Exception
{
    /// <summary>Creates the exception with a descriptive message.</summary>
    public UnsupportedImageFormatException(string message) : base(message) { }
}
