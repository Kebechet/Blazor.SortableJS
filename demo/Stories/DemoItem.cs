using System.Runtime.CompilerServices;

namespace Blazor.SortableJS.Demo.Stories;

/// <summary>Represents an observable reference-type item used by the interactive stories.</summary>
internal sealed class DemoItem : IDemoIdentityItem
{
    /// <summary>Initializes an item and captures its reference identity.</summary>
    public DemoItem(string key, string label)
    {
        Key = key;
        Label = label;
        InitialIdentity = RuntimeHelpers.GetHashCode(this);
    }

    /// <summary>Gets the stable item key.</summary>
    public string Key { get; }

    /// <summary>Gets the displayed label.</summary>
    public string Label { get; }

    /// <summary>Gets the identity captured when this instance was created.</summary>
    public int InitialIdentity { get; }

    /// <summary>Gets the current CLR reference identity.</summary>
    public int Identity => RuntimeHelpers.GetHashCode(this);
}

/// <summary>Describes the identity information rendered by a demo model-state panel.</summary>
public interface IDemoIdentityItem
{
    /// <summary>Gets the stable item key.</summary>
    string Key { get; }
    /// <summary>Gets the displayed label.</summary>
    string Label { get; }
    /// <summary>Gets the identity captured when this instance was created.</summary>
    int InitialIdentity { get; }
    /// <summary>Gets the current CLR reference identity.</summary>
    int Identity { get; }
}
