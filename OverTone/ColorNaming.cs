namespace OverTone;

/// <summary>
/// Maps an RGB color to the nearest human-friendly name from a compact built-in table (nearest by
/// squared RGB distance). Shared by the sample app and the JSON exporter so there's a single table to
/// maintain. The palette is intentionally broad in the blue/purple region, where a sparse table tends
/// to mislabel medium blues as "Teal".
/// </summary>
public static class ColorNaming
{
    private static readonly (string Name, byte R, byte G, byte B)[] Colors =
    [
        ("Red",             220,  20,  60),
        ("Coral",           255, 127,  80),
        ("Salmon",          250, 128, 114),
        ("Orange",          255, 140,   0),
        ("Gold",            255, 215,   0),
        ("Yellow",          255, 255,   0),
        ("Olive",           128, 128,   0),
        ("Lime",             50, 205,  50),
        ("Green",             0, 128,   0),
        ("Forest Green",     34, 139,  34),
        ("Teal",              0, 128, 128),
        ("Cyan",              0, 255, 255),
        ("Sky Blue",        135, 206, 235),
        ("Cornflower",      100, 149, 237),
        ("Steel Blue",       70, 130, 180),
        ("Denim",            21,  96, 189),
        ("Royal Blue",       65, 105, 225),
        ("Cobalt",            0,  71, 171),
        ("Blue",              0,   0, 255),
        ("Navy",              0,   0, 128),
        ("Dark Slate Blue",  72,  61, 139),
        ("Slate Blue",      106,  90, 205),
        ("Indigo",           75,   0, 130),
        ("Purple",          128,   0, 128),
        ("Violet",          238, 130, 238),
        ("Magenta",         255,   0, 255),
        ("Pink",            255, 105, 180),
        ("Brown",           139,  69,  19),
        ("Maroon",          128,   0,   0),
        ("Beige",           245, 245, 220),
        ("White",           255, 255, 255),
        ("Silver",          192, 192, 192),
        ("Gray",            128, 128, 128),
        ("Charcoal",         54,  69,  79),
        ("Black",             0,   0,   0),
    ];

    /// <summary>
    /// Returns the name of the nearest color (by squared RGB distance) from the built-in table.
    /// </summary>
    public static string NearestName(byte r, byte g, byte b)
    {
        var best = Colors[0].Name;
        var bestDistance = long.MaxValue;

        foreach (var (name, nr, ng, nb) in Colors)
        {
            long dr = nr - r;
            long dg = ng - g;
            long db = nb - b;
            var distance = dr * dr + dg * dg + db * db;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = name;
        }

        return best;
    }
}
