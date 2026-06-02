using System.Diagnostics;
using System.Text;
using OverTone.Processing;

namespace OverTone.Sample;

internal static class Program
{
    // ── ANSI helpers ──────────────────────────────────────────────────────────
    private static bool AnsiSupported =>
        !Console.IsOutputRedirected &&
        (Environment.GetEnvironmentVariable("WT_SESSION") is not null          // Windows Terminal
         || Environment.GetEnvironmentVariable("TERM_PROGRAM") is not null     // iTerm2 / VS Code
         || (Environment.GetEnvironmentVariable("TERM") is { } t
             && (t.Contains("color", StringComparison.OrdinalIgnoreCase) || t.Contains("xterm"))));

    private static string Fg(byte r, byte g, byte b, string text) =>
        AnsiSupported ? $"\e[38;2;{r};{g};{b}m{text}\e[0m" : text;

    private static string Bg(byte r, byte g, byte b, string text) =>
        AnsiSupported ? $"\e[48;2;{r};{g};{b}m{text}\e[0m" : text;

    private static string Bold(string text) => AnsiSupported ? $"\e[1m{text}\e[0m" : text;
    private static string Dim(string text) => AnsiSupported ? $"\e[2m{text}\e[0m" : text;

    // ── Algorithms offered in the extract menu ──────────────────────────────────
    private static readonly (PaletteAlgorithm Algorithm, string Label, PaletteSelectionMode Selection)[] Algorithms =
    [
        (PaletteAlgorithm.KMeans,      "K-Means",              PaletteSelectionMode.Diverse),
        (PaletteAlgorithm.MedianCut,   "Median Cut",           PaletteSelectionMode.Diverse),
        (PaletteAlgorithm.Octree,      "Octree",               PaletteSelectionMode.Diverse),
        (PaletteAlgorithm.FuzzyCMeans, "Fuzzy C-Means",        PaletteSelectionMode.Diverse),
        (PaletteAlgorithm.Popularity,  "Popularity",           PaletteSelectionMode.Diverse),
        (PaletteAlgorithm.Wu,          "Wu Quantization",      PaletteSelectionMode.Diverse),
        (PaletteAlgorithm.NeuQuant,    "NeuQuant",             PaletteSelectionMode.Diverse),
        (PaletteAlgorithm.NeuQuant,    "NeuQuant (Iterative)", PaletteSelectionMode.Dominant),
    ];

    // Distinct algorithms compared head-to-head (all in Diverse mode for a fair comparison).
    private static readonly (PaletteAlgorithm Algorithm, string Label)[] CompareSet =
    [
        (PaletteAlgorithm.KMeans,      "K-Means"),
        (PaletteAlgorithm.MedianCut,   "Median Cut"),
        (PaletteAlgorithm.Octree,      "Octree"),
        (PaletteAlgorithm.FuzzyCMeans, "Fuzzy C-Means"),
        (PaletteAlgorithm.Popularity,  "Popularity"),
        (PaletteAlgorithm.Wu,          "Wu"),
        (PaletteAlgorithm.NeuQuant,    "NeuQuant"),
    ];

    private static readonly (PaletteExportFormat Format, string Label)[] ExportFormats =
    [
        (PaletteExportFormat.Json,     "JSON"),
        (PaletteExportFormat.HexList,  "Hex list"),
        (PaletteExportFormat.CArray,   "C array"),
        (PaletteExportFormat.Css,      "CSS"),
        (PaletteExportFormat.Scss,     "SCSS"),
        (PaletteExportFormat.Tailwind, "Tailwind"),
    ];

    private static readonly PaletteGenerator Generator = new();
    private static readonly PaletteExporter Exporter = new();
    private static readonly HttpClient Http = new();

    // A loaded image, kept in memory so the user can re-extract and compare without reloading.
    private sealed record ImageSource(byte[] Data, string Label, string DefaultName);

    // ── Entry point ───────────────────────────────────────────────────────────
    private static async Task Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; }
        catch { /* some redirected/limited consoles reject this; ignore */ }

        // Accept an image path or URL as a command-line argument (drag-drop / pipelines).
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            var img = await LoadSource(args[0]);
            if (img is not null && await SessionLoop(img))
                return;
        }

        await MainMenu();
    }

    // ── Main menu ───────────────────────────────────────────────────────────────
    private static async Task MainMenu()
    {
        while (true)
        {
            DrawBanner();
            Console.WriteLine($"  {Bold("1)")} Open image file");
            Console.WriteLine($"  {Bold("2)")} Load image from URL");
            Console.WriteLine($"  {Bold("q)")} Exit");
            Console.WriteLine();
            Console.WriteLine(Dim("  Tip: pass an image path or URL as an argument to skip this menu."));
            Console.WriteLine();

            switch (Prompt("  Select › "))
            {
                case null or "q" or "quit" or "3" or "exit":
                    return;
                case "1":
                {
                    var path = Prompt("  Image path › ");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var img = await LoadSource(path);
                        if (img is not null && await SessionLoop(img))
                            return;
                    }
                    break;
                }
                case "2":
                {
                    var url = Prompt("  Image URL › ");
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        var img = await LoadSource(url);
                        if (img is not null && await SessionLoop(img))
                            return;
                    }
                    break;
                }
            }
        }
    }

    // ── Per-image session (re-run without reloading) ─────────────────────────────
    // Returns true when the user wants to quit the whole app, false to return to the main menu.
    private static async Task<bool> SessionLoop(ImageSource img)
    {
        while (true)
        {
            DrawBanner();
            Console.WriteLine($"  {Bold("Loaded:")} {Dim(img.Label)}");
            Console.WriteLine();
            Console.WriteLine($"  {Bold("1)")} Extract a palette");
            Console.WriteLine($"  {Bold("2)")} Compare algorithms");
            Console.WriteLine($"  {Bold("3)")} Open a different image");
            Console.WriteLine($"  {Bold("q)")} Quit");
            Console.WriteLine();

            switch (Prompt("  Select › "))
            {
                case null or "q" or "quit": return true;
                case "1": await ExtractFlow(img); break;
                case "2": await CompareFlow(img); break;
                case "3" or "b" or "back": return false;
            }
        }
    }

    // ── Load a source into memory (cross-platform: path prompt or URL) ───────────
    private static async Task<ImageSource?> LoadSource(string input)
    {
        var trimmed = input.Trim().Trim('"');
        var isUrl = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        try
        {
            if (isUrl)
            {
                var data = await RunWithSpinner("  Downloading image", () => Http.GetByteArrayAsync(trimmed));
                return new ImageSource(data, trimmed, DeriveUrlName(trimmed));
            }

            var full = Path.GetFullPath(trimmed);
            var bytes = await File.ReadAllBytesAsync(full);
            return new ImageSource(bytes, Path.GetFileName(full), Sanitize(Path.GetFileNameWithoutExtension(full)));
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"  {Fg(255, 80, 80, "✖")} Could not load '{trimmed}': {ex.Message}");
            Pause();
            return null;
        }
    }

    // ── Extract a single palette ─────────────────────────────────────────────────
    private static async Task ExtractFlow(ImageSource img)
    {
        DrawBanner();
        Console.WriteLine($"  {Bold("Choose an algorithm:")}\n");
        for (var i = 0; i < Algorithms.Length; i++)
            Console.WriteLine($"  {Bold($"{i + 1})")} {Algorithms[i].Label}");
        Console.WriteLine();

        var algInput = Prompt("  Algorithm [1] › ");
        var algIndex = int.TryParse(algInput, out var ai) && ai >= 1 && ai <= Algorithms.Length ? ai - 1 : 0;
        var (algorithm, algorithmLabel, selection) = Algorithms[algIndex];

        var colorCount = int.TryParse(Prompt("  Number of colors [6] › "), out var cc) && cc > 0 ? cc : 6;

        Console.WriteLine(Dim($"  Selection mode  1) Diverse  2) Dominant   [Enter to keep {selection}]"));
        switch (Prompt("  Mode › "))
        {
            case "1": selection = PaletteSelectionMode.Diverse; break;
            case "2": selection = PaletteSelectionMode.Dominant; break;
        }

        var neuQuantOptions = algorithm == PaletteAlgorithm.NeuQuant ? PromptNeuQuant(colorCount) : null;

        List<ColorPalette> palette;
        try
        {
            palette = await RunWithSpinner($"  Extracting with {Bold(algorithmLabel)}",
                () => Generator.ExtractColorPaletteAsync(img.Data, colorCount, algorithm, selection, neuQuantOptions,
                    maxDegreeOfParallelism: Environment.ProcessorCount));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {Fg(255, 80, 80, "✖")} Failed: {ex.Message}");
            Pause();
            return;
        }

        DisplayPalette(palette, $"{algorithmLabel} · {selection}", img.Label);
        await PromptExport(palette, img);
        Pause();
    }

    private static NeuQuantOptions? PromptNeuQuant(int colorCount)
    {
        var auto = NeuQuantOptions.ForColorCount(colorCount);
        Console.WriteLine(Dim($"  NeuQuant defaults → neurons: {auto.NeuronCount}, iterations: {auto.TrainingIterations}"));

        var neurons    = int.TryParse(Prompt("  Override neurons?    [Enter to keep] › "), out var n) && n > 0 ? n : auto.NeuronCount;
        var iterations = int.TryParse(Prompt("  Override iterations? [Enter to keep] › "), out var it) && it > 0 ? it : auto.TrainingIterations;

        return neurons != auto.NeuronCount || iterations != auto.TrainingIterations
            ? new NeuQuantOptions(neurons, iterations)
            : null;
    }

    // ── Compare every algorithm on the same image, ranked by mean ΔE ─────────────
    private static async Task CompareFlow(ImageSource img)
    {
        DrawBanner();
        Console.WriteLine($"  {Bold("Compare algorithms")} {Dim("— same image, ranked by mean ΔE (lower = closer to the image)")}");
        Console.WriteLine();

        var colorCount = int.TryParse(Prompt("  Number of colors [6] › "), out var cc) && cc > 0 ? cc : 6;
        Console.WriteLine();

        var results = new List<(string Label, List<ColorPalette> Palette, double DeltaE)>();
        foreach (var (algorithm, label) in CompareSet)
        {
            try
            {
                var palette = await RunWithSpinner($"  {label}",
                    () => Generator.ExtractColorPaletteAsync(img.Data, colorCount, algorithm,
                        maxDegreeOfParallelism: Environment.ProcessorCount));
                results.Add((label, palette, PaletteQuality.MeanDeltaE(img.Data, palette,
                    maxDegreeOfParallelism: Environment.ProcessorCount)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {Fg(255, 80, 80, "✖")} {label}: {ex.Message}");
            }
        }

        results.Sort((a, b) => a.DeltaE.CompareTo(b.DeltaE));

        Console.WriteLine();
        Console.WriteLine(Dim("  ───────────────────────────────────────────────────────────"));
        for (var i = 0; i < results.Count; i++)
        {
            var (label, palette, deltaE) = results[i];
            var marker = i == 0 ? Fg(0, 230, 120, "★") : " ";
            Console.WriteLine($"  {marker} {label,-14} {Bold($"ΔE {deltaE,6:F2}")}  {RenderSwatches(palette)}");
        }
        Console.WriteLine(Dim("  ───────────────────────────────────────────────────────────"));
        Console.WriteLine(Dim("  ★ best (lowest mean ΔE) · ΔE ≈ how far each pixel sits from its nearest palette color"));
        Console.WriteLine();
        Pause();
    }

    private static string RenderSwatches(IReadOnlyList<ColorPalette> palette)
    {
        if (!AnsiSupported)
            return string.Join(' ', palette.Select(c => c.AsHex));

        var sb = new StringBuilder();
        foreach (var c in palette)
            sb.Append(Bg(c.R, c.G, c.B, "  "));
        return sb.ToString();
    }

    // ── Export ────────────────────────────────────────────────────────────────
    private static async Task PromptExport(List<ColorPalette> palette, ImageSource img)
    {
        Console.WriteLine($"  {Bold("Export this palette?")} {Dim("(Enter to skip)")}");

        var menu = new StringBuilder("  ");
        for (var i = 0; i < ExportFormats.Length; i++)
            menu.Append($"{Bold($"{i + 1})")} {ExportFormats[i].Label}   ");
        menu.Append($"{Bold("A)")} All");
        Console.WriteLine(menu.ToString());

        var choice = Prompt("  Export › ");
        if (string.IsNullOrEmpty(choice))
            return;

        PaletteExportFormat[] selected;
        if (choice.Equals("A", StringComparison.OrdinalIgnoreCase))
            selected = ExportFormats.Select(f => f.Format).ToArray();
        else if (int.TryParse(choice, out var n) && n >= 1 && n <= ExportFormats.Length)
            selected = [ExportFormats[n - 1].Format];
        else
        {
            Console.WriteLine($"  {Fg(255, 80, 80, "✖")} Unknown choice — skipping export.");
            return;
        }

        var nameInput = Prompt($"  Output name [{img.DefaultName}] › ");
        var baseName = string.IsNullOrWhiteSpace(nameInput) ? img.DefaultName : Sanitize(nameInput);
        var options = new PaletteExportOptions { PaletteName = baseName };

        Console.WriteLine();
        foreach (var format in selected)
        {
            try
            {
                var path = Path.GetFullPath($"{baseName}.{Exporter.GetFileExtension(format)}");
                await Exporter.ExportToFileAsync(palette, format, path, options);
                Console.WriteLine($"  {Fg(0, 230, 120, "✔")} Saved {Dim(path)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  {Fg(255, 80, 80, "✖")} {format}: {ex.Message}");
            }
        }
        Console.WriteLine();
    }

    private static string DeriveUrlName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var segment = uri.Segments.LastOrDefault()?.Trim('/');
            var candidate = string.IsNullOrWhiteSpace(segment) ? uri.Host : Path.GetFileNameWithoutExtension(segment);
            return Sanitize(candidate);
        }
        return "palette";
    }

    private static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "palette";

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "palette" : cleaned;
    }

    // ── Palette display (swatch + hex + RGB + HSL + name + share) ─────────────────
    private static void DisplayPalette(List<ColorPalette> palette, string method, string source)
    {
        var totalPixels = palette.Sum(p => (long)p.PixelCount);
        if (totalPixels == 0) totalPixels = 1;

        const int barWidth = 10;

        Console.WriteLine();
        Console.WriteLine($"  {Bold("Source:")}  {Dim(source)}");
        Console.WriteLine($"  {Bold("Method:")}  {method}");
        Console.WriteLine($"  {Bold("Colors:")}  {palette.Count}");
        Console.WriteLine();

        for (var i = 0; i < palette.Count; i++)
        {
            var c = palette[i];
            var (h, s, l) = ToHsl(c.R, c.G, c.B);
            var pct = c.PixelCount * 100.0 / totalPixels;
            var rgb = $"rgb({c.R,3},{c.G,3},{c.B,3})";
            var hsl = $"hsl({h,3},{s,3}%,{l,3}%)";
            var name = ColorNaming.NearestName(c.R, c.G, c.B);

            if (AnsiSupported)
            {
                var filled = (int)Math.Round(pct / 100.0 * barWidth);
                var bar = Fg(c.R, c.G, c.B, new string('█', filled) + new string('░', barWidth - filled));
                Console.WriteLine($"  {Bg(c.R, c.G, c.B, "      ")}  {Bold(c.AsHex)}  {Dim(rgb)}  {Dim(hsl)}  {name,-11}{bar} {pct,5:F1}%");
            }
            else
            {
                var originalBg = Console.BackgroundColor;
                var originalFg = Console.ForegroundColor;
                Console.BackgroundColor = MapToConsoleColor(c.R, c.G, c.B);
                Console.ForegroundColor = IsDarkColor(c.R, c.G, c.B) ? ConsoleColor.White : ConsoleColor.Black;
                Console.Write($"  #{i + 1:D2}  ");
                Console.BackgroundColor = originalBg;
                Console.ForegroundColor = originalFg;
                Console.WriteLine($" {c.AsHex}  {rgb}  {hsl}  {name,-11} {pct,5:F1}%  {c.PixelCount:N0} px");
            }
        }
        Console.WriteLine();
    }

    // ── Spinner with an elapsed timer (skipped when output isn't an ANSI terminal) ─
    private static async Task<T> RunWithSpinner<T>(string message, Func<Task<T>> work)
    {
        if (!AnsiSupported)
            return await work();

        char[] frames = ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'];
        var cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        var spin = Task.Run(async () =>
        {
            var i = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                Console.Write($"\r  \e[38;2;0;200;255m{frames[i % frames.Length]}\e[0m {message}  {Dim($"{sw.Elapsed.TotalSeconds:F1}s")}   ");
                i++;
                try { await Task.Delay(80, cts.Token); }
                catch (OperationCanceledException) { break; }
            }
        }, cts.Token);

        T result;
        try { result = await work(); }
        finally
        {
            await cts.CancelAsync();
            try { await spin; } catch { /* ignored */ }
        }

        Console.Write($"\r  {Fg(0, 230, 120, "✔")} {message}  {Dim($"{sw.Elapsed.TotalSeconds:F1}s")}   \n");
        return result;
    }

    // ── RGB → HSL (h in degrees, s & l in percent) ────────────────────────────────
    private static (int H, int S, int L) ToHsl(byte r8, byte g8, byte b8)
    {
        var r = r8 / 255.0;
        var g = g8 / 255.0;
        var b = b8 / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;

        double h = 0, s = 0;
        if (max > min)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6.0 : 0.0);
            else if (max == g) h = (b - r) / d + 2.0;
            else h = (r - g) / d + 4.0;
            h /= 6.0;
        }

        return ((int)Math.Round(h * 360.0), (int)Math.Round(s * 100.0), (int)Math.Round(l * 100.0));
    }

    // ── Banner ──────────────────────────────────────────────────────────────────
    private static void DrawBanner()
    {
        SafeClear();

        string[] lines =
        [
            @"   ___                 _____                    ",
            @"  / _ \  __   __ ___  |_   _|  ___   _ __   ___",
            @" | | | | \ \ / // _ \   | |   / _ \ | '_ \ / _ \",
            @" | |_| |  \ V /|  __/   | |  | (_) || | | ||  __/",
            @"  \___/    \_/  \___|   |_|   \___/ |_| |_| \___|",
        ];

        (byte R, byte G, byte B)[] stops =
        [
            (138,  43, 226),
            ( 99,  32, 243),
            ( 60,  80, 255),
            (  0, 180, 255),
            (  0, 230, 230),
        ];

        for (var i = 0; i < lines.Length; i++)
        {
            var (r, g, b) = stops[i];
            Console.WriteLine(AnsiSupported ? $"\e[38;2;{r};{g};{b}m{lines[i]}\e[0m" : lines[i]);
        }

        Console.WriteLine(Dim("  Color Palette Extractor\n"));
    }

    // ── ConsoleColor fallback (legacy terminals without truecolor ANSI) ───────────
    private static ConsoleColor MapToConsoleColor(byte r, byte g, byte b)
    {
        var consoleColors = new Dictionary<ConsoleColor, (int R, int G, int B)>
        {
            [ConsoleColor.Black]       = (0,   0,   0),
            [ConsoleColor.DarkBlue]    = (0,   0, 139),
            [ConsoleColor.DarkGreen]   = (0, 100,   0),
            [ConsoleColor.DarkCyan]    = (0, 139, 139),
            [ConsoleColor.DarkRed]     = (139,  0,   0),
            [ConsoleColor.DarkMagenta] = (139,  0, 139),
            [ConsoleColor.DarkYellow]  = (184, 134,  11),
            [ConsoleColor.Gray]        = (190, 190, 190),
            [ConsoleColor.DarkGray]    = (105, 105, 105),
            [ConsoleColor.Blue]        = (0,   0, 255),
            [ConsoleColor.Green]       = (0, 255,   0),
            [ConsoleColor.Cyan]        = (0, 255, 255),
            [ConsoleColor.Red]         = (255,  0,   0),
            [ConsoleColor.Magenta]     = (255,  0, 255),
            [ConsoleColor.Yellow]      = (255, 255,  0),
            [ConsoleColor.White]       = (255, 255, 255),
        };

        var best = ConsoleColor.Black;
        var bestDist = long.MaxValue;
        foreach (var kvp in consoleColors)
        {
            var dr = kvp.Value.R - r;
            var dg = kvp.Value.G - g;
            var db = kvp.Value.B - b;
            var dist = (long)dr * dr + (long)dg * dg + (long)db * db;
            if (dist >= bestDist) continue;
            bestDist = dist;
            best = kvp.Key;
        }
        return best;
    }

    private static bool IsDarkColor(byte r, byte g, byte b) =>
        0.299 * r + 0.587 * g + 0.114 * b < 128;

    // ── Small console helpers (cross-platform & redirect-safe) ───────────────────
    private static void SafeClear()
    {
        if (Console.IsOutputRedirected)
            return;
        try { Console.Clear(); }
        catch (IOException) { /* not a real terminal; ignore */ }
    }

    private static string? Prompt(string label)
    {
        Console.Write(label);
        return Console.ReadLine()?.Trim();
    }

    private static void Pause()
    {
        if (Console.IsInputRedirected)
            return;
        Console.WriteLine(Dim("  Press Enter to continue…"));
        Console.ReadLine();
    }
}
