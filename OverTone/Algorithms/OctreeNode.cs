using OverTone;
namespace OverTone.Algorithms;

/// <summary>
/// One node in an octree used to group similar colors together.
/// Each node can have up to eight child nodes (one for each octant).
/// When a node has no children it is a "leaf" and holds sums and a count
/// so we can compute the average color for that group.
/// </summary>
public class OctreeNode
{
    /// <summary>
    /// Child nodes for the 8 octants. Entries may be null when a child is absent.
    /// </summary>
    public OctreeNode?[] Children { get; } = new OctreeNode?[8];

    /// <summary>
    /// True when this node has no children. A leaf node represents a final color bucket.
    /// </summary>
    public bool IsLeaf => ChildCount == 0;

    /// <summary>
    /// How many child slots are currently used (0..8).
    /// </summary>
    public int ChildCount { get; private set; }

    // Running totals for color channels. When this node is a leaf these sums and the
    // PixelCount are used to compute the average RGB color for the bucket.
    private int _redSum;
    private int _greenSum;
    private int _blueSum;

    /// <summary>
    /// How many pixels were added to this bucket (used to compute averages).
    /// </summary>
    public int PixelCount { get; private set; }

    /// <summary>
    /// Add a color (pixel) to the tree. The method walks down the tree, picking a child
    /// based on the color bits for each level. If we reach the maximum depth the node
    /// just accumulates the color into its sums and count (it becomes a bucket).
    /// </summary>
    /// <param name="r">Red channel (0-255).</param>
    /// <param name="g">Green channel (0-255).</param>
    /// <param name="b">Blue channel (0-255).</param>
    /// <param name="depth">How deep we are in the tree. Root is 0.</param>
    /// <param name="levels">Helper lists grouped by tree level used when pruning the tree.</param>
    public void AddColor(byte r, byte g, byte b, int depth, List<OctreeNode>[] levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        // Be defensive: if the provided levels array is smaller than expected do not throw.
        // Some callers may pass a trimmed levels array; in that case we still build the tree
        // but skip registering nodes in the levels list when the index would be out of range.
        var nextLevelIndex = depth + 1;
        var canTrackInLevels = levels.Length > nextLevelIndex;

        if (depth >= Octree.MaxDepth)
        {
            _redSum += r;
            _greenSum += g;
            _blueSum += b;
            PixelCount++;
            return;
        }

        var childIndex = GetChildIndex(r, g, b, depth);
        var child = Children[childIndex];

        if (child == null)
        {
            var newChild = new OctreeNode();
            Children[childIndex] = newChild;
            if (canTrackInLevels)
                levels[nextLevelIndex].Add(newChild);
            ChildCount++;
            child = newChild;
        }

        child.AddColor(r, g, b, depth + 1, levels);
    }

    /// <summary>
    /// Merge this node's children into the node itself, making it a leaf.
    /// Returns how many leaf nodes were removed by this merge (used when shrinking the tree).
    /// </summary>
    public int Reduce(List<OctreeNode>[] levels, int levelIndex)
    {
        var removedLeafCount = 0;

        for (var i = 0; i < Children.Length; i++)
        {
            var child = Children[i];
            if (child == null) continue;
            _redSum += child._redSum;
            _greenSum += child._greenSum;
            _blueSum += child._blueSum;
            PixelCount += child.PixelCount;
            removedLeafCount += child.IsLeaf ? 1 : child.ChildCount;

            // If this child was tracked in the levels list, remove it since it's being merged.
            if (levelIndex + 1 < levels.Length)
                levels[levelIndex + 1].Remove(child);

            Children[i] = null;
        }

        ChildCount = 0;
        return removedLeafCount == 0 ? 1 : removedLeafCount;
    }

    /// <summary>
    /// Return the average red value for this bucket. If no pixels have been added
    /// the method returns 0.
    /// </summary>
    public byte GetAverageRed() => PixelCount == 0 ? (byte)0 : (byte)(_redSum / PixelCount);

    /// <summary>
    /// Return the average green value for this bucket. If no pixels have been added
    /// the method returns 0.
    /// </summary>
    public byte GetAverageGreen() => PixelCount == 0 ? (byte)0 : (byte)(_greenSum / PixelCount);

    /// <summary>
    /// Return the average blue value for this bucket. If no pixels have been added
    /// the method returns 0.
    /// </summary>
    public byte GetAverageBlue() => PixelCount == 0 ? (byte)0 : (byte)(_blueSum / PixelCount);

    private static int GetChildIndex(byte r, byte g, byte b, int depth)
    {
        // The octree picks one bit from each color channel at the current tree depth.
        // For example at depth 0 we look at the highest bit of R,G,B; at depth 1 we look at
        // the next highest bit, and so on. Each of those three bits (R,G,B) becomes one
        // bit of a 3-bit index (R = bit2, G = bit1, B = bit0) selecting which child slot to use.
        var bitShift = 7 - depth;

        // Move the target bit to position 0 and keep only that bit.
        var redBit = (r >> bitShift) & 1;
        var greenBit = (g >> bitShift) & 1;
        var blueBit = (b >> bitShift) & 1;

        // Pack into a small number 0..7: (R<<2) | (G<<1) | B
        return (redBit << 2) | (greenBit << 1) | blueBit;
    }
}
