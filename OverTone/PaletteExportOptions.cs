namespace OverTone;

/// <summary>
/// Optional settings that customize how a palette is serialized by <see cref="PaletteExporter"/>.
/// </summary>
public record PaletteExportOptions
{
    /// <summary>
    /// A human-readable name for the palette. Used in JSON, in header comments, and to derive the
    /// C array identifier. Defaults to <c>"OverTone Palette"</c>.
    /// </summary>
    public string PaletteName { get; init; } = "OverTone Palette";

    /// <summary>
    /// The base name used for generated variable/key names, for example <c>--color-1</c> (CSS),
    /// <c>$color-1</c> (SCSS), or <c>'color-1'</c> (Tailwind). Defaults to <c>"color"</c>.
    /// </summary>
    public string Prefix { get; init; } = "color";

    /// <summary>
    /// When <c>true</c> (default), formats that support it (currently JSON) include per-color pixel
    /// counts and percentages. Set to <c>false</c> for a leaner, metadata-free export.
    /// </summary>
    public bool IncludeMetadata { get; init; } = true;
}
