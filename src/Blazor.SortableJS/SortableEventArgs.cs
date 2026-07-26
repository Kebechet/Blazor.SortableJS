namespace Kebechet.Blazor.SortableJS;

/// <summary>Identifies a SortableJS event.</summary>
public enum SortableEventKind
{
    /// <summary>An item was chosen.</summary>
    Choose,
    /// <summary>An item was unchosen.</summary>
    Unchoose,
    /// <summary>Dragging started.</summary>
    Start,
    /// <summary>Dragging ended.</summary>
    End,
    /// <summary>An item was added from another list.</summary>
    Add,
    /// <summary>An item changed position in its list.</summary>
    Update,
    /// <summary>A list was sorted.</summary>
    Sort,
    /// <summary>An item was removed into another list.</summary>
    Remove,
    /// <summary>A filtered item was activated.</summary>
    Filter,
    /// <summary>A dragged item moved over a possible insertion point.</summary>
    Move,
    /// <summary>A clone was created.</summary>
    Clone,
    /// <summary>The insertion position changed during dragging.</summary>
    Change,
    /// <summary>An item was selected by MultiDrag.</summary>
    Select,
    /// <summary>An item was deselected by MultiDrag.</summary>
    Deselect,
    /// <summary>An item was dropped outside a valid list.</summary>
    Spill
}

/// <summary>Describes the collection operation associated with a SortableJS event.</summary>
public enum SortableOperationKind
{
    /// <summary>The event does not mutate a collection.</summary>
    None,
    /// <summary>Items were reordered within one collection.</summary>
    Reorder,
    /// <summary>Items were transferred between collections.</summary>
    Transfer,
    /// <summary>Items were copied into another collection.</summary>
    Clone,
    /// <summary>Two items exchanged positions.</summary>
    Swap,
    /// <summary>Items were removed after being dropped outside a valid list.</summary>
    Removal
}

/// <summary>Contains typed data for a SortableJS event.</summary>
/// <typeparam name="TItem">The item type rendered by the component receiving the event.</typeparam>
public sealed class SortableEventArgs<TItem> : EventArgs
{
    /// <summary>Gets the event kind.</summary>
    public SortableEventKind Kind { get; internal set; }
    /// <summary>Gets the collection operation kind.</summary>
    public SortableOperationKind Operation { get; internal set; }
    /// <summary>Gets the source component DOM id.</summary>
    public string SourceId { get; internal set; } = string.Empty;
    /// <summary>Gets the destination component DOM id.</summary>
    public string DestinationId { get; internal set; } = string.Empty;
    /// <summary>Gets the source indexes, aligned with <see cref="Items"/>.</summary>
    public IReadOnlyList<int> OldIndexes { get; internal set; } = Array.Empty<int>();
    /// <summary>Gets the destination indexes, aligned with <see cref="Items"/>.</summary>
    public IReadOnlyList<int> NewIndexes { get; internal set; } = Array.Empty<int>();
    /// <summary>Gets the affected items representable by <typeparamref name="TItem"/>.</summary>
    public IReadOnlyList<TItem> Items { get; internal set; } = Array.Empty<TItem>();
    /// <summary>Gets whether the operation used a clone pull policy.</summary>
    public bool IsClone { get; internal set; }
    /// <summary>Gets whether the Swap plugin exchanged two items.</summary>
    public bool IsSwap { get; internal set; }
}

/// <summary>Represents the JSON event payload sent by the package's JavaScript module.</summary>
public sealed class SortableJsEvent
{
    /// <summary>Gets or sets the SortableJS event name.</summary>
    public string EventName { get; set; } = string.Empty;
    /// <summary>Gets or sets the source component DOM id.</summary>
    public string SourceId { get; set; } = string.Empty;
    /// <summary>Gets or sets the destination component DOM id.</summary>
    public string DestinationId { get; set; } = string.Empty;
    /// <summary>Gets or sets the source indexes.</summary>
    public int[] OldIndexes { get; set; } = Array.Empty<int>();
    /// <summary>Gets or sets the destination indexes.</summary>
    public int[] NewIndexes { get; set; } = Array.Empty<int>();
    /// <summary>Gets or sets whether the move used clone mode.</summary>
    public bool IsClone { get; set; }
    /// <summary>Gets or sets whether the event came from the Swap plugin.</summary>
    public bool IsSwap { get; set; }
    /// <summary>Gets or sets whether an OnSpill operation removes items.</summary>
    public bool IsSpillRemoval { get; set; }
}



