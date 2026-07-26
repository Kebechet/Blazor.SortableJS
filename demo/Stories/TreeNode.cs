using System.Runtime.CompilerServices;

namespace Blazor.SortableJS.Demo.Stories;

/// <summary>Represents a node in the recursive nesting story.</summary>
public sealed class TreeNode : IDemoIdentityItem
{
    /// <summary>Initializes a node and its children.</summary>
    /// <param name="label">The displayed label.</param>
    /// <param name="children">The child nodes.</param>
    public TreeNode(string label, params TreeNode[] children)
    {
        Key = label.ToLowerInvariant().Replace(' ', '-');
        Label = label;
        Children = children.ToList();
        InitialIdentity = RuntimeHelpers.GetHashCode(this);
    }

    /// <summary>Gets the stable node key.</summary>
    public string Key { get; }

    /// <summary>Gets the displayed label.</summary>
    public string Label { get; }

    /// <summary>Gets the mutable child collection.</summary>
    public List<TreeNode> Children { get; }

    /// <summary>Gets the identity captured when this instance was created.</summary>
    public int InitialIdentity { get; }

    /// <summary>Gets the current CLR reference identity.</summary>
    public int Identity => RuntimeHelpers.GetHashCode(this);
}
