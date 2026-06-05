namespace OverTone.Theming;

/// <summary>
/// An opaque 24-bit sRGB color used by the theming layer. Distinct from <see cref="ColorPalette"/>,
/// which carries extraction metadata (pixel counts) that is meaningless for a synthesized theme color.
/// </summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>The color as a <c>#RRGGBB</c> hex string.</summary>
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Parses a <c>#RGB</c> or <c>#RRGGBB</c> hex string (the leading <c>#</c> is optional).</summary>
    /// <exception cref="FormatException">The string is not a valid hex color.</exception>
    public static Rgb FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var s = hex.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length == 3)
            s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        if (s.Length != 6)
            throw new FormatException($"'{hex}' is not a valid hex color (expected #RGB or #RRGGBB).");

        return new Rgb(
            Convert.ToByte(s.Substring(0, 2), 16),
            Convert.ToByte(s.Substring(2, 2), 16),
            Convert.ToByte(s.Substring(4, 2), 16));
    }

    /// <inheritdoc />
    public override string ToString() => Hex;
}
