namespace OverTone.Theming;

/// <summary>
/// A complete, WCAG-aware UI color scheme for a single <see cref="ThemeMode"/>: a set of semantic roles
/// (<see cref="ColorRole"/>), each with a color and a matching "on" color. Access roles via the named
/// properties (<see cref="Primary"/>, <see cref="OnPrimary"/>, …) or iterate <see cref="Roles"/>.
/// Build one with <see cref="FromSeed(Rgb, SchemeOptions)"/> or
/// <see cref="PaletteSchemeExtensions.BuildScheme"/>; serialize it with the <c>AsCss</c> helpers.
/// </summary>
/// <remarks>Creates a scheme from a resolved role map.</remarks>
public sealed class ColorScheme(ThemeMode mode, IReadOnlyDictionary<ColorRole, RoleColor> roles)
{
    private readonly IReadOnlyDictionary<ColorRole, RoleColor> _roles = roles;

    /// <summary>The mode this scheme is tuned for.</summary>
    public ThemeMode Mode { get; } = mode;

    /// <summary>All resolved roles, for iteration and token export.</summary>
    public IReadOnlyDictionary<ColorRole, RoleColor> Roles => _roles;

    /// <summary>Gets a role's resolved color if present.</summary>
    public bool TryGet(ColorRole role, out RoleColor color) => _roles.TryGetValue(role, out color);

    /// <summary>Builds a single scheme (the mode is taken from <paramref name="options"/>).</summary>
    public static ColorScheme FromSeed(Rgb seed, SchemeOptions? options = null)
        => SchemeBuilder.Build(seed, [], options ?? new SchemeOptions());

    /// <summary>Builds a single scheme from a hex seed (e.g. <c>"#3B82F6"</c>).</summary>
    public static ColorScheme FromSeed(string seedHex, SchemeOptions? options = null)
        => FromSeed(Rgb.FromHex(seedHex), options);

    /// <summary>Builds matching light and dark schemes from the same seed.</summary>
    public static ThemePair BuildThemePair(Rgb seed, SchemeOptions? options = null)
        => SchemeBuilder.BuildPair(seed, [], options ?? new SchemeOptions());

    /// <summary>Builds matching light and dark schemes from a hex seed.</summary>
    public static ThemePair BuildThemePair(string seedHex, SchemeOptions? options = null)
        => BuildThemePair(Rgb.FromHex(seedHex), options);

    /// <summary>
    /// Interpolates between two schemes at <paramref name="t"/> (0..1) in OkLab — for smoothly cross-fading
    /// a UI theme (e.g. as the now-playing track changes). Only roles present in <em>both</em> schemes are
    /// included; a role's ramp is interpolated when both sides expose matching steps, otherwise it is
    /// dropped. The result takes its <see cref="Mode"/> from whichever scheme is nearer
    /// (<paramref name="t"/> &lt; 0.5 → <paramref name="a"/>).
    /// </summary>
    public static ColorScheme Lerp(ColorScheme a, ColorScheme b, double t)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        t = Math.Clamp(t, 0.0, 1.0);

        var roles = new Dictionary<ColorRole, RoleColor>();
        foreach (var (role, ra) in a._roles)
        {
            if (!b._roles.TryGetValue(role, out var rb)) continue;

            IReadOnlyList<Shade>? ramp = null;
            if (ra.Ramp is { } rampA && rb.Ramp is { } rampB && rampA.Count == rampB.Count)
            {
                var shades = new List<Shade>(rampA.Count);
                for (var i = 0; i < rampA.Count; i++)
                    shades.Add(new Shade(rampA[i].Step, Rgb.Lerp(rampA[i].Color, rampB[i].Color, t)));
                ramp = shades;
            }

            roles[role] = new RoleColor(Rgb.Lerp(ra.Color, rb.Color, t), Rgb.Lerp(ra.On, rb.On, t), ramp);
        }

        return new ColorScheme(t < 0.5 ? a.Mode : b.Mode, roles);
    }

    private RoleColor Role(ColorRole role) => _roles.TryGetValue(role, out var c) ? c : default;

    /// <summary>The dominant brand color.</summary>
    public Rgb Primary => Role(ColorRole.Primary).Color;
    /// <summary>Text/icons on <see cref="Primary"/>.</summary>
    public Rgb OnPrimary => Role(ColorRole.Primary).On;

    /// <summary>The complementary accent.</summary>
    public Rgb Secondary => Role(ColorRole.Secondary).Color;
    /// <summary>Text/icons on <see cref="Secondary"/>.</summary>
    public Rgb OnSecondary => Role(ColorRole.Secondary).On;

    /// <summary>The third accent.</summary>
    public Rgb Tertiary => Role(ColorRole.Tertiary).Color;
    /// <summary>Text/icons on <see cref="Tertiary"/>.</summary>
    public Rgb OnTertiary => Role(ColorRole.Tertiary).On;

    /// <summary>The base page background.</summary>
    public Rgb Background => Role(ColorRole.Background).Color;
    /// <summary>Text/icons on <see cref="Background"/>.</summary>
    public Rgb OnBackground => Role(ColorRole.Background).On;

    /// <summary>A raised container surface.</summary>
    public Rgb Surface => Role(ColorRole.Surface).Color;
    /// <summary>Text/icons on <see cref="Surface"/>.</summary>
    public Rgb OnSurface => Role(ColorRole.Surface).On;

    /// <summary>A subtler container surface tint.</summary>
    public Rgb SurfaceVariant => Role(ColorRole.SurfaceVariant).Color;
    /// <summary>Text/icons on <see cref="SurfaceVariant"/>.</summary>
    public Rgb OnSurfaceVariant => Role(ColorRole.SurfaceVariant).On;

    /// <summary>The default body text/foreground color.</summary>
    public Rgb Neutral => Role(ColorRole.Neutral).Color;

    /// <summary>Borders, dividers, and disabled outlines.</summary>
    public Rgb Outline => Role(ColorRole.Outline).Color;

    /// <summary>Positive/confirmation status.</summary>
    public Rgb Success => Role(ColorRole.Success).Color;
    /// <summary>Text/icons on <see cref="Success"/>.</summary>
    public Rgb OnSuccess => Role(ColorRole.Success).On;

    /// <summary>Caution status.</summary>
    public Rgb Warning => Role(ColorRole.Warning).Color;
    /// <summary>Text/icons on <see cref="Warning"/>.</summary>
    public Rgb OnWarning => Role(ColorRole.Warning).On;

    /// <summary>Error/destructive status.</summary>
    public Rgb Error => Role(ColorRole.Error).Color;
    /// <summary>Text/icons on <see cref="Error"/>.</summary>
    public Rgb OnError => Role(ColorRole.Error).On;

    /// <summary>Informational status.</summary>
    public Rgb Info => Role(ColorRole.Info).Color;
    /// <summary>Text/icons on <see cref="Info"/>.</summary>
    public Rgb OnInfo => Role(ColorRole.Info).On;
}
