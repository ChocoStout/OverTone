namespace OverTone;

/// <summary>
/// A thread-safe, fixed-capacity <b>LRU</b> cache over a <see cref="PaletteGenerator"/>, so repeated
/// requests for the same image return instantly instead of re-segmenting. The <c>byte[]</c> overload is
/// keyed by a fast content hash; the file/URL overload is keyed by the source string, so a repeated URL
/// also skips the re-download. Opt-in — construct one and keep it for as long as you want the cache to
/// live (e.g. register it as a singleton). Building a theme on top of the cached colors is cheap, so this
/// caches the expensive step (extraction) and leaves theming to the caller.
/// </summary>
/// <remarks>
/// Returned lists are defensive copies, so callers can mutate them without corrupting the cache. Two
/// concurrent misses for the same key may both compute (the result is deterministic, so this is harmless);
/// there is no single-flight coordination.
/// </remarks>
public sealed class PaletteCache
{
    private readonly record struct Entry(string Key, List<ColorPalette> Value);

    private readonly PaletteGenerator _generator;
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _map;
    private readonly LinkedList<Entry> _lru = new();

    /// <summary>
    /// Creates a cache over <paramref name="generator"/> (a fresh default generator when <c>null</c>),
    /// holding at most <paramref name="capacity"/> entries before evicting the least-recently-used.
    /// </summary>
    public PaletteCache(PaletteGenerator? generator = null, int capacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _generator = generator ?? new PaletteGenerator();
        _capacity = capacity;
        _map = new Dictionary<string, LinkedListNode<Entry>>(capacity);
    }

    /// <summary>The number of entries currently cached.</summary>
    public int Count
    {
        get { lock (_gate) return _map.Count; }
    }

    /// <summary>Removes every cached entry.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _lru.Clear();
        }
    }

    /// <summary>
    /// The main colors of an in-memory image, cached by content hash. See
    /// <see cref="PaletteGenerator.GetColorsAsync(byte[], int, int, CancellationToken)"/>.
    /// </summary>
    public async Task<List<ColorPalette>> GetColorsAsync(byte[] imageData, int colorCount = 6,
        int maxDegreeOfParallelism = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        var key = $"b:{Fnv1a64(imageData):x16}:{colorCount}";
        if (TryGet(key, out var hit))
            return hit;

        var fresh = await _generator.GetColorsAsync(imageData, colorCount, maxDegreeOfParallelism, cancellationToken);
        return Store(key, fresh);
    }

    /// <summary>
    /// The main colors of an image file or URL, cached by source string (a repeated URL skips the
    /// re-download). See <see cref="PaletteGenerator.GetColorsAsync(string, int, bool, int, CancellationToken)"/>.
    /// </summary>
    public async Task<List<ColorPalette>> GetColorsAsync(string source, int colorCount = 6, bool isUrl = false,
        int maxDegreeOfParallelism = 1, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var key = $"s:{(isUrl ? "u" : "f")}:{colorCount}:{source}";
        if (TryGet(key, out var hit))
            return hit;

        var fresh = await _generator.GetColorsAsync(source, colorCount, isUrl, maxDegreeOfParallelism, cancellationToken);
        return Store(key, fresh);
    }

    private bool TryGet(string key, out List<ColorPalette> value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                value = Clone(node.Value.Value);
                return true;
            }
        }

        value = [];
        return false;
    }

    private List<ColorPalette> Store(string key, List<ColorPalette> value)
    {
        lock (_gate)
        {
            if (!_map.ContainsKey(key))
            {
                var node = new LinkedListNode<Entry>(new Entry(key, value));
                _lru.AddFirst(node);
                _map[key] = node;

                if (_map.Count > _capacity)
                {
                    var lru = _lru.Last!;
                    _lru.RemoveLast();
                    _map.Remove(lru.Value.Key);
                }
            }
        }

        return Clone(value);
    }

    private static List<ColorPalette> Clone(List<ColorPalette> source)
    {
        var copy = new List<ColorPalette>(source.Count);
        foreach (var c in source)
            copy.Add(new ColorPalette { R = c.R, G = c.G, B = c.B, PixelCount = c.PixelCount });
        return copy;
    }

    /// <summary>FNV-1a 64-bit — fast and dependency-free, for cache keying only (not a secure hash).</summary>
    private static ulong Fnv1a64(byte[] data)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;

        var hash = offset;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }
}
