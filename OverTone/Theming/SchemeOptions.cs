namespace OverTone.Theming;

/// <summary>How secondary/tertiary accents are derived when they aren't supplied by extracted colors.</summary>
public enum Harmony
{
    /// <summary>Neighbors on the wheel (±30°). The most reliable, never-clashing default.</summary>
    Analogous,

    /// <summary>The opposite hue (180°). High contrast, but can look muted where the gamut is thin.</summary>
    Complementary,

    /// <summary>Two hues 120° away. Vivid and balanced.</summary>
    Triadic,

    /// <summary>Two hues ±150° away (the complement's neighbors). Softer than triadic.</summary>
    SplitComplementary,
}

/// <summary>How status colors (success/warning/error/info) relate to the scheme's primary.</summary>
public enum StatusHarmony
{
    /// <summary>Use the canonical anchors verbatim.</summary>
    None,

    /// <summary>Keep canonical hues but match the scheme's lightness/chroma "weight" (default).</summary>
    ToneMatch,

    /// <summary>Also nudge hue toward the primary (capped ±12°, clamped to each status's safe hue band).</summary>
    HueShift,
}

/// <summary>Tuning for <see cref="ColorScheme"/> generation. All values have sensible defaults.</summary>
public record SchemeOptions
{
    /// <summary>WCAG AA contrast for normal text.</summary>
    public const double AA = 4.5;

    /// <summary>WCAG AA contrast for large text and UI components.</summary>
    public const double AALarge = 3.0;

    /// <summary>WCAG AAA contrast.</summary>
    public const double AAA = 7.0;

    /// <summary>Which mode <see cref="ColorScheme.FromSeed(Rgb, SchemeOptions)"/> produces. Default <see cref="ThemeMode.Light"/>.</summary>
    public ThemeMode Mode { get; init; } = ThemeMode.Light;

    /// <summary>
    /// Target WCAG contrast ratio for "on" colors. Defaults to <see cref="AA"/> (4.5). Note: a vivid
    /// mid-tone role can't always reach <see cref="AAA"/> (7) for any on-color, so the builder may shift
    /// the role's own tone to comply and will keep whatever it can guarantee.
    /// </summary>
    public double ContrastTarget { get; init; } = AA;

    /// <summary>How to derive secondary/tertiary when extracted accents don't supply them.</summary>
    public Harmony Harmony { get; init; } = Harmony.Analogous;

    /// <summary>How status colors relate to the primary. Default <see cref="StatusHarmony.ToneMatch"/>.</summary>
    public StatusHarmony StatusHarmony { get; init; } = StatusHarmony.ToneMatch;

    /// <summary>Chroma of the neutral family (surfaces/text) in OkLab units — a tasteful "tinted gray". Default 0.02.</summary>
    public double NeutralChroma { get; init; } = 0.02;

    /// <summary>When true, every role also gets a 50…950 tonal ramp. Default false.</summary>
    public bool IncludeRamps { get; init; }
}
