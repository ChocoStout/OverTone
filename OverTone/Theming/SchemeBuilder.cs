using OverTone.Processing;

namespace OverTone.Theming;

/// <summary>
/// Synthesizes a WCAG-aware <see cref="ColorScheme"/> from a seed color (plus optional accents) in
/// OkLCh. This is the "harmonize" path — unlike extraction it deliberately normalizes chroma into a
/// usable range and forces every (role, on-color) pairing to meet the contrast target. Deterministic.
/// </summary>
public static class SchemeBuilder
{
    private const double FallbackHue = 260.0;       // a calm blue, used when the seed has no usable hue
    private const double MinPrimaryChroma = 0.05;   // keep the primary visibly colored
    private const double MaxPrimaryChroma = 0.18;   // soft cap so buttons aren't neon
    private const double NeutralSeedChroma = 0.02;  // below this the seed is treated as neutral

    // Per-mode OkLab lightness/chroma targets (validated against WCAG contrast in review).
    private readonly record struct Targets(
        double BgL, double SurfL, double SvL, double PrimL, double PrimCScale,
        double NeutralL, double OutlineL, double StatusLLift, double StatusCScale);

    private static Targets ForMode(ThemeMode mode) => mode == ThemeMode.Dark
        ? new Targets(0.15, 0.21, 0.27, 0.72, 0.90, 0.92, 0.60, 0.12, 0.90)
        : new Targets(0.99, 0.97, 0.93, 0.55, 0.95, 0.25, 0.55, 0.00, 1.00);

    /// <summary>Builds a single scheme for <paramref name="o"/>.<see cref="SchemeOptions.Mode"/>.</summary>
    public static ColorScheme Build(Rgb seed, IReadOnlyList<Rgb> accents, SchemeOptions o)
    {
        var t = ForMode(o.Mode);
        var (hue, c0) = ResolveSeed(seed, accents);
        var roles = new Dictionary<ColorRole, RoleColor>();

        // Neutral family (background, surfaces, body text, outline) at the seed hue, near-neutral chroma.
        var nc = Math.Max(0.0, o.NeutralChroma);
        var background = Lch(t.BgL, nc * 0.5, hue);
        var surface = Lch(t.SurfL, nc, hue);
        var surfaceVariant = Lch(t.SvL, nc * 1.5, hue);
        var neutralText = EnsureContrast(Lch(t.NeutralL, nc, hue), background, o.ContrastTarget);
        var outline = Lch(t.OutlineL, nc, hue);

        roles[ColorRole.Background] = new RoleColor(background, neutralText);
        roles[ColorRole.Surface] = new RoleColor(surface, neutralText);
        roles[ColorRole.SurfaceVariant] = new RoleColor(surfaceVariant, neutralText);
        roles[ColorRole.Neutral] = new RoleColor(neutralText, background);
        roles[ColorRole.Outline] = new RoleColor(outline, neutralText);

        // Primary, then secondary/tertiary from distinct accents (else harmony rotation).
        var primaryC = c0 * t.PrimCScale;
        roles[ColorRole.Primary] = ResolveRole(Lch(t.PrimL, primaryC, hue), o.ContrastTarget);

        var offsets = Offsets(o.Harmony);
        var usedHues = new List<double> { hue };
        var secondaryHue = PickHue(accents, usedHues, hue + offsets[0]);
        usedHues.Add(secondaryHue);
        var tertiaryHue = PickHue(accents, usedHues, hue + offsets[1]);

        roles[ColorRole.Secondary] = ResolveRole(Lch(t.PrimL, primaryC, secondaryHue), o.ContrastTarget);
        roles[ColorRole.Tertiary] = ResolveRole(Lch(t.PrimL, primaryC, tertiaryHue), o.ContrastTarget);

        // Status colors — canonical anchors, image-independent so "red = error" always holds.
        AddStatus(roles, ColorRole.Error, 0.55, 0.18, 29, hue, c0, t, o);
        AddStatus(roles, ColorRole.Warning, 0.80, 0.15, 80, hue, c0, t, o);
        AddStatus(roles, ColorRole.Success, 0.62, 0.14, 150, hue, c0, t, o);
        AddStatus(roles, ColorRole.Info, 0.60, 0.14, 255, hue, c0, t, o);

        if (o.IncludeRamps)
            foreach (var role in RampedRoles)
                if (roles.TryGetValue(role, out var rc))
                {
                    var (_, rampC, rampH) = ColorMetrics.RgbToOkLch(rc.Color.R, rc.Color.G, rc.Color.B);
                    roles[role] = rc with { Ramp = BuildRamp(rampH, rampC) };
                }

        return new ColorScheme(o.Mode, roles);
    }

    /// <summary>Builds matching light + dark schemes from the same seed (sibling hues).</summary>
    public static ThemePair BuildPair(Rgb seed, IReadOnlyList<Rgb> accents, SchemeOptions o) =>
        new(Build(seed, accents, o with { Mode = ThemeMode.Light }),
            Build(seed, accents, o with { Mode = ThemeMode.Dark }));

    // ---- helpers ----

    private static Rgb Lch(double l, double c, double h)
    {
        var (r, g, b) = ColorMetrics.OkLchToRgb(l, c, h);
        return new Rgb(r, g, b);
    }

    private static (double Hue, double C0) ResolveSeed(Rgb seed, IReadOnlyList<Rgb> accents)
    {
        var (_, sc, sh) = ColorMetrics.RgbToOkLch(seed.R, seed.G, seed.B);
        if (sc >= NeutralSeedChroma)
            return (sh, Math.Clamp(sc, MinPrimaryChroma, MaxPrimaryChroma));

        // Near-neutral seed: borrow the hue of the most-chromatic accent, else fall back.
        foreach (var a in OrderedAccents(accents))
        {
            var (_, ac, ah) = ColorMetrics.RgbToOkLch(a.R, a.G, a.B);
            if (ac >= 0.03) return (ah, Math.Clamp(ac, MinPrimaryChroma, MaxPrimaryChroma));
        }
        return (FallbackHue, 0.12);
    }

    /// <summary>Picks a distinct accent hue (&gt;25° from every used hue) or the synthesized fallback.</summary>
    private static double PickHue(IReadOnlyList<Rgb> accents, List<double> usedHues, double fallback)
    {
        foreach (var a in OrderedAccents(accents))
        {
            var (_, ac, ah) = ColorMetrics.RgbToOkLch(a.R, a.G, a.B);
            if (ac < 0.03) continue;
            if (usedHues.TrueForAll(u => HueDiff(u, ah) > 25.0))
                return ah;
        }
        return Normalize(fallback);
    }

    private static IEnumerable<Rgb> OrderedAccents(IReadOnlyList<Rgb> accents) => accents
        .Select((c, i) => (c, i, lch: ColorMetrics.RgbToOkLch(c.R, c.G, c.B)))
        .OrderByDescending(x => x.lch.C)
        .ThenBy(x => x.lch.h)
        .ThenBy(x => x.i)
        .Select(x => x.c);

    private static double[] Offsets(Harmony harmony) => harmony switch
    {
        Harmony.Complementary => [180.0, 30.0],
        Harmony.Triadic => [120.0, -120.0],
        Harmony.SplitComplementary => [150.0, -150.0],
        _ => [30.0, -30.0], // Analogous
    };

    private static void AddStatus(
        Dictionary<ColorRole, RoleColor> roles, ColorRole role,
        double anchorL, double anchorC, double anchorH, double primaryHue, double c0, Targets t, SchemeOptions o)
    {
        var l = Math.Min(0.90, anchorL + t.StatusLLift);
        var c = anchorC * t.StatusCScale;
        var h = anchorH;

        if (o.StatusHarmony == StatusHarmony.ToneMatch)
            // Track the scheme's vibrancy without exceeding the (accessible) anchor chroma.
            c = Math.Clamp(c0 + 0.06, anchorC * 0.7, anchorC) * t.StatusCScale;
        else if (o.StatusHarmony == StatusHarmony.HueShift)
            h = ClampHueBand(role, anchorH + ShiftToward(anchorH, primaryHue, 12.0));

        roles[role] = ResolveRole(Lch(l, c, h), o.ContrastTarget);
    }

    /// <summary>
    /// Returns the role color paired with the best on-color. If the on-color can't meet the target
    /// against the given tone, shifts the role's lightness (both directions, nearest wins) until it can;
    /// if even that fails (e.g. AAA on a vivid mid-tone), keeps the best achievable rather than looping.
    /// </summary>
    private static RoleColor ResolveRole(Rgb color, double target)
    {
        if (BestOnContrast(color) >= target)
            return new RoleColor(color, BestOn(color));

        var (l0, c, h) = ColorMetrics.RgbToOkLch(color.R, color.G, color.B);

        double? up = null, down = null;
        for (var l = l0; l <= 1.0; l += 0.01)
            if (BestOnContrast(Lch(l, c, h)) >= target) { up = l; break; }
        for (var l = l0; l >= 0.0; l -= 0.01)
            if (BestOnContrast(Lch(l, c, h)) >= target) { down = l; break; }

        double chosen;
        if (up is { } u && down is { } d) chosen = u - l0 <= l0 - d ? u : d;
        else if (up is { } u2) chosen = u2;
        else if (down is { } d2) chosen = d2;
        else return new RoleColor(color, BestOn(color)); // best effort

        var fixedColor = Lch(chosen, c, h);
        return new RoleColor(fixedColor, BestOn(fixedColor));
    }

    private static Rgb EnsureContrast(Rgb fg, Rgb bg, double target)
    {
        var (r, g, b) = ColorMetrics.EnsureContrast(fg.R, fg.G, fg.B, bg.R, bg.G, bg.B, target);
        return new Rgb(r, g, b);
    }

    private static Rgb BestOn(Rgb c)
    {
        var (r, g, b) = ColorMetrics.BestOnColor(c.R, c.G, c.B);
        return new Rgb(r, g, b);
    }

    private static double BestOnContrast(Rgb c)
    {
        var on = BestOn(c);
        return ColorMetrics.ContrastRatio(on.R, on.G, on.B, c.R, c.G, c.B);
    }

    private static double HueDiff(double a, double b)
    {
        var d = Math.Abs(Normalize(a) - Normalize(b)) % 360.0;
        return Math.Min(d, 360.0 - d);
    }

    private static double ShiftToward(double from, double to, double maxDegrees)
    {
        var signed = ((to - from + 540.0) % 360.0) - 180.0; // shortest signed delta, [-180,180]
        return Math.Clamp(signed, -maxDegrees, maxDegrees);
    }

    private static double ClampHueBand(ColorRole role, double h)
    {
        var (lo, hi) = role switch
        {
            ColorRole.Error => (10.0, 45.0),
            ColorRole.Warning => (70.0, 95.0),
            ColorRole.Success => (130.0, 165.0),
            ColorRole.Info => (240.0, 270.0),
            _ => (0.0, 360.0),
        };
        return Math.Clamp(Normalize(h), lo, hi);
    }

    private static double Normalize(double h) => ((h % 360.0) + 360.0) % 360.0;

    private static readonly ColorRole[] RampedRoles =
    [
        ColorRole.Primary, ColorRole.Secondary, ColorRole.Tertiary, ColorRole.Neutral,
        ColorRole.Success, ColorRole.Warning, ColorRole.Error, ColorRole.Info,
    ];

    // Validated tonal-ramp shape: perceptually-even lightness 50→950, with a chroma curve that peaks
    // mid-ramp and tapers at both ends (a flat chroma ramp looks cheap at the tints, muddy at the shades).
    private static readonly (int Step, double L, double CMult)[] RampSteps =
    [
        (50, 0.97, 0.30), (100, 0.93, 0.45), (200, 0.85, 0.70), (300, 0.76, 0.90),
        (400, 0.68, 1.00), (500, 0.60, 1.00), (600, 0.52, 0.95), (700, 0.44, 0.85),
        (800, 0.36, 0.72), (900, 0.27, 0.55), (950, 0.21, 0.45),
    ];

    private static IReadOnlyList<Shade> BuildRamp(double hue, double peakChroma)
    {
        var shades = new List<Shade>(RampSteps.Length);
        foreach (var (step, l, mult) in RampSteps)
            shades.Add(new Shade(step, Lch(l, peakChroma * mult, hue)));
        return shades;
    }
}
