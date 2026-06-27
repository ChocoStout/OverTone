using OverTone.Processing;
using Xunit;

namespace OverTone.Tests;

/// <summary>
/// Tests for the spectrum/gradient builder (<see cref="SpectrumBuilder"/>) and its supporting color-math
/// primitives on <see cref="ColorMetrics"/> / <see cref="ColorPalette"/>: the HSL round-trip, hue distance
/// wraparound, the chroma vividness proxy, and the merge/fold + area-weighting + hue-ordering of stops.
/// All inputs are hand-built palettes, so the suite is pure and deterministic (no image decoding).
/// </summary>
public class SpectrumTests
{
    private static ColorPalette Color(byte r, byte g, byte b, int pixels) =>
        new() { R = r, G = g, B = b, PixelCount = pixels };

    // ---- Color-math primitives -------------------------------------------------------------------

    [Fact]
    public void HslToRgb_round_trips_with_ToHsl()
    {
        // Sweep a grid of vivid colors; ToHsl ∘ FromHsl should return to within ±1 per channel.
        byte[] levels = [0, 40, 90, 150, 210, 255];
        foreach (var r in levels)
        foreach (var g in levels)
        foreach (var b in levels)
        {
            var (h, s, l) = ColorMetrics.RgbToHsl(r, g, b);
            var (rr, gg, bb) = ColorMetrics.HslToRgb(h, s, l);
            Assert.True(
                Math.Abs(rr - r) <= 1 && Math.Abs(gg - g) <= 1 && Math.Abs(bb - b) <= 1,
                $"({r},{g},{b}) round-tripped to ({rr},{gg},{bb})");
        }
    }

    [Fact]
    public void FromHsl_matches_known_colors_and_defaults_pixel_count()
    {
        var red = ColorPalette.FromHsl(0, 1.0, 0.5);
        Assert.Equal((byte)255, red.R);
        Assert.Equal((byte)0, red.G);
        Assert.Equal((byte)0, red.B);
        Assert.Equal(0, red.PixelCount);

        // Pure green at 120°, and the count flows through when given.
        var green = ColorPalette.FromHsl(120, 1.0, 0.5, pixelCount: 42);
        Assert.Equal((byte)0, green.R);
        Assert.Equal((byte)255, green.G);
        Assert.Equal((byte)0, green.B);
        Assert.Equal(42, green.PixelCount);
    }

    [Fact]
    public void HslToRgb_normalizes_and_clamps_out_of_range_inputs()
    {
        // Hue wraps (380° == 20°) and s/l clamp into range rather than throwing or distorting.
        Assert.Equal(ColorMetrics.HslToRgb(20, 0.6, 0.5), ColorMetrics.HslToRgb(380, 0.6, 0.5));
        Assert.Equal(ColorMetrics.HslToRgb(-340, 0.6, 0.5), ColorMetrics.HslToRgb(20, 0.6, 0.5));
        Assert.Equal(ColorMetrics.HslToRgb(0, 5.0, 2.0), ((byte)255, (byte)255, (byte)255)); // l clamps to 1 → white
    }

    [Theory]
    [InlineData(350, 10, 20)]    // wraps across 0°
    [InlineData(10, 350, 20)]    // order-independent
    [InlineData(0, 180, 180)]    // antipodal — the maximum
    [InlineData(90, 90, 0)]      // identical
    [InlineData(355, 5, 10)]
    public void HueDistance_is_the_shortest_arc(double a, double b, double expected)
    {
        Assert.Equal(expected, ColorMetrics.HueDistance(a, b), 6);
        Assert.InRange(ColorMetrics.HueDistance(a, b), 0.0, 180.0);
    }

    [Fact]
    public void HslChroma_downweights_pale_and_dark_over_raw_saturation()
    {
        // Pure red: max chroma.
        Assert.Equal(1.0, ColorMetrics.HslChroma(1.0, 0.5), 6);
        // A near-black and a near-white both carry full saturation but near-zero chroma.
        Assert.True(ColorMetrics.HslChroma(1.0, 0.02) < 0.05);
        Assert.True(ColorMetrics.HslChroma(1.0, 0.98) < 0.05);
        // Grays have zero chroma regardless of lightness.
        Assert.Equal(0.0, ColorMetrics.HslChroma(0.0, 0.5), 6);
    }

    // ---- Spectrum builder ------------------------------------------------------------------------

    [Fact]
    public void Build_drops_near_neutral_colors()
    {
        var palette = new[]
        {
            Color(128, 128, 128, 1000), // gray — zero chroma
            Color(20, 20, 22, 800),     // near-black
            Color(220, 30, 40, 400),    // vivid red — the only keeper
        };

        var spectrum = SpectrumBuilder.Build(palette);

        var stop = Assert.Single(spectrum);
        Assert.Equal("#DC1E28", stop.AsHex);
        Assert.Equal(400, stop.Weight);
    }

    [Fact]
    public void Build_merges_nearby_hues_and_folds_their_areas()
    {
        // Three reds within a ~22° hue window plus one clearly-separate blue.
        var red = Color(255, 0, 0, 500);       // hue 0°
        var redA = Color(255, 40, 0, 300);     // ~9°
        var redB = Color(255, 0, 40, 200);     // ~351°
        var blue = Color(0, 0, 255, 250);      // hue 240°

        var spectrum = SpectrumBuilder.Build(new[] { red, redA, redB, blue });

        Assert.Equal(2, spectrum.Count);

        // The red family folds into one stop carrying the SUMMED area, represented by the most prominent member.
        var redStop = spectrum.Single(s => s.ToHsl().H < 60 || s.ToHsl().H > 300);
        Assert.Equal(1000, redStop.Weight);            // 500 + 300 + 200
        Assert.Equal("#FF0000", redStop.AsHex);        // the highest-area member wins the color

        var blueStop = spectrum.Single(s => s.AsHex == "#0000FF");
        Assert.Equal(250, blueStop.Weight);            // untouched
    }

    [Fact]
    public void Build_orders_stops_low_to_high_hue()
    {
        var palette = new[]
        {
            Color(0, 0, 255, 100),   // blue   ~240°
            Color(255, 0, 0, 100),   // red    0°
            Color(0, 255, 0, 100),   // green  120°
        };

        var hues = SpectrumBuilder.Build(palette).Select(s => s.ToHsl().H).ToList();

        Assert.Equal(3, hues.Count);
        Assert.Equal(hues.OrderBy(h => h), hues); // ascending
    }

    [Fact]
    public void Build_caps_at_max_stops_keeping_the_most_prominent()
    {
        // Six well-separated hues with distinct areas; cap to the top three by area.
        var palette = new[]
        {
            Color(255, 0, 0, 10),     // red    — smallest
            Color(255, 255, 0, 20),   // yellow
            Color(0, 255, 0, 60),     // green  — keeper
            Color(0, 255, 255, 30),   // cyan
            Color(0, 0, 255, 50),     // blue   — keeper
            Color(255, 0, 255, 40),   // magenta— keeper
        };

        var spectrum = SpectrumBuilder.Build(palette, maxStops: 3);

        Assert.Equal(3, spectrum.Count);
        // The three largest areas (60, 50, 40) survive; the small ones (10, 20, 30) are dropped.
        Assert.Equal(new long[] { 40, 50, 60 }, spectrum.Select(s => s.Weight).OrderBy(w => w).ToArray());
    }

    [Fact]
    public void Build_returns_empty_when_nothing_clears_the_chroma_floor()
    {
        var grays = new[] { Color(0, 0, 0, 100), Color(128, 128, 128, 100), Color(255, 255, 255, 100) };
        Assert.Empty(SpectrumBuilder.Build(grays));
        Assert.Empty(SpectrumBuilder.Build(System.Array.Empty<ColorPalette>()));
    }

    [Fact]
    public void Build_validates_arguments()
    {
        Assert.Throws<System.ArgumentNullException>(() => SpectrumBuilder.Build(null!));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => SpectrumBuilder.Build(System.Array.Empty<ColorPalette>(), maxStops: 0));
    }

    [Fact]
    public void Build_chroma_floor_is_adjustable()
    {
        // A muted color (low chroma) is dropped by default but kept with a lower floor.
        var muted = Color(140, 125, 125, 100); // small chroma
        Assert.True(muted.HslChroma < SpectrumBuilder.DefaultChromaFloor);

        Assert.Empty(SpectrumBuilder.Build(new[] { muted }));
        Assert.Single(SpectrumBuilder.Build(new[] { muted }, chromaFloor: 0.0));
    }
}
