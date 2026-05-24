using System.Text;
using System.Runtime.Versioning;

namespace OverTone.Sample
{
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

        private static string Bold(string text) =>
            AnsiSupported ? $"\e[1m{text}\e[0m" : text;

        private static string Dim(string text) =>
            AnsiSupported ? $"\e[2m{text}\e[0m" : text;

        // ── Algorithms ────────────────────────────────────────────────────────────
        private static readonly (PaletteAlgorithm Algorithm, string Label, bool Dedupe)[] Algorithms =
        [
            (PaletteAlgorithm.KMeans,      "K-Means",              false),
            (PaletteAlgorithm.MedianCut,   "Median Cut",           false),
            (PaletteAlgorithm.Octree,      "Octree",               false),
            (PaletteAlgorithm.FuzzyCMeans, "Fuzzy C-Means",        false),
            (PaletteAlgorithm.Popularity,  "Popularity",           false),
            (PaletteAlgorithm.Wu,          "Wu Quantization",      false),
            (PaletteAlgorithm.NeuQuant,    "NeuQuant",             false),
            (PaletteAlgorithm.NeuQuant,    "NeuQuant (Iterative)", true),
        ];

        private static readonly PaletteGenerator Generator = new();

        // ── Entry point ───────────────────────────────────────────────────────────
        [STAThread]
        [SupportedOSPlatform("windows")]
        private static void Main() => MainAsync().GetAwaiter().GetResult();

        private static async Task MainAsync()
        {
            Console.OutputEncoding = Encoding.UTF8;

            while (true)
            {
                DrawBanner();

                Console.WriteLine($"  {Bold("1)")} Open image file");
                Console.WriteLine($"  {Bold("2)")} Load image from URL");
                Console.WriteLine($"  {Bold("3)")} Exit");
                Console.WriteLine();
                Console.Write("  Select › ");

                switch (Console.ReadKey(true).KeyChar)
                {
                    case '1':
                    {
                        var path = ShowFileDialog();
                        if (!string.IsNullOrEmpty(path))
                            await RunPipeline(source: path, isUrl: false);
                        break;
                    }
                    case '2':
                    {
                        Console.WriteLine();
                        Console.Write("  Image URL › ");
                        var url = Console.ReadLine()?.Trim();
                        if (!string.IsNullOrWhiteSpace(url))
                            await RunPipeline(source: url, isUrl: true);
                        break;
                    }
                    case '3':
                        Console.Clear();
                        return;
                }
            }
        }

        // ── Banner ────────────────────────────────────────────────────────────────
        private static void DrawBanner()
        {
            Console.Clear();

            string[] lines =
            [
                @"   ___                 _____                    ",
                @"  / _ \  __   __ ___  |_   _|  ___   _ __   ___",
                @" | | | | \ \ / // _ \   | |   / _ \ | '_ \ / _ \",
                @" | |_| |  \ V /|  __/   | |  | (_) || | | ||  __/",
                @"  \___/    \_/  \___|   |_|   \___/ |_| |_| \___|",
            ];

            // Gradient: deep violet → electric cyan
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

        // ── File dialog ───────────────────────────────────────────────────────────
        // OpenFileDialog requires an STA thread. After the first await in MainAsync the
        // continuation runs on a thread-pool (MTA) thread, so we must spin up a fresh
        // STA thread for the dialog instead of calling it directly.
        private static string? ShowFileDialog()
        {
            string? result = null;

            var thread = new Thread(() =>
            {
                using var ofd = new OpenFileDialog();
                ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*";
                ofd.Title  = "Select an image";
                result = ofd.ShowDialog() == DialogResult.OK ? ofd.FileName : null;
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return result;
        }

        // ── Pipeline ──────────────────────────────────────────────────────────────
        private static async Task RunPipeline(string source, bool isUrl)
        {
            Console.Clear();
            Console.WriteLine();

            // ── Algorithm picker ──────────────────────────────────────────────────
            Console.WriteLine($"  {Bold("Choose an algorithm:")}\n");
            for (var i = 0; i < Algorithms.Length; i++)
                Console.WriteLine($"  {Bold($"{i + 1})")} {Algorithms[i].Label}");

            Console.WriteLine();
            Console.Write("  Algorithm › ");

            var algChar = Console.ReadKey(true).KeyChar;
            var algIndex = algChar - '1';
            if (algIndex < 0 || algIndex >= Algorithms.Length)
                algIndex = 0;

            var (algorithm, algorithmLabel, dedupe) = Algorithms[algIndex];
            Console.WriteLine(algorithmLabel);

            // ── Color count ───────────────────────────────────────────────────────
            Console.Write("  Number of colors [6] › ");
            if (!int.TryParse(Console.ReadLine(), out var colorCount) || colorCount <= 0)
                colorCount = 6;

            // ── NeuQuant options (auto-scaled by default, overridable) ─────────────
            NeuQuantOptions? neuQuantOptions = null;
            if (algorithm == PaletteAlgorithm.NeuQuant)
            {
                var auto = NeuQuantOptions.ForColorCount(colorCount);
                Console.WriteLine();
                Console.WriteLine(Dim($"  NeuQuant defaults → neurons: {auto.NeuronCount}, iterations: {auto.TrainingIterations}"));
                Console.Write("  Override neurons?     [Enter to keep] › ");
                var neuronsInput = Console.ReadLine()?.Trim();
                Console.Write("  Override iterations?  [Enter to keep] › ");
                var iterInput = Console.ReadLine()?.Trim();

                var neurons    = int.TryParse(neuronsInput, out var n) && n > 0 ? n : auto.NeuronCount;
                var iterations = int.TryParse(iterInput,   out var it) && it > 0 ? it : auto.TrainingIterations;

                // Only allocate a custom options object if the user actually changed something
                if (neurons != auto.NeuronCount || iterations != auto.TrainingIterations)
                    neuQuantOptions = new NeuQuantOptions(neurons, iterations);

                Console.WriteLine(Dim($"  Using → neurons: {neurons}, iterations: {iterations}"));
                Console.WriteLine();
            }

            // ── Extraction with spinner ───────────────────────────────────────────
            Console.WriteLine();
            List<ColorPalette> palette;
            try
            {
                palette = await RunWithSpinner(
                    $"  Extracting palette using {Bold(algorithmLabel)}",
                    () => Generator.ExtractColorPaletteAsync(source, colorCount, isUrl, algorithm, dedupe, neuQuantOptions));
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"  {Fg(255, 80, 80, "✖")} Failed: {ex.Message}");
                Pause();
                return;
            }

            // ── Display ───────────────────────────────────────────────────────────
            var label = isUrl ? source : Path.GetFileName(source);
            DisplayPalette(palette, algorithmLabel, label);
            Pause();
        }

        // ── Spinner ───────────────────────────────────────────────────────────────
        private static async Task<T> RunWithSpinner<T>(string message, Func<Task<T>> work)
        {
            char[] frames = ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'];
            var cts = new CancellationTokenSource();

            var spin = Task.Run(async () =>
            {
                var i = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    var frame = AnsiSupported ? $"\e[38;2;0;200;255m{frames[i % frames.Length]}\e[0m" : "-";
                    Console.Write($"\r  {frame} {message}   ");
                    i++;
                    try { await Task.Delay(80, cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }, cts.Token);

            T result;
            try
            {
                result = await work();
            }
            finally
            {
                await cts.CancelAsync();
                try { await spin; } catch { /* ignored */ }
            }

            Console.Write($"\r  {Fg(0, 230, 120, "✔")} {message}   \n");
            return result;
        }

        // ── Palette display ───────────────────────────────────────────────────────
        private static void DisplayPalette(List<ColorPalette> palette, string algorithm, string source)
        {
            var totalPixels = palette.Sum(p => (long)p.PixelCount);
            if (totalPixels == 0) totalPixels = 1;

            const int barWidth = 28;

            Console.WriteLine();
            Console.WriteLine($"  {Bold("Source:")}  {Dim(source)}");
            Console.WriteLine($"  {Bold("Method:")}  {algorithm}");
            Console.WriteLine($"  {Bold("Colors:")}  {palette.Count}");
            Console.WriteLine();
            Console.WriteLine(Dim("  ─────────────────────────────────────────────────────"));

            for (var i = 0; i < palette.Count; i++)
            {
                var c = palette[i];
                var pct = totalPixels > 0 ? c.PixelCount * 100.0 / totalPixels : 0;
                var filledBars = (int)Math.Round(pct / 100.0 * barWidth);
                var bar = new string('█', filledBars) + new string('░', barWidth - filledBars);

                // ── Swatch ────────────────────────────────────────────────────────
                string swatch;
                if (AnsiSupported)
                {
                    swatch = Bg(c.R, c.G, c.B, "      ");
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
                    Console.WriteLine($" {c.AsHex}  {pct,5:F1}%  {c.PixelCount:N0} px");
                    Console.WriteLine(Dim("  ─────────────────────────────────────────────────────"));
                    continue;
                }

                // ── ANSI row ──────────────────────────────────────────────────────
                var coloredBar = Fg(c.R, c.G, c.B, bar);
                var hexLabel   = Bold(c.AsHex);
                var nameHint   = Dim(GetColorName(c.R, c.G, c.B));
                Console.WriteLine($"  {swatch}  {hexLabel}  {nameHint,-18} {coloredBar} {pct,5:F1}%");
            }

            Console.WriteLine(Dim("  ─────────────────────────────────────────────────────"));
            Console.WriteLine();
        }

        // ── Nearest CSS-ish color name ─────────────────────────────────────────────
        private static readonly (string Name, byte R, byte G, byte B)[] ColorNames =
        [
            ("Red",         220,  20,  60),
            ("Orange",      255, 140,   0),
            ("Yellow",      255, 215,   0),
            ("Lime",         50, 205,  50),
            ("Green",         0, 128,   0),
            ("Teal",          0, 128, 128),
            ("Cyan",          0, 255, 255),
            ("Sky Blue",    135, 206, 235),
            ("Blue",          0,   0, 255),
            ("Navy",          0,   0, 128),
            ("Indigo",       75,   0, 130),
            ("Violet",      238, 130, 238),
            ("Magenta",     255,   0, 255),
            ("Pink",        255, 105, 180),
            ("Brown",       139,  69,  19),
            ("Maroon",      128,   0,   0),
            ("Olive",       128, 128,   0),
            ("White",       255, 255, 255),
            ("Silver",      192, 192, 192),
            ("Gray",        128, 128, 128),
            ("Charcoal",     54,  69,  79),
            ("Black",         0,   0,   0),
        ];

        private static string GetColorName(byte r, byte g, byte b)
        {
            var best     = ColorNames[0].Name;
            var bestDist = long.MaxValue;
            foreach (var (name, nr, ng, nb) in ColorNames)
            {
                var dr = (long)(nr - r);
                var dg = (long)(ng - g);
                var db = (long)(nb - b);
                var dist = dr * dr + dg * dg + db * db;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best     = name;
            }
            return best;
        }

        // ── ConsoleColor fallback ─────────────────────────────────────────────────
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

            var best     = ConsoleColor.Black;
            var bestDist = long.MaxValue;
            foreach (var kvp in consoleColors)
            {
                var dr = kvp.Value.R - r;
                var dg = kvp.Value.G - g;
                var db = kvp.Value.B - b;
                var dist = (long)dr * dr + (long)dg * dg + (long)db * db;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best     = kvp.Key;
            }
            return best;
        }

        private static bool IsDarkColor(byte r, byte g, byte b) =>
            0.299 * r + 0.587 * g + 0.114 * b < 128;

        private static void Pause()
        {
            Console.WriteLine(Dim("  Press any key to return to the menu…"));
            Console.ReadKey(true);
        }
    }
}

