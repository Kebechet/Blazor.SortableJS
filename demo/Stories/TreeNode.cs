namespace Blazor.SortableJS.Demo.Stories;

/// <summary>Represents a node in the recursive nesting story.</summary>
public sealed class TreeNode
{
    /// <summary>Initializes a node and its children.</summary>
    /// <param name="label">The displayed label.</param>
    /// <param name="children">The child nodes.</param>
    public TreeNode(string label, params TreeNode[] children)
    {
        Label = label;
        Children = children.ToList();
    }

    /// <summary>Gets the displayed label.</summary>
    public string Label { get; }

    /// <summary>Gets the mutable child collection.</summary>
    public List<TreeNode> Children { get; }
}
