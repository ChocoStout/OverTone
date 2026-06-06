using OverTone.Export;

namespace OverTone;

/// <summary>
/// Serializes a generated palette into one of several text formats (see <see cref="PaletteExportFormat"/>).
/// Add a new format by implementing <see cref="IPaletteExporter"/> and registering it (via DI /
/// <c>AddOverTone()</c>, or by passing it to the
/// <see cref="PaletteExporter(IEnumerable{IPaletteExporter})"/> constructor).
/// </summary>
public class PaletteExporter
{
    private readonly Dictionary<PaletteExportFormat, IPaletteExporter> _exporters;

    /// <summary>
    /// Creates an exporter backed by the built-in formats (see <see cref="DefaultExporters"/>).
    /// </summary>
    public PaletteExporter() : this(DefaultExporters())
    {
    }

    /// <summary>
    /// Creates an exporter from an explicit set of format exporters (e.g. resolved by a DI container).
    /// When two report the same <see cref="PaletteExportFormat"/>, the first one wins.
    /// </summary>
    /// <param name="exporters">The exporters to make available.</param>
    public PaletteExporter(IEnumerable<IPaletteExporter> exporters)
    {
        _exporters = [];
        foreach (var exporter in exporters)
            _exporters.TryAdd(exporter.Format, exporter);
    }

    /// <summary>Creates a fresh instance of every built-in exporter, in a stable order.</summary>
    public static IReadOnlyList<IPaletteExporter> DefaultExporters() =>
    [
        new JsonPaletteExporter(),
        new HexListPaletteExporter(),
        new CArrayPaletteExporter(),
        new CssPaletteExporter(),
        new ScssPaletteExporter(),
        new TailwindPaletteExporter(),
    ];

    /// <summary>
    /// The set of export formats discovered and available for use.
    /// </summary>
    public IReadOnlyCollection<PaletteExportFormat> AvailableFormats => _exporters.Keys;

    /// <summary>
    /// Gets the file extension (without a leading dot) for the given format, for example <c>"json"</c>.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when the requested format has no exporter.</exception>
    public string GetFileExtension(PaletteExportFormat format) => Resolve(format).FileExtension;

    /// <summary>
    /// Serializes the given palette into the requested format and returns it as a string.
    /// </summary>
    /// <param name="palette">The colors to serialize.</param>
    /// <param name="format">The output format.</param>
    /// <param name="options">Optional naming/metadata settings; defaults are used when <c>null</c>.</param>
    /// <returns>The formatted palette as a string.</returns>
    /// <exception cref="NotSupportedException">Thrown when the requested format has no exporter.</exception>
    public string Export(IReadOnlyList<ColorPalette> palette, PaletteExportFormat format,
        PaletteExportOptions? options = null)
        => Resolve(format).Export(palette, options ?? new PaletteExportOptions());

    /// <summary>
    /// Serializes the given palette into the requested format and writes it to <paramref name="path"/>.
    /// </summary>
    /// <param name="palette">The colors to serialize.</param>
    /// <param name="format">The output format.</param>
    /// <param name="path">The destination file path.</param>
    /// <param name="options">Optional naming/metadata settings; defaults are used when <c>null</c>.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    /// <exception cref="NotSupportedException">Thrown when the requested format has no exporter.</exception>
    public async Task ExportToFileAsync(IReadOnlyList<ColorPalette> palette, PaletteExportFormat format,
        string path, PaletteExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = Export(palette, format, options);
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    private IPaletteExporter Resolve(PaletteExportFormat format) =>
        _exporters.TryGetValue(format, out var exporter)
            ? exporter
            : throw new NotSupportedException($"Export format: {format} is not implemented");
}
