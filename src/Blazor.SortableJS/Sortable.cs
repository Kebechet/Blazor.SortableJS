using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Kebechet.Blazor.SortableJS;

/// <summary>Renders a SortableJS-enabled collection whose order remains owned by Blazor.</summary>
/// <typeparam name="TItem">The collection item type.</typeparam>
public sealed class Sortable<TItem> : ComponentBase, IAsyncDisposable, ISortableContainer
{
    private const string InteropModulePath = "./_content/Kebechet.Blazor.SortableJS/sortable-interop.js";
    private const string DefaultRootTag = "div";
    private const string DefaultItemTag = "div";
    private const string DefaultDataIdAttribute = "data-id";
    private const string ItemMarkerAttribute = SortableInteropNames.ItemMarkerAttribute;
    private const string SetDataTextAttribute = SortableInteropNames.SetDataTextAttribute;
    private const string UndraggableClass = SortableInteropNames.UndraggableClass;
    private readonly List<object?> _lastMovedItems = new();
    private DotNetObjectReference<Sortable<TItem>>? _dotNetReference;
    private IJSObjectReference? _module;
    private IJSObjectReference? _sortableReference;
    private SortableRegistry? _registry;
    private string _resolvedId = string.Empty;
    private string? _lastAppliedOptions;
    private SortableOptions? _resolvedDefaults;
    private bool _isDisposed;

    /// <summary>
    /// Serializer used only to detect option changes, never to talk to JavaScript.
    /// </summary>
    /// <remarks>
    /// Nulls are kept: an option being cleared is exactly the change that has to be noticed, and
    /// omitting it would make "set" and "cleared" describe identically.
    /// </remarks>
    private static readonly JsonSerializerOptions OptionsSnapshotSerializer = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Gets or sets the mutable collection that is reordered in place.</summary>
    /// <remarks>
    /// Null makes the list accept-only: it takes drops and raises the usual callbacks, but stores
    /// nothing. Items still leave their source collection exactly as they would for any other
    /// cross-list move, which is what a delete or archive zone wants.
    /// </remarks>
    [Parameter]
    public IList<TItem>? Items { get; set; } = new List<TItem>();

    /// <summary>Gets or sets the template rendered for each item.</summary>
    [Parameter, EditorRequired]
    public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Gets or sets SortableJS options for this list.</summary>
    [Parameter]
    public SortableOptions Options { get; set; } = new();

    /// <summary>Gets or sets the root DOM id. It is generated automatically when omitted.</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>Gets or sets the root element name.</summary>
    [Parameter]
    public string RootTag { get; set; } = DefaultRootTag;

    /// <summary>Gets or sets the element name that wraps each item template.</summary>
    [Parameter]
    public string ItemTag { get; set; } = DefaultItemTag;

    /// <summary>Gets or sets a CSS class applied to the root element.</summary>
    [Parameter]
    public string? RootClass { get; set; }

    /// <summary>Gets or sets a CSS class applied to every item wrapper.</summary>
    [Parameter]
    public string? ItemClass { get; set; }

    /// <summary>Gets or sets whether stable item keys are used when rendering.</summary>
    [Parameter]
    public bool ShouldUseItemKeys { get; set; } = true;

    /// <summary>Gets or sets a selector that returns the stable render and persistence key for an item.</summary>
    [Parameter]
    public Func<TItem, object>? ItemKeySelector { get; set; }

    /// <summary>Gets or sets a selector for the text placed in the browser drag data transfer object.</summary>
    [Parameter]
    public Func<TItem, string>? SetDataTextSelector { get; set; }

    /// <summary>Gets or sets a function that clones an item during a clone-mode cross-list move.</summary>
    [Parameter]
    public Func<TItem, TItem>? CloneFunction { get; set; }

    /// <summary>Gets or sets a function that converts an item arriving from a list of another item type.</summary>
    /// <remarks>Throwing is the only way to refuse an item. Prefer <see cref="TryConvertFunction"/>.</remarks>
    [Parameter]
    public Func<object, TItem>? ConvertFunction { get; set; }

    /// <summary>
    /// Gets or sets a conversion that may decline an item arriving from a list of another item type.
    /// </summary>
    /// <remarks>
    /// Returning false refuses the transfer and leaves both collections untouched, so a destination
    /// can reject an item without exceptions taking part in ordinary drag-and-drop control flow.
    /// Takes precedence over <see cref="ConvertFunction"/>.
    /// </remarks>
    [Parameter]
    public SortableTryConvert<TItem>? TryConvertFunction { get; set; }

    /// <summary>Gets or sets a predicate deciding whether an individual item can start a drag.</summary>
    /// <remarks>
    /// Items that fail it are marked with <see cref="UndraggableClass"/>, which the interop module
    /// always adds to the SortableJS filter selector. The CSS-selector options stay available for
    /// consumers who would rather express the rule themselves.
    /// </remarks>
    [Parameter]
    public Func<TItem, bool>? IsItemDraggable { get; set; }

    /// <summary>Gets or sets a synchronous decision that can reject a move or steer its placement.</summary>
    /// <remarks>
    /// <see cref="OnMove"/> can only observe. SortableJS asks whether a move is allowed synchronously
    /// and acts on the returned value, which an asynchronous <c>EventCallback</c> cannot supply, so
    /// this is the only way to veto a drop or override the insertion position.
    /// <para>
    /// WebAssembly only. Blazor Server makes every callback a network round trip and so cannot
    /// answer synchronously; setting this under Server throws rather than silently doing nothing.
    /// </para>
    /// </remarks>
    [Parameter]
    public Func<SortableMoveContext<TItem>, SortableMoveDecision>? MoveDecision { get; set; }

    /// <summary>Gets or sets a synchronous predicate deciding whether this list accepts an item.</summary>
    /// <remarks>
    /// Maps to the SortableJS group <c>put</c> function, allowing per-item and per-source decisions
    /// that the fixed group modes cannot express. Enforced in .NET when the drop is applied, so it
    /// holds on every platform and for every item of a MultiDrag selection, not only the primary one.
    /// </remarks>
    [Parameter]
    public Func<SortableTransferContext<TItem>, bool>? CanAcceptItem { get; set; }

    /// <summary>Gets or sets a synchronous predicate deciding whether an item may leave this list.</summary>
    /// <remarks>
    /// Maps to the SortableJS group <c>pull</c> function. Enforced in .NET as well, as for
    /// <see cref="CanAcceptItem"/>.
    /// </remarks>
    [Parameter]
    public Func<SortableTransferContext<TItem>, bool>? CanReleaseItem { get; set; }

    /// <summary>Gets or sets unmatched attributes applied to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Gets or sets the callback invoked when an item is chosen.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnChoose { get; set; }
    /// <summary>Gets or sets the callback invoked when an item is unchosen.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnUnchoose { get; set; }
    /// <summary>Gets or sets the callback invoked when dragging starts.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnStart { get; set; }
    /// <summary>Gets or sets the callback invoked when dragging ends.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnEnd { get; set; }
    /// <summary>Gets or sets the callback invoked before items arriving from another list are inserted.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnAdd { get; set; }
    /// <summary>Gets or sets the callback invoked before items are reordered in this list.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnUpdate { get; set; }
    /// <summary>Gets or sets the callback invoked when a list reports a sort.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnSort { get; set; }
    /// <summary>Gets or sets the callback invoked after items leave this list.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnRemove { get; set; }
    /// <summary>Gets or sets the callback invoked when a filtered item is activated.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnFilter { get; set; }
    /// <summary>Gets or sets the notification callback invoked while an item moves over a target.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnMove { get; set; }
    /// <summary>Gets or sets the callback invoked when SortableJS creates a clone.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnClone { get; set; }
    /// <summary>Gets or sets the callback invoked when the prospective insertion position changes.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnChange { get; set; }
    /// <summary>Gets or sets the callback invoked when MultiDrag selects an item.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnSelect { get; set; }
    /// <summary>Gets or sets the callback invoked when MultiDrag deselects an item.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnDeselect { get; set; }
    /// <summary>Gets or sets the callback invoked when an item is spilled outside a valid list.</summary>
    [Parameter]
    public EventCallback<SortableEventArgs<TItem>> OnSpill { get; set; }

    string ISortableContainer.Id
    {
        get { return _resolvedId; }
    }

    int ISortableContainer.Count
    {
        get { return Items?.Count ?? 0; }
    }

    bool ISortableContainer.IsAcceptOnly
    {
        get { return Items is null; }
    }

    IReadOnlyList<object?> ISortableContainer.LastMovedItems
    {
        get { return _lastMovedItems; }
        set
        {
            _lastMovedItems.Clear();
            _lastMovedItems.AddRange(value);
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _resolvedId = string.IsNullOrWhiteSpace(Id)
            ? SortableElementId.Next()
            : Id;
        _resolvedDefaults = ServiceProvider.GetService(typeof(ISortableDefaults)) is ISortableDefaults registeredDefaults
            ? registeredDefaults.Options
            : SortableDefaults.Options;
        _registry = SortableRegistryProvider.Get(JSRuntime);
        _registry.Register(this);
    }

    /// <summary>Pairs the two option sources so a single description covers both.</summary>
    private sealed record OptionsSnapshot(SortableOptions? Defaults, SortableOptions Options, SortableDecisionFlags Decisions);

    /// <summary>Tells the interop module which synchronous decisions are configured.</summary>
    private sealed record SortableDecisionFlags(bool HasMoveDecision, bool HasPutDecision, bool HasPullDecision);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isDisposed)
        {
            return;
        }

        // Deliberately here rather than in OnParametersSet, and on every render rather than only the
        // first. OnParametersSet also runs while a WebAssembly app is being prerendered on the
        // server, where IsBrowser is false, so the guard would reject a perfectly valid app before
        // it ever reached the browser. OnAfterRender does not run during prerendering. Checking
        // every render, not just the first, still catches a decision assigned later - which would
        // otherwise slip past and silently never take effect.
        GuardSynchronousDecisions();

        if (firstRender)
        {
            _dotNetReference = DotNetObjectReference.Create(this);
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", InteropModulePath);
            _lastAppliedOptions = DescribeOptions();
            _sortableReference = await _module.InvokeAsync<IJSObjectReference>(
                "create",
                _resolvedId,
                _dotNetReference,
                _resolvedDefaults,
                Options,
                DescribeDecisions());
            return;
        }

        if (_sortableReference is null)
        {
            return;
        }

        // Reactive options are worth keeping - most wrappers freeze theirs after initialization -
        // but a component re-renders for reasons that have nothing to do with them, and each render
        // was costing an interop round trip plus one option() call per key. Comparing a description
        // of the options is far cheaper than the call it avoids, and on Blazor Server that call
        // crosses the network.
        var description = DescribeOptions();
        if (string.Equals(description, _lastAppliedOptions, StringComparison.Ordinal))
        {
            return;
        }

        _lastAppliedOptions = description;
        await _sortableReference.InvokeVoidAsync("update", _resolvedDefaults, Options, DescribeDecisions());
    }

    private string DescribeOptions()
    {
        return JsonSerializer.Serialize(
            new OptionsSnapshot(_resolvedDefaults, Options, DescribeDecisions()),
            OptionsSnapshotSerializer);
    }

    private SortableDecisionFlags DescribeDecisions()
    {
        return new SortableDecisionFlags(
            MoveDecision is not null,
            CanAcceptItem is not null,
            CanReleaseItem is not null);
    }

    /// <summary>Receives one of the fifteen typed SortableJS events from the ES module.</summary>
    /// <param name="sortableEvent">The event payload.</param>
    /// <returns>A task that completes after callbacks and any collection mutation finish.</returns>
    [JSInvokable]
    public async Task HandleEventAsync(SortableJsEvent sortableEvent)
    {
        if (_isDisposed || !Enum.TryParse<SortableEventKind>(sortableEvent.EventName, true, out var kind))
        {
            return;
        }

        var isMutation = kind == SortableEventKind.Add || kind == SortableEventKind.Update ||
            kind == SortableEventKind.Spill && sortableEvent.IsSpillRemoval;
        var operationPlan = isMutation ? _registry?.Prepare(sortableEvent) : null;
        IReadOnlyList<object?>? operationItems = null;
        if (operationPlan is not null)
        {
            operationItems = sortableEvent.IsSwap || sortableEvent.IsSpillRemoval
                ? operationPlan.SourceItems
                : operationPlan.DestinationItems;
        }

        var args = CreateEventArgs(sortableEvent, kind, operationItems);
        await InvokeCallbackAsync(kind, args);
        if (operationPlan is not null)
        {
            _registry?.Apply(operationPlan);
        }
    }

    /// <summary>Answers, synchronously, whether a move is allowed and where it should land.</summary>
    /// <param name="request">The move under consideration.</param>
    /// <returns>The decision, as the numeric value of <see cref="SortableMoveDecision"/>.</returns>
    [JSInvokable]
    public int DecideMove(SortableDecisionRequest request)
    {
        if (MoveDecision is null)
        {
            return (int)SortableMoveDecision.Default;
        }

        return (int)MoveDecision(new SortableMoveContext<TItem>
        {
            SourceId = request.SourceId,
            DestinationId = request.DestinationId,
            Item = ItemAt(request.DraggedIndex, request.SourceId),
            RelatedItem = ItemAt(request.RelatedIndex, request.DestinationId),
            DraggedIndex = request.DraggedIndex,
            RelatedIndex = request.RelatedIndex,
            WillInsertAfter = request.WillInsertAfter
        });
    }

    /// <summary>Answers, synchronously, whether this list accepts an incoming item.</summary>
    /// <param name="request">The transfer under consideration.</param>
    /// <returns>True when the item may be dropped here.</returns>
    [JSInvokable]
    public bool DecidePut(SortableDecisionRequest request)
    {
        return CanAcceptItem is null || CanAcceptItem(CreateTransferContext(request));
    }

    /// <summary>Answers, synchronously, whether an item may leave this list.</summary>
    /// <param name="request">The transfer under consideration.</param>
    /// <returns>True when the item may be dragged out.</returns>
    [JSInvokable]
    public bool DecidePull(SortableDecisionRequest request)
    {
        return CanReleaseItem is null || CanReleaseItem(CreateTransferContext(request));
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (string.IsNullOrEmpty(RootTag) || string.IsNullOrEmpty(ItemTag))
        {
            throw new InvalidOperationException("RootTag and ItemTag cannot be empty.");
        }

        // Render-tree sequences identify source locations, so every loop iteration must reuse them.
        builder.OpenElement(0, RootTag);
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "id", _resolvedId);
        if (!string.IsNullOrWhiteSpace(RootClass))
        {
            builder.AddAttribute(3, "class", RootClass);
        }

        var items = Items ?? Array.Empty<TItem>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            builder.OpenElement(4, ItemTag);
            builder.SetKey(GetRenderKey(item, index));
            builder.AddAttribute(5, ItemMarkerAttribute, string.Empty);
            var itemClass = ResolveItemClass(item);
            if (!string.IsNullOrWhiteSpace(itemClass))
            {
                builder.AddAttribute(6, "class", itemClass);
            }

            var key = GetStableItemKey(item);
            if (key is not null)
            {
                builder.AddAttribute(7, Options.DataIdAttribute ?? DefaultDataIdAttribute, Convert.ToString(key, CultureInfo.InvariantCulture));
            }

            if (SetDataTextSelector is not null)
            {
                builder.AddAttribute(8, SetDataTextAttribute, SetDataTextSelector(item));
            }

            if (ItemTemplate is not null)
            {
                builder.AddContent(9, ItemTemplate(item));
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <summary>Destroys the SortableJS instance and releases all JavaScript and .NET interop references.</summary>
    /// <returns>A value task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _registry?.Unregister(this);
        _registry = null;

        var sortableReference = _sortableReference;
        var module = _module;
        _sortableReference = null;
        _module = null;

        try
        {
            if (sortableReference is not null)
            {
                await sortableReference.InvokeVoidAsync("destroy");
            }
        }
        catch (JSDisconnectedException)
        {
        }
        finally
        {
            if (sortableReference is not null)
            {
                try
                {
                    await sortableReference.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                }
            }

            if (module is not null)
            {
                try
                {
                    await module.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                }
            }

            _dotNetReference?.Dispose();
            _dotNetReference = null;
        }
    }

    private SortableEventArgs<TItem> CreateEventArgs(
        SortableJsEvent sortableEvent,
        SortableEventKind kind,
        IReadOnlyList<object?>? operationItems)
    {
        var rawItems = operationItems ?? _registry?.GetEventItems(
            kind,
            sortableEvent.SourceId,
            sortableEvent.DestinationId,
            sortableEvent.OldIndexes,
            sortableEvent.NewIndexes) ?? Array.Empty<object?>();
        var typedItems = rawItems.OfType<TItem>().ToArray();
        var operation = SortableOperationKind.None;
        if (sortableEvent.IsSwap && kind == SortableEventKind.Update)
        {
            operation = SortableOperationKind.Swap;
        }
        else if (kind == SortableEventKind.Update)
        {
            operation = SortableOperationKind.Reorder;
        }
        else if (kind == SortableEventKind.Add)
        {
            operation = sortableEvent.IsClone ? SortableOperationKind.Clone : SortableOperationKind.Transfer;
        }
        else if (kind == SortableEventKind.Spill && sortableEvent.IsSpillRemoval)
        {
            operation = SortableOperationKind.Removal;
        }

        return new SortableEventArgs<TItem>
        {
            Kind = kind,
            Operation = operation,
            SourceId = sortableEvent.SourceId,
            DestinationId = sortableEvent.DestinationId,
            OldIndexes = sortableEvent.OldIndexes,
            NewIndexes = sortableEvent.NewIndexes,
            Items = typedItems,
            IsClone = sortableEvent.IsClone,
            IsSwap = sortableEvent.IsSwap
        };
    }

    private Task InvokeCallbackAsync(SortableEventKind kind, SortableEventArgs<TItem> args)
    {
        switch (kind)
        {
            case SortableEventKind.Choose: return OnChoose.InvokeAsync(args);
            case SortableEventKind.Unchoose: return OnUnchoose.InvokeAsync(args);
            case SortableEventKind.Start: return OnStart.InvokeAsync(args);
            case SortableEventKind.End: return OnEnd.InvokeAsync(args);
            case SortableEventKind.Add: return OnAdd.InvokeAsync(args);
            case SortableEventKind.Update: return OnUpdate.InvokeAsync(args);
            case SortableEventKind.Sort: return OnSort.InvokeAsync(args);
            case SortableEventKind.Remove: return OnRemove.InvokeAsync(args);
            case SortableEventKind.Filter: return OnFilter.InvokeAsync(args);
            case SortableEventKind.Move: return OnMove.InvokeAsync(args);
            case SortableEventKind.Clone: return OnClone.InvokeAsync(args);
            case SortableEventKind.Change: return OnChange.InvokeAsync(args);
            case SortableEventKind.Select: return OnSelect.InvokeAsync(args);
            case SortableEventKind.Deselect: return OnDeselect.InvokeAsync(args);
            case SortableEventKind.Spill: return OnSpill.InvokeAsync(args);
            default: return Task.CompletedTask;
        }
    }

    private SortableTransferContext<TItem> CreateTransferContext(SortableDecisionRequest request)
    {
        return CreateTransferContext(
            ItemAt(request.DraggedIndex, request.SourceId),
            request.SourceId,
            request.DestinationId,
            request.DraggedIndex);
    }

    private static SortableTransferContext<TItem> CreateTransferContext(object? item, string sourceId, string destinationId, int draggedIndex)
    {
        return new SortableTransferContext<TItem>
        {
            SourceId = sourceId,
            DestinationId = destinationId,
            Item = item is TItem typedItem ? typedItem : default,
            DraggedIndex = draggedIndex
        };
    }

    /// <summary>
    /// Resolves an index reported by JavaScript against the list that JavaScript named.
    /// </summary>
    /// <remarks>
    /// It cannot just read this component's own collection. DecidePut runs on the destination while
    /// the dragged item still belongs to the source, so a local lookup handed every put predicate a
    /// null item; cross-list DecideMove has the mirror problem for the item under the pointer. The
    /// registry knows every list, so the owning one answers. A list of another item type yields
    /// null rather than a wrong item.
    /// </remarks>
    private TItem? ItemAt(int index, string listId)
    {
        if (index < 0)
        {
            return default;
        }

        if (string.Equals(listId, _resolvedId, StringComparison.Ordinal))
        {
            return Items is not null && index < Items.Count ? Items[index] : default;
        }

        return _registry?.ReadItem(listId, index) is TItem item ? item : default;
    }

    /// <summary>
    /// Fails loudly when a synchronous decision is configured on a platform that cannot make one.
    /// </summary>
    /// <remarks>
    /// SortableJS reads the return value of these callbacks on the spot. Blazor Server turns every
    /// callback into a network round trip, so the value can never arrive in time and the predicate
    /// would simply never take effect - a silently ignored veto is worse than an unsupported one.
    /// </remarks>
    private void GuardSynchronousDecisions()
    {
        // Only MoveDecision. It steers where an item lands while the pointer is still moving, which
        // nothing can do after the fact. CanAcceptItem and CanReleaseItem are also enforced in .NET
        // when the drop is applied, so on Blazor Server they still refuse the transfer - the drag
        // just is not rejected visually on the way. Working with less feedback beats not working.
        if (MoveDecision is null || OperatingSystem.IsBrowser())
        {
            return;
        }

        throw new PlatformNotSupportedException(
            $"{nameof(MoveDecision)} needs synchronous interop, which is only available on WebAssembly. " +
            "SortableJS reads the returned value immediately, so under Blazor Server the decision " +
            $"would arrive too late and be ignored. {nameof(CanAcceptItem)} and {nameof(CanReleaseItem)} " +
            $"work on every platform, and {nameof(TryConvertFunction)} can refuse an item once dropped.");
    }

    private string? ResolveItemClass(TItem item)
    {
        if (IsItemDraggable is null || IsItemDraggable(item))
        {
            return ItemClass;
        }

        return string.IsNullOrWhiteSpace(ItemClass) ? UndraggableClass : $"{ItemClass} {UndraggableClass}";
    }

    private object GetRenderKey(TItem item, int index)
    {
        return ShouldUseItemKeys ? GetStableItemKey(item) ?? index : index;
    }

    private object? GetStableItemKey(TItem item)
    {
        return ItemKeySelector is not null ? ItemKeySelector(item) : item;
    }

    IReadOnlyList<object?> ISortableContainer.ReadItems(IReadOnlyList<int> indexes)
    {
        if (Items is null)
        {
            return Array.Empty<object?>();
        }

        return indexes.Where(index => index >= 0 && index < Items.Count).Select(index => (object?)Items[index]).ToArray();
    }

    bool ISortableContainer.TryConvertIncoming(object? item, bool isClone, out object? converted)
    {
        if (item is TItem typedItem)
        {
            converted = isClone && CloneFunction is not null ? CloneFunction(typedItem) : typedItem;
            return true;
        }

        // An accept-only list stores nothing, so it has no type to convert to and never refuses on
        // those grounds. The source still gives the item up, which is the point of a delete zone.
        if (Items is null)
        {
            converted = item;
            return true;
        }

        if (item is not null && TryConvertFunction is not null)
        {
            var isAccepted = TryConvertFunction(item, out var result);
            converted = result;
            return isAccepted;
        }

        if (item is not null && ConvertFunction is not null)
        {
            converted = ConvertFunction(item);
            return true;
        }

        if (item is null && default(TItem) is null)
        {
            converted = default(TItem);
            return true;
        }

        throw new InvalidOperationException(
            $"Cannot move an item of type '{item?.GetType().FullName ?? "null"}' into Sortable<{typeof(TItem).FullName}>. " +
            $"Set {nameof(TryConvertFunction)} or {nameof(ConvertFunction)} on the destination component.");
    }

    bool ISortableContainer.CanAccept(object? item, string sourceId, string destinationId, int draggedIndex)
    {
        return CanAcceptItem is null || CanAcceptItem(CreateTransferContext(item, sourceId, destinationId, draggedIndex));
    }

    bool ISortableContainer.CanRelease(object? item, string sourceId, string destinationId, int draggedIndex)
    {
        return CanReleaseItem is null || CanReleaseItem(CreateTransferContext(item, sourceId, destinationId, draggedIndex));
    }

    void ISortableContainer.RemoveAt(int index)
    {
        Items?.RemoveAt(index);
    }

    void ISortableContainer.Insert(int index, object? item)
    {
        Items?.Insert(index, (TItem)item!);
    }

    void ISortableContainer.Swap(int firstIndex, int secondIndex)
    {
        if (Items is null)
        {
            return;
        }

        (Items[firstIndex], Items[secondIndex]) = (Items[secondIndex], Items[firstIndex]);
    }

    void ISortableContainer.RequestRender()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    /// Resolves the optional <see cref="ISortableDefaults"/> registration.
    /// </summary>
    /// <remarks>
    /// Injecting the interface directly would not do. A nullable [Inject] property is still a hard
    /// requirement to Blazor's activator, which throws "There is no registered service of type ..."
    /// for every consumer who never called AddSortableJs - that is, for the documented default.
    /// </remarks>
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;
}






