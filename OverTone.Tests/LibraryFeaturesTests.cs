using OverTone.Processing;
using OverTone.Theming;
using Xunit;

namespace OverTone.Tests;

/// <summary>
/// Tests for the 1.1 library features that deepen consumer integration: <see cref="ColorPalette"/>
/// conversion helpers, OkLab interpolation, one-call theme-from-image, the SCSS scheme export,
/// <see cref="PaletteCache"/>, and cooperative cancellation.
/// </summary>
public class LibraryFeaturesTests
{
    // Dominant blue + red accent + cream — three known, distinct regions.
    private static byte[] Image() => SyntheticImage.VerticalStripes(64, 64,
        ((0x28, 0x5A, 0xD2), 3.0),
        ((0xDC, 0x14, 0x3C), 1.0),
        ((0xF5, 0xF5, 0xF0), 1.0));

    private static void NearRgb((byte R, byte G, byte B) actual, (byte R, byte G, byte B) expected, int tol = 2)
        => Assert.True(
            Math.Abs(actual.R - expected.R) <= tol &&
            Math.Abs(actual.G - expected.G) <= tol &&
            Math.Abs(actual.B - expected.B) <= tol,
            $"({actual.R},{actual.G},{actual.B}) not within {tol} of ({expected.R},{expected.G},{expected.B})");

    [Fact]
    public void ColorPalette_conversion_helpers()
    {
        var c = new ColorPalette { R = 0x28, G = 0x5A, B = 0xD2 };

        var argb = c.ToArgb();
        Assert.Equal(0xFF, (argb >> 24) & 0xFF);
        Assert.Equal(0x28, (argb >> 16) & 0xFF);
        Assert.Equal(0x5A, (argb >> 8) & 0xFF);
        Assert.Equal(0xD2, argb & 0xFF);

        var (h, s, l) = new ColorPalette { R = 255, G = 0, B = 0 }.ToHsl();
        Assert.Equal(0.0, h, 1);
        Assert.Equal(1.0, s, 3);
        Assert.Equal(0.5, l, 3);
    }

    [Fact]
    public void RelativeLuminance_and_IsDark()
    {
        Assert.Equal(1.0, new ColorPalette { R = 255, G = 255, B = 255 }.RelativeLuminance, 6);
        Assert.Equal(0.0, new ColorPalette { R = 0, G = 0, B = 0 }.RelativeLuminance, 6);

        Assert.True(new ColorPalette { R = 0, G = 0, B = 0 }.IsDark);
        Assert.True(new ColorPalette { R = 0x19, G = 0x19, B = 0x40 }.IsDark);   // navy
        Assert.False(new ColorPalette { R = 255, G = 255, B = 255 }.IsDark);
        Assert.False(new ColorPalette { R = 0xF5, G = 0xF5, B = 0xF0 }.IsDark);  // cream
    }

    [Fact]
    public void OkLab_lerp_hits_endpoints_and_stays_neutral_for_grays()
    {
        NearRgb(ColorMetrics.LerpOkLab(10, 20, 30, 200, 100, 50, 0.0), (10, 20, 30));
        NearRgb(ColorMetrics.LerpOkLab(10, 20, 30, 200, 100, 50, 1.0), (200, 100, 50));

        var mid = ColorMetrics.LerpOkLab(0, 0, 0, 255, 255, 255, 0.5);
        Assert.True(mid.R == mid.G && mid.G == mid.B, "black→white blend should stay gray");
        Assert.InRange(mid.R, 80, 200);

        // Out-of-range t clamps rather than extrapolating.
        NearRgb(ColorMetrics.LerpOkLab(10, 20, 30, 200, 100, 50, -1.0), (10, 20, 30));
        NearRgb(ColorMetrics.LerpOkLab(10, 20, 30, 200, 100, 50, 2.0), (200, 100, 50));
    }

    [Fact]
    public async Task Palette_lerp_blends_pairwise_to_the_shorter_length()
    {
        var a = await Palette.GetColorsAsync(Image(), 4);
        var b = await Palette.GetColorsAsync(SyntheticImage.ManyColors(64, 64), 6);

        var blended = ColorInterpolation.Lerp(a, b, 0.5);
        Assert.Equal(Math.Min(a.Count, b.Count), blended.Count);

        var first = ColorInterpolation.Lerp(a, b, 0.0);
        NearRgb((first[0].R, first[0].G, first[0].B), (a[0].R, a[0].G, a[0].B));
    }

    [Fact]
    public void Scheme_lerp_hits_endpoints()
    {
        var a = ColorScheme.FromSeed("#285AD2");
        var b = ColorScheme.FromSeed("#DC143C");

        var at0 = ColorScheme.Lerp(a, b, 0.0);
        var at1 = ColorScheme.Lerp(a, b, 1.0);

        NearRgb((at0.Primary.R, at0.Primary.G, at0.Primary.B), (a.Primary.R, a.Primary.G, a.Primary.B));
        NearRgb((at1.Primary.R, at1.Primary.G, at1.Primary.B), (b.Primary.R, b.Primary.G, b.Primary.B));
    }

    [Fact]
    public void Scss_export_emits_variables_a_map_and_ramps()
    {
        var scss = ColorScheme.FromSeed("#285AD2", new SchemeOptions { IncludeRamps = true }).AsScss();
        Assert.Contains("$color-primary:", scss);
        Assert.Contains("$color-on-primary:", scss);
        Assert.Contains("$color: (", scss);
        Assert.Contains("\"primary\":", scss);
        Assert.Contains("$color-primary-500:", scss);
    }

    [Fact]
    public void Scss_pair_export_has_light_and_dark_maps()
    {
        var scss = ColorScheme.BuildThemePair("#285AD2").AsScss();
        Assert.Contains("$color-light: (", scss);
        Assert.Contains("$color-dark: (", scss);
    }

    [Fact]
    public async Task GetThemeAsync_from_image_builds_an_accessible_scheme()
    {
        var scheme = await Palette.GetThemeAsync(Image(), colorCount: 6);

        var contrast = ColorMetrics.ContrastRatio(
            scheme.Primary.R, scheme.Primary.G, scheme.Primary.B,
            scheme.OnPrimary.R, scheme.OnPrimary.G, scheme.OnPrimary.B);
        Assert.True(contrast >= SchemeOptions.AA - 1e-6, $"primary/on contrast {contrast:F2} < AA");

        var pair = await new PaletteGenerator().GetThemePairAsync(Image());
        Assert.Equal(ThemeMode.Light, pair.Light.Mode);
        Assert.Equal(ThemeMode.Dark, pair.Dark.Mode);
    }

    [Fact]
    public async Task Cache_hits_by_content_returns_copies_and_evicts_lru()
    {
        var cache = new PaletteCache(capacity: 1);
        var img = Image();

        var first = await cache.GetColorsAsync(img, 4);
        Assert.Equal(1, cache.Count);

        // Mutating the returned list must not corrupt the cached entry.
        var originalHex = first[0].AsHex;
        first[0].R = (byte)(first[0].R ^ 0xFF);

        var second = await cache.GetColorsAsync(img, 4);
        Assert.Equal(1, cache.Count);                 // same content → a hit, not a second entry
        Assert.Equal(originalHex, second[0].AsHex);   // cache unaffected by the earlier mutation

        // A different colorCount is a different key; capacity 1 evicts the first.
        await cache.GetColorsAsync(img, 5);
        Assert.Equal(1, cache.Count);

        cache.Clear();
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task Extraction_observes_an_already_canceled_token()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var generator = new PaletteGenerator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => generator.GetColorsAsync(Image(), 4, 1, cts.Token));
    }
}
