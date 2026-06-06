using OverTone.Processing;
using OverTone.Theming;
using Xunit;

namespace OverTone.Tests;

/// <summary>
/// Tests for the semantic theme builder: the OkLCh/WCAG color math, and the guarantee that every
/// synthesized (role, on-color) pairing actually meets its contrast target in both light and dark.
/// </summary>
public class ThemingTests
{
    private static double Contrast(Rgb a, Rgb b) => ColorMetrics.ContrastRatio(a.R, a.G, a.B, b.R, b.G, b.B);

    private static IEnumerable<(string Pair, Rgb Color, Rgb On)> OnPairs(ColorScheme s) =>
    [
        ("primary", s.Primary, s.OnPrimary),
        ("secondary", s.Secondary, s.OnSecondary),
        ("tertiary", s.Tertiary, s.OnTertiary),
        ("background", s.Background, s.OnBackground),
        ("surface", s.Surface, s.OnSurface),
        ("surface-variant", s.SurfaceVariant, s.OnSurfaceVariant),
        ("success", s.Success, s.OnSuccess),
        ("warning", s.Warning, s.OnWarning),
        ("error", s.Error, s.OnError),
        ("info", s.Info, s.OnInfo),
        ("neutral-on-background", s.Background, s.Neutral),
    ];

    [Fact]
    public void OkLab_and_OkLch_round_trip_within_one_step()
    {
        byte[] levels = [0, 64, 128, 192, 255];
        foreach (var r in levels)
        foreach (var g in levels)
        foreach (var b in levels)
        {
            var (ll, la, lb) = ColorMetrics.RgbToOkLab(r, g, b);
            var (rr, rg, rb) = ColorMetrics.OkLabToRgb(ll, la, lb);
            Assert.True(Math.Abs(rr - r) <= 2 && Math.Abs(rg - g) <= 2 && Math.Abs(rb - b) <= 2,
                $"OkLab round-trip drifted for ({r},{g},{b}) -> ({rr},{rg},{rb})");

            var (cl, cc, ch) = ColorMetrics.RgbToOkLch(r, g, b);
            var (r2, g2, b2) = ColorMetrics.OkLchToRgb(cl, cc, ch);
            Assert.True(Math.Abs(r2 - r) <= 2 && Math.Abs(g2 - g) <= 2 && Math.Abs(b2 - b) <= 2,
                $"OkLch round-trip drifted for ({r},{g},{b}) -> ({r2},{g2},{b2})");
        }
    }

    [Fact]
    public void Wcag_contrast_and_luminance_match_known_values()
    {
        Assert.Equal(1.0, ColorMetrics.RelativeLuminance(255, 255, 255), 6);
        Assert.Equal(0.0, ColorMetrics.RelativeLuminance(0, 0, 0), 6);
        Assert.Equal(21.0, ColorMetrics.ContrastRatio(0, 0, 0, 255, 255, 255), 6);   // black vs white
        Assert.Equal(1.0, ColorMetrics.ContrastRatio(255, 255, 255, 255, 255, 255), 6);

        // #767676 on white is the canonical ~4.5:1 boundary gray (WebAIM).
        var midOnWhite = ColorMetrics.ContrastRatio(0x76, 0x76, 0x76, 255, 255, 255);
        Assert.InRange(midOnWhite, 4.4, 4.7);
    }

    [Theory]
    [InlineData(0.65, 0.35, 140.0)] // green: far out of gamut at this chroma
    [InlineData(0.70, 0.40, 30.0)]  // red
    [InlineData(0.55, 0.40, 264.0)] // blue
    public void GamutMap_reduces_chroma_but_preserves_hue_and_lightness(double l, double c, double h)
    {
        var (r, g, b) = ColorMetrics.OkLchToRgb(l, c, h);
        var (l2, c2, h2) = ColorMetrics.RgbToOkLch(r, g, b);

        var hueDelta = Math.Abs(((h2 - h + 540) % 360) - 180);
        Assert.True(hueDelta < 4.0, $"hue drifted {hueDelta:F2}° ({h} -> {h2})");
        Assert.True(l2 >= l - 0.03 && l2 <= l + 0.03, $"lightness drifted ({l} -> {l2})");
        Assert.True(c2 <= c + 1e-6, "chroma should not increase");
    }

    [Theory]
    [InlineData("#285AD2")] // blue
    [InlineData("#DC143C")] // red
    [InlineData("#1EA046")] // green
    [InlineData("#F5F5F0")] // near-white seed
    [InlineData("#19191E")] // near-black seed
    [InlineData("#808080")] // neutral seed (exercises the fallback hue)
    public void Every_role_on_pair_meets_AA_in_both_modes(string seedHex)
    {
        var pair = ColorScheme.BuildThemePair(seedHex);

        foreach (var scheme in new[] { pair.Light, pair.Dark })
        foreach (var (name, color, on) in OnPairs(scheme))
            Assert.True(Contrast(color, on) >= SchemeOptions.AA - 1e-6,
                $"{scheme.Mode} {name}: contrast {Contrast(color, on):F2} < {SchemeOptions.AA}");
    }

    [Fact]
    public void Scheme_is_deterministic()
    {
        var a = ColorScheme.BuildThemePair("#285AD2", new SchemeOptions { IncludeRamps = false });
        var b = ColorScheme.BuildThemePair("#285AD2", new SchemeOptions { IncludeRamps = false });
        Assert.Equal(a.AsCss(), b.AsCss());
    }

    [Fact]
    public void Neutral_seed_yields_a_chromatic_primary()
    {
        // A gray seed has no hue; the primary must still be a real, colored brand color (fallback hue).
        var gray = ColorScheme.FromSeed("#808080").Primary;
        var (_, grayC, _) = ColorMetrics.RgbToOkLch(gray.R, gray.G, gray.B);
        Assert.True(grayC > 0.04, $"neutral-seed primary should be chromatic, was C={grayC:F3}");

        // With a vivid accent available, the primary should borrow that accent's hue (reddish here).
        var palette = new List<ColorPalette>
        {
            new() { R = 0x80, G = 0x80, B = 0x80 },
            new() { R = 0xDC, G = 0x14, B = 0x3C },
        };
        var primary = palette.BuildScheme().Primary;
        var (_, c, h) = ColorMetrics.RgbToOkLch(primary.R, primary.G, primary.B);
        Assert.True(c > 0.04, "primary should be chromatic");
        var hueToRed = Math.Abs(((h - 29 + 540) % 360) - 180); // red ≈ 29° in OkLCh
        Assert.True(hueToRed < 35, $"primary hue {h:F0}° should be near the red accent");
    }

    [Fact]
    public void Near_black_seed_primary_is_usable()
    {
        // A near-black seed must not yield a near-black "primary" — its lightness is clamped up.
        var primary = ColorScheme.FromSeed("#0A0A0A").Primary;
        var (l, _, _) = ColorMetrics.RgbToOkLch(primary.R, primary.G, primary.B);
        Assert.True(l is > 0.35 and < 0.85, $"primary lightness {l:F2} should sit in a usable band");
    }

    [Fact]
    public void ThemePair_css_has_root_dark_block_and_role_vars()
    {
        var css = ColorScheme.BuildThemePair("#285AD2").AsCss();
        Assert.Contains(":root {", css);
        Assert.Contains("--color-primary:", css);
        Assert.Contains("--color-on-primary:", css);
        Assert.Contains("--color-error:", css);
        Assert.Contains("@media (prefers-color-scheme: dark)", css);
        Assert.Contains("[data-theme=\"dark\"]", css);
    }

    [Fact]
    public void Ramps_have_11_monotonic_steps_when_requested()
    {
        var scheme = ColorScheme.FromSeed("#285AD2", new SchemeOptions { IncludeRamps = true });
        Assert.True(scheme.TryGet(ColorRole.Primary, out var primary));
        Assert.NotNull(primary.Ramp);
        Assert.Equal(11, primary.Ramp!.Count);
        Assert.Equal([50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950],
            [.. primary.Ramp.Select(s => s.Step)]);

        var lastL = double.MaxValue;
        foreach (var shade in primary.Ramp)
        {
            var (l, _, _) = ColorMetrics.RgbToOkLch(shade.Color.R, shade.Color.G, shade.Color.B);
            Assert.True(l <= lastL + 1e-6, $"ramp lightness not monotonic at step {shade.Step}");
            lastL = l;
        }
    }

    [Fact]
    public void Ramp_css_vars_emitted_when_ramps_included()
    {
        var css = ColorScheme.FromSeed("#285AD2", new SchemeOptions { IncludeRamps = true }).AsCss();
        Assert.Contains("--color-primary-50:", css);
        Assert.Contains("--color-primary-500:", css);
        Assert.Contains("--color-primary-950:", css);
    }

    [Fact]
    public void Rgb_FromHex_parses_short_and_long_forms()
    {
        Assert.Equal(new Rgb(0x28, 0x5A, 0xD2), Rgb.FromHex("#285AD2"));
        Assert.Equal(new Rgb(255, 255, 255), Rgb.FromHex("#fff"));
        Assert.Equal("#285AD2", Rgb.FromHex("285AD2").Hex);
        Assert.Throws<FormatException>(() => Rgb.FromHex("#12"));
    }
}
