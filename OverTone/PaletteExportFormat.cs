namespace OverTone;

/// <summary>
/// Specifies the output format used to serialize a color palette via <see cref="PaletteExporter"/>.
/// </summary>
public enum PaletteExportFormat
{
    /// <summary>Structured JSON with hex, RGB, HSL, a color name, and optional pixel metadata.</summary>
    Json,

    /// <summary>Plain text, one <c>#RRGGBB</c> value per line.</summary>
    HexList,

    /// <summary>C/C++ header with a <c>uint8_t[][3]</c> RGB array, suitable for Arduino/FastLED LED strips.</summary>
    CArray,

    /// <summary>CSS custom properties inside a <c>:root</c> block.</summary>
    Css,

    /// <summary>SCSS variables plus a Sass list.</summary>
    Scss,

    /// <summary>A Tailwind CSS color config snippet for <c>tailwind.config.js</c>.</summary>
    Tailwind,
}
