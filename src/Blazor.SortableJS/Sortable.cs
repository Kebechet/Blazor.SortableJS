using System.Globalization;
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
    private const string ItemMarkerAttribute = "data-sortable-item";
    private const string DefaultDataIdAttribute = "data-id";
    private const string SetDataTextAttribute = "data-sortable-text";
    private readonly List<object?> _lastMovedItems = new();
    private DotNetObjectReference<Sortable<TItem>>? _dotNetReference;
    private IJSObjectReference? _module;
    private IJSObjectReference? _sortableReference;
    private SortableRegistry? _registry;
    private string _resolvedId = string.Empty;
    private bool _isDisposed;

    /// <summary>Gets or sets the mutable collection that is reordered in place.</summary>
    [Parameter, EditorRequired]
    public IList<TItem> Items { get; set; } = new List<TItem>();

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
    [Parameter]
    public Func<object, TItem>? ConvertFunction { get; set; }

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
        get { return Items.Count; }
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
        _registry = SortableRegistryProvider.Get(JSRuntime);
        _registry.Register(this);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isDisposed)
        {
            return;
        }

        if (firstRender)
        {
            _dotNetReference = DotNetObjectReference.Create(this);
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", InteropModulePath);
            _sortableReference = await _module.InvokeAsync<IJSObjectReference>(
                "create",
                _resolvedId,
                _dotNetReference,
                SortableDefaults.Options,
                Options);
            return;
        }

        if (_sortableReference is not null)
        {
            await _sortableReference.InvokeVoidAsync("update", SortableDefaults.Options, Options);
        }
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

        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            builder.OpenElement(4, ItemTag);
            builder.SetKey(GetRenderKey(item, index));
            builder.AddAttribute(5, ItemMarkerAttribute, string.Empty);
            if (!string.IsNullOrWhiteSpace(ItemClass))
            {
                builder.AddAttribute(6, "class", ItemClass);
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
        return indexes.Where(index => index >= 0 && index < Items.Count).Select(index => (object?)Items[index]).ToArray();
    }

    object? ISortableContainer.ConvertIncoming(object? item, bool isClone)
    {
        if (item is TItem typedItem)
        {
            return isClone && CloneFunction is not null ? CloneFunction(typedItem) : typedItem;
        }

        if (item is not null && ConvertFunction is not null)
        {
            return ConvertFunction(item);
        }

        if (item is null && default(TItem) is null)
        {
            return default(TItem);
        }

        throw new InvalidOperationException(
            $"Cannot move an item of type '{item?.GetType().FullName ?? "null"}' into Sortable<{typeof(TItem).FullName}>. " +
            "Set ConvertFunction on the destination component.");
    }

    void ISortableContainer.RemoveAt(int index)
    {
        Items.RemoveAt(index);
    }

    void ISortableContainer.Insert(int index, object? item)
    {
        Items.Insert(index, (TItem)item!);
    }

    void ISortableContainer.Swap(int firstIndex, int secondIndex)
    {
        (Items[firstIndex], Items[secondIndex]) = (Items[secondIndex], Items[firstIndex]);
    }

    void ISortableContainer.RequestRender()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;
}






