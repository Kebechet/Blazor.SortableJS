namespace Kebechet.Blazor.SortableJS;

/// <summary>
/// Configures a SortableJS instance. Null values leave the corresponding SortableJS default unchanged.
/// </summary>
public sealed class SortableOptions
{
    /// <summary>Gets or sets the rules used when dragging between lists.</summary>
    public SortableGroupOptions? Group { get; set; }
    /// <summary>Gets or sets whether items can be sorted inside this list.</summary>
    public bool? IsSortingEnabled { get; set; }
    /// <summary>Gets or sets whether dragging is disabled.</summary>
    public bool? IsDisabled { get; set; }
    /// <summary>Gets or sets browser-storage persistence settings.</summary>
    public SortableStoreOptions? Store { get; set; }
    /// <summary>Gets or sets the CSS selector for the drag handle.</summary>
    public string? Handle { get; set; }
    /// <summary>Gets or sets the CSS selector that identifies draggable child elements.</summary>
    public string? Draggable { get; set; }
    /// <summary>Gets or sets the percentage of a target that must overlap before items swap.</summary>
    public double? SwapThreshold { get; set; }
    /// <summary>Gets or sets whether inverted swap zones are used.</summary>
    public bool? IsInvertedSwapEnabled { get; set; }
    /// <summary>Gets or sets the threshold used by inverted swap zones.</summary>
    public double? InvertedSwapThreshold { get; set; }
    /// <summary>Gets or sets whether a hidden clone is removed from the DOM.</summary>
    public bool? IsCloneRemovedOnHide { get; set; }
    /// <summary>Gets or sets the list direction.</summary>
    public SortableDirection? Direction { get; set; }
    /// <summary>Gets or sets the class applied to the drop-position ghost.</summary>
    public string? GhostClass { get; set; }
    /// <summary>Gets or sets the class applied to the chosen item.</summary>
    public string? ChosenClass { get; set; }
    /// <summary>Gets or sets the class applied to the drag proxy.</summary>
    public string? DragClass { get; set; }
    /// <summary>Gets or sets selectors whose native interactions SortableJS should not suppress.</summary>
    public string? IgnoredSelectors { get; set; }
    /// <summary>Gets or sets a CSS selector for items that cannot start a drag.</summary>
    public string? Filter { get; set; }
    /// <summary>Gets or sets whether filtered interactions call <c>preventDefault</c>.</summary>
    public bool? ShouldPreventOnFilter { get; set; }
    /// <summary>Gets or sets the animation duration in milliseconds.</summary>
    public int? AnimationDuration { get; set; }
    /// <summary>Gets or sets the CSS easing value used by animations.</summary>
    public string? Easing { get; set; }
    /// <summary>Gets or sets whether drop-event propagation is stopped.</summary>
    public bool? ShouldStopDropPropagation { get; set; }
    /// <summary>Gets or sets whether drag-over-event propagation is stopped.</summary>
    public bool? ShouldStopDragOverPropagation { get; set; }
    /// <summary>Gets or sets the item attribute read by SortableJS persistence.</summary>
    public string? DataIdAttribute { get; set; }
    /// <summary>Gets or sets constant text written by SortableJS's data-transfer callback.</summary>
    public string? SetDataText { get; set; }
    /// <summary>Gets or sets the delay, in milliseconds, before dragging starts.</summary>
    public int? Delay { get; set; }
    /// <summary>Gets or sets whether the drag delay applies only to touch input.</summary>
    public bool? IsDelayOnTouchOnly { get; set; }
    /// <summary>Gets or sets the touch movement, in pixels, that cancels a delayed drag.</summary>
    public int? TouchStartThreshold { get; set; }
    /// <summary>Gets or sets whether the fallback drag implementation is always used.</summary>
    public bool? IsFallbackForced { get; set; }
    /// <summary>Gets or sets the class applied to the fallback drag proxy.</summary>
    public string? FallbackClass { get; set; }
    /// <summary>Gets or sets whether the fallback proxy is appended to the document body.</summary>
    public bool? IsFallbackOnBody { get; set; }
    /// <summary>Gets or sets the pointer movement, in pixels, required to begin a fallback drag.</summary>
    public int? FallbackTolerance { get; set; }
    /// <summary>Gets or sets the fallback proxy offset.</summary>
    public SortableFallbackOffset? FallbackOffset { get; set; }
    /// <summary>Gets or sets whether pointer events are used when supported.</summary>
    public bool? IsPointerSupported { get; set; }
    /// <summary>Gets or sets the distance, in pixels, for insertion into an empty list.</summary>
    public int? EmptyInsertThreshold { get; set; }
    /// <summary>Gets or sets whether nearby scroll containers scroll during a drag.</summary>
    public bool? IsAutoScrollEnabled { get; set; }
    /// <summary>Gets or sets whether SortableJS auto-scrolling is used even when native scrolling is available.</summary>
    public bool? IsAutoScrollFallbackForced { get; set; }
    /// <summary>Gets or sets the edge distance, in pixels, that starts auto-scrolling.</summary>
    public int? ScrollSensitivity { get; set; }
    /// <summary>Gets or sets the auto-scroll speed in pixels per interval.</summary>
    public int? ScrollSpeed { get; set; }
    /// <summary>Gets or sets whether parent scroll containers can also scroll.</summary>
    public bool? ShouldBubbleScroll { get; set; }
    /// <summary>Gets or sets a CSS selector for the element to scroll, or null for automatic discovery.</summary>
    public string? ScrollContainerSelector { get; set; }
    /// <summary>Gets or sets whether native SortableJS scrolling continues after the configured scroll hook.</summary>
    public bool? ShouldContinueNativeScrolling { get; set; }
    /// <summary>Gets or sets whether an item dropped outside a valid list returns to its source.</summary>
    public bool? ShouldRevertOnSpill { get; set; }
    /// <summary>Gets or sets whether an item dropped outside a valid list is removed.</summary>
    public bool? ShouldRemoveOnSpill { get; set; }
    /// <summary>Gets or sets whether the Swap plugin is enabled.</summary>
    public bool? IsSwapEnabled { get; set; }
    /// <summary>Gets or sets the class applied to a Swap plugin target.</summary>
    public string? SwapClass { get; set; }
    /// <summary>Gets or sets whether the MultiDrag plugin is enabled.</summary>
    public bool? IsMultiDragEnabled { get; set; }
    /// <summary>Gets or sets the class applied to MultiDrag selections.</summary>
    public string? SelectedClass { get; set; }
    /// <summary>Gets or sets the modifier key used to select multiple items.</summary>
    public SortableMultiDragKey? MultiDragKey { get; set; }
    /// <summary>Gets or sets whether clicking outside this list preserves the MultiDrag selection.</summary>
    public bool? ShouldAvoidImplicitDeselect { get; set; }
}

/// <summary>Defines cross-list pull behavior.</summary>
public enum PullMode
{
    /// <summary>Allows pulls from any compatible list.</summary>
    Enabled,
    /// <summary>Disallows pulls from this list.</summary>
    Disabled,
    /// <summary>Copies the item into the destination and leaves the source unchanged.</summary>
    Clone,
    /// <summary>Allows pulls only into the configured pull groups.</summary>
    ListedGroups
}

/// <summary>Defines cross-list put behavior.</summary>
public enum PutMode
{
    /// <summary>Accepts items from any compatible list.</summary>
    Enabled,
    /// <summary>Rejects items from other lists.</summary>
    Disabled,
    /// <summary>Accepts items only from the configured put groups.</summary>
    ListedGroups
}

/// <summary>Configures how a list participates in a SortableJS group.</summary>
public sealed class SortableGroupOptions
{
    /// <summary>Gets or sets the group name shared by connected lists.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the pull policy.</summary>
    public PullMode PullMode { get; set; } = PullMode.Enabled;
    /// <summary>Gets or sets the put policy.</summary>
    public PutMode PutMode { get; set; } = PutMode.Enabled;
    /// <summary>Gets or sets destination group names allowed by the listed-groups pull policy.</summary>
    public IReadOnlyList<string> PullGroups { get; set; } = Array.Empty<string>();
    /// <summary>Gets or sets source group names allowed by the listed-groups put policy.</summary>
    public IReadOnlyList<string> PutGroups { get; set; } = Array.Empty<string>();
    /// <summary>Gets or sets whether a hidden clone animates back to the source.</summary>
    public bool ShouldRevertClone { get; set; }
}

/// <summary>Defines the orientation used to calculate insertion points.</summary>
public enum SortableDirection
{
    /// <summary>Lets SortableJS detect the direction from layout.</summary>
    Automatic,
    /// <summary>Uses vertical insertion points.</summary>
    Vertical,
    /// <summary>Uses horizontal insertion points.</summary>
    Horizontal
}

/// <summary>Defines the keyboard modifier used by the MultiDrag plugin.</summary>
public enum SortableMultiDragKey
{
    /// <summary>Uses the Alt key.</summary>
    Alt,
    /// <summary>Uses the Control key.</summary>
    Control,
    /// <summary>Uses the Meta or Command key.</summary>
    Meta,
    /// <summary>Uses the Shift key.</summary>
    Shift
}

/// <summary>Specifies the fallback drag proxy offset.</summary>
public sealed class SortableFallbackOffset
{
    /// <summary>Gets or sets the horizontal offset in pixels.</summary>
    public int X { get; set; }
    /// <summary>Gets or sets the vertical offset in pixels.</summary>
    public int Y { get; set; }
}

/// <summary>Configures local browser persistence through SortableJS's store option.</summary>
public sealed class SortableStoreOptions
{
    /// <summary>Gets or sets the local-storage key.</summary>
    public string Key { get; set; } = string.Empty;
}

/// <summary>Provides process-wide defaults merged underneath each component's options.</summary>
/// <remarks>
/// Safe to set once at startup, and only then. On Blazor Server this value is shared by every
/// circuit, so assigning it in response to a user - a per-user preference, a tenant theme, a runtime
/// toggle - changes behaviour for everyone currently connected, and a host running several apps in
/// one process has no way to keep them apart. Register
/// <see cref="SortableServiceCollectionExtensions.AddSortableJs"/> instead and the defaults are
/// scoped like any other service. A registered <see cref="ISortableDefaults"/> takes precedence.
/// </remarks>
public static class SortableDefaults
{
    /// <summary>Gets or sets the default options used by subsequently initialized components.</summary>
    public static SortableOptions? Options { get; set; }
}

/// <summary>Supplies the default options merged underneath each component's own options.</summary>
public interface ISortableDefaults
{
    /// <summary>Gets the defaults, or null to apply none.</summary>
    SortableOptions? Options { get; }
}

