using OverTone.Theming;

namespace OverTone;

/// <summary>
/// The simplest possible entry point — "just give me the N main colors of this image" — with no
/// algorithm, selection mode, or threshold to choose. Under the hood it uses the region-aware
/// segmentation pipeline with sensible defaults (SLIC → region merge → peak colors → saliency → honest
/// coverage). For repeated calls or dependency injection, prefer a shared
/// <see cref="PaletteGenerator"/> and its <see cref="PaletteGenerator.GetColorsAsync(byte[], int, int, CancellationToken)"/>.
/// </summary>
public static class Palette
{
    private static readonly PaletteGenerator Generator = new();

    /// <summary>Returns the <paramref name="colorCount"/> main colors of an in-memory image.</summary>
    public static Task<List<ColorPalette>> GetColorsAsync(byte[] imageData, int colorCount = 6,
        CancellationToken cancellationToken = default)
        => Generator.GetColorsAsync(imageData, colorCount, cancellationToken: cancellationToken);

    /// <summary>Returns the <paramref name="colorCount"/> main colors of an image file or URL.</summary>
    public static Task<List<ColorPalette>> GetColorsAsync(string source, int colorCount = 6, bool isUrl = false,
        CancellationToken cancellationToken = default)
        => Generator.GetColorsAsync(source, colorCount, isUrl, cancellationToken: cancellationToken);

    /// <summary>Builds a ready-to-use <see cref="ColorScheme"/> straight from an in-memory image.</summary>
    public static Task<ColorScheme> GetThemeAsync(byte[] imageData, SchemeOptions? options = null,
        int colorCount = 6, CancellationToken cancellationToken = default)
        => Generator.GetThemeAsync(imageData, options, colorCount, cancellationToken: cancellationToken);

    /// <summary>Builds a ready-to-use <see cref="ColorScheme"/> from an image file or URL.</summary>
    public static Task<ColorScheme> GetThemeAsync(string source, SchemeOptions? options = null,
        int colorCount = 6, bool isUrl = false, CancellationToken cancellationToken = default)
        => Generator.GetThemeAsync(source, options, colorCount, isUrl, cancellationToken: cancellationToken);

    /// <summary>Builds a matching light + dark <see cref="ThemePair"/> from an in-memory image.</summary>
    public static Task<ThemePair> GetThemePairAsync(byte[] imageData, SchemeOptions? options = null,
        int colorCount = 6, CancellationToken cancellationToken = default)
        => Generator.GetThemePairAsync(imageData, options, colorCount, cancellationToken: cancellationToken);

    /// <summary>Builds a matching light + dark <see cref="ThemePair"/> from an image file or URL.</summary>
    public static Task<ThemePair> GetThemePairAsync(string source, SchemeOptions? options = null,
        int colorCount = 6, bool isUrl = false, CancellationToken cancellationToken = default)
        => Generator.GetThemePairAsync(source, options, colorCount, isUrl, cancellationToken: cancellationToken);
}
