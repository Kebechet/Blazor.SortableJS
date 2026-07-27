namespace Kebechet.Blazor.SortableJS;

/// <summary>
/// Fixed strings shared by the component and <c>sortable-interop.js</c>.
/// </summary>
/// <remarks>
/// These are a format contract, not code references: the component writes them into the DOM and the
/// interop module reads them back out through CSS selectors, so the two sides must never drift.
/// Every value here is mirrored by a constant at the top of <c>sortable-interop.js</c>; change one
/// and you must change the other.
/// </remarks>
public static class SortableInteropNames
{
    /// <summary>Marks an element as one of the list's draggable rows.</summary>
    public const string ItemMarkerAttribute = "data-sortable-item";

    /// <summary>Carries the text handed to the browser's drag data transfer object.</summary>
    public const string SetDataTextAttribute = "data-sortable-text";

    /// <summary>
    /// Applied to rows rejected by <c>IsItemDraggable</c>. The interop module always appends it to
    /// the SortableJS filter selector, so a row carrying it cannot start a drag.
    /// </summary>
    public const string UndraggableClass = "kebechet-sortable-undraggable";
}
