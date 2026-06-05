namespace OverTone.Theming;

/// <summary>
/// A matched light + dark scheme generated from the same seed, so the two modes are siblings (same hues)
/// rather than independent themes. Serialize both at once with <c>AsCss()</c> to get a light <c>:root</c>
/// plus a dark override block.
/// </summary>
public sealed record ThemePair(ColorScheme Light, ColorScheme Dark);
