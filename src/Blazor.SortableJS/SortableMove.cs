namespace Kebechet.Blazor.SortableJS;

/// <summary>What SortableJS should do with a move that is being considered.</summary>
/// <remarks>
/// SortableJS asks this question synchronously and acts on the answer, so it cannot be expressed as
/// an <c>EventCallback</c>. See <see cref="Sortable{TItem}.MoveDecision"/> for where it can be used.
/// </remarks>
public enum SortableMoveDecision
{
    /// <summary>Let SortableJS decide, as if no decision had been supplied.</summary>
    Default = 0,

    /// <summary>Refuse the move.</summary>
    Reject = 1,

    /// <summary>Accept it, placing the dragged item before the item it is over.</summary>
    InsertBefore = 2,

    /// <summary>Accept it, placing the dragged item after the item it is over.</summary>
    InsertAfter = 3
}

/// <summary>Describes a move SortableJS is considering, for a synchronous decision.</summary>
/// <typeparam name="TItem">The deciding list's item type.</typeparam>
public sealed class SortableMoveContext<TItem>
{
    /// <summary>Gets the id of the list the item is leaving.</summary>
    public string SourceId { get; init; } = string.Empty;

    /// <summary>Gets the id of the list the item would land in.</summary>
    public string DestinationId { get; init; } = string.Empty;

    /// <summary>Gets the dragged item, or default when it belongs to a list of another type.</summary>
    public TItem? Item { get; init; }

    /// <summary>Gets the item currently under the pointer, or default when there is none.</summary>
    public TItem? RelatedItem { get; init; }

    /// <summary>Gets the dragged item's index in the source list.</summary>
    public int DraggedIndex { get; init; } = -1;

    /// <summary>Gets the index of the item under the pointer, or -1 when there is none.</summary>
    public int RelatedIndex { get; init; } = -1;

    /// <summary>Gets whether SortableJS intends to insert after the related item.</summary>
    public bool WillInsertAfter { get; init; }
}

/// <summary>Describes an item moving between two lists, for a synchronous decision.</summary>
/// <typeparam name="TItem">The deciding list's item type.</typeparam>
public sealed class SortableTransferContext<TItem>
{
    /// <summary>Gets the id of the list the item is leaving.</summary>
    public string SourceId { get; init; } = string.Empty;

    /// <summary>Gets the id of the list the item would land in.</summary>
    public string DestinationId { get; init; } = string.Empty;

    /// <summary>Gets the item being moved, or default when it belongs to a list of another type.</summary>
    public TItem? Item { get; init; }

    /// <summary>Gets the item's index in the source list.</summary>
    public int DraggedIndex { get; init; } = -1;
}

/// <summary>The payload the interop module sends for a synchronous decision.</summary>
/// <remarks>Public only because it crosses the JS interop boundary.</remarks>
public sealed class SortableDecisionRequest
{
    /// <summary>Gets or sets the id of the list the item is leaving.</summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the id of the list the item would land in.</summary>
    public string DestinationId { get; set; } = string.Empty;

    /// <summary>Gets or sets the dragged item's index in the source list.</summary>
    public int DraggedIndex { get; set; } = -1;

    /// <summary>Gets or sets the index of the item under the pointer, or -1 when there is none.</summary>
    public int RelatedIndex { get; set; } = -1;

    /// <summary>Gets or sets whether SortableJS intends to insert after the related item.</summary>
    public bool WillInsertAfter { get; set; }
}
