using OverTone;
namespace OverTone.Algorithms;

/// <summary>
/// A helper container for building and reducing an octree used for color quantization.
/// The octree groups similar colors into leaf nodes; pruning the tree reduces the number
/// of leaves (colors) and produces a palette of representative colors.
/// </summary>
public class Octree
{
    /// <summary>
    /// Maximum tree depth (bits per channel considered). Public so helper nodes can reference it.
    /// </summary>
    public const int MaxDepth = 8;

    private readonly OctreeNode _root = new();
    private readonly List<OctreeNode>[] _levels = new List<OctreeNode>[MaxDepth + 1];

    /// <summary>
    /// Creates a new, empty octree instance.
    /// </summary>
    public Octree()
    {
        for (var i = 0; i <= MaxDepth; i++)
            _levels[i] = [];
    }

    /// <summary>
    /// Adds a color to the octree, incrementing counts on the appropriate leaf node.
    /// </summary>
    /// <param name="r">Red channel (0-255).</param>
    /// <param name="g">Green channel (0-255).</param>
    /// <param name="b">Blue channel (0-255).</param>
    public void AddColor(byte r, byte g, byte b) => _root.AddColor(r, g, b, 0, _levels);

    /// <summary>
    /// Reduces (prunes) the octree until the number of leaf nodes is less than or equal
    /// to <paramref name="maxColors"/>. Nodes are merged from upper levels downward.
    /// </summary>
    /// <param name="maxColors">Maximum number of leaf nodes to retain.</param>
    public void Reduce(byte maxColors)
    {
        // Merge nodes until leaf count <= maxColors
        var leaves = GetLeafCount();
        for (var level = MaxDepth - 1; leaves > maxColors && level >= 0; level--)
        {
            var nodesAtLevel = _levels[level];

            if (nodesAtLevel.Count == 0) 
                continue;

            // Order candidate nodes for reduction. Simpler heuristics are used here.
            var ordered = nodesAtLevel.OrderBy(n => n.ChildCount).ToList();

            foreach (var node in ordered.TakeWhile(node => leaves > maxColors))
                leaves -= node.Reduce(_levels, level);
        }
    }

    /// <summary>
    /// Enumerates representative palette colors and their counts from the current leaf nodes.
    /// </summary>
    /// <returns>Sequence of tuples (R, G, B, Count).</returns>
    public IEnumerable<(byte R, byte G, byte B, int Count)> GetPalette()
    {
        var leaves = new List<OctreeNode>();

        CollectLeaves(_root, leaves);

        foreach (var leaf in leaves)
            yield return (leaf.GetAverageRed(), leaf.GetAverageGreen(), leaf.GetAverageBlue(), leaf.PixelCount);
    }

    private int GetLeafCount()
    {
        var count = 0;

        CountLeaves(_root, ref count);

        return count;
    }

    private static void CountLeaves(OctreeNode node, ref int count)
    {
        if (node.IsLeaf)
        {
            count++; 
            return;
        }

        foreach (var child in node.Children)
        {
            if (child == null) continue;
            CountLeaves(child, ref count);
        }
    }

    private static void CollectLeaves(OctreeNode node, List<OctreeNode> outLeaves)
    {
        if (node.IsLeaf)
        {
            outLeaves.Add(node); 
            return;
        }

        foreach (var child in node.Children)
        {
            if (child == null) continue;
            CollectLeaves(child, outLeaves);
        }
    }

}
