namespace OverTone;

/// <summary>
/// The simplest possible entry point — "just give me the N main colors of this image" — with no
/// algorithm, selection mode, or threshold to choose. Under the hood it uses the region-aware
/// segmentation pipeline with sensible defaults (SLIC → region merge → peak colors → saliency → honest
/// coverage). For repeated calls or dependency injection, prefer a shared
/// <see cref="PaletteGenerator"/> and its <see cref="PaletteGenerator.GetColorsAsync(byte[], int, int)"/>.
/// </summary>
public static class Palette
{
    private static readonly PaletteGenerator Generator = new();

    /// <summary>Returns the <paramref name="colorCount"/> main colors of an in-memory image.</summary>
    public static Task<List<ColorPalette>> GetColorsAsync(byte[] imageData, int colorCount = 6)
        => Generator.GetColorsAsync(imageData, colorCount);

    /// <summary>Returns the <paramref name="colorCount"/> main colors of an image file or URL.</summary>
    public static Task<List<ColorPalette>> GetColorsAsync(string source, int colorCount = 6, bool isUrl = false)
        => Generator.GetColorsAsync(source, colorCount, isUrl);
}
