namespace OverTone.Theming;

/// <summary>
/// Bridges an extracted palette to the theming layer: the most-dominant color (index 0) becomes the
/// seed and the rest are offered as accent candidates for the secondary/tertiary roles.
/// </summary>
public static class PaletteSchemeExtensions
{
    /// <summary>Builds a single <see cref="ColorScheme"/> (mode from <paramref name="options"/>).</summary>
    public static ColorScheme BuildScheme(this IReadOnlyList<ColorPalette> palette, SchemeOptions? options = null)
    {
        var (seed, accents) = Split(palette);
        return SchemeBuilder.Build(seed, accents, options ?? new SchemeOptions());
    }

    /// <summary>Builds matching light + dark schemes from the palette's dominant color.</summary>
    public static ThemePair BuildThemePair(this IReadOnlyList<ColorPalette> palette, SchemeOptions? options = null)
    {
        var (seed, accents) = Split(palette);
        return SchemeBuilder.BuildPair(seed, accents, options ?? new SchemeOptions());
    }

    private static (Rgb Seed, List<Rgb> Accents) Split(IReadOnlyList<ColorPalette> palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (palette.Count == 0)
            throw new ArgumentException("Cannot build a scheme from an empty palette.", nameof(palette));

        var seed = new Rgb(palette[0].R, palette[0].G, palette[0].B);
        var accents = new List<Rgb>(Math.Max(0, palette.Count - 1));
        for (var i = 1; i < palette.Count; i++)
            accents.Add(new Rgb(palette[i].R, palette[i].G, palette[i].B));
        return (seed, accents);
    }
}
