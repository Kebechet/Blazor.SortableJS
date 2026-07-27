using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.JSInterop;

namespace Kebechet.Blazor.SortableJS;

internal static class SortableRegistryProvider
{
    private static readonly ConditionalWeakTable<IJSRuntime, SortableRegistry> Registries = new();

    internal static SortableRegistry Get(IJSRuntime runtime)
    {
        return Registries.GetValue(runtime, _ => new SortableRegistry());
    }
}

internal sealed class SortableRegistry
{
    private readonly Dictionary<string, ISortableContainer> _containers = new(StringComparer.Ordinal);

    internal void Register(ISortableContainer container)
    {
        if (_containers.ContainsKey(container.Id))
        {
            throw new InvalidOperationException($"A Sortable component with id '{container.Id}' is already registered.");
        }

        _containers.Add(container.Id, container);
    }

    internal void Unregister(ISortableContainer container)
    {
        if (_containers.TryGetValue(container.Id, out var registered) && ReferenceEquals(registered, container))
        {
            _containers.Remove(container.Id);
        }
    }

    internal IReadOnlyList<object?> GetEventItems(SortableEventKind kind, string sourceId, string destinationId, IReadOnlyList<int> oldIndexes, IReadOnlyList<int> newIndexes)
    {
        if (kind == SortableEventKind.Remove && _containers.TryGetValue(sourceId, out var removedFrom))
        {
            return removedFrom.LastMovedItems;
        }

        if (_containers.TryGetValue(sourceId, out var source))
        {
            var sourceItems = source.ReadItems(oldIndexes);
            if (sourceItems.Count > 0)
            {
                return sourceItems;
            }
        }

        if (_containers.TryGetValue(destinationId, out var destination))
        {
            return destination.ReadItems(newIndexes);
        }

        return Array.Empty<object?>();
    }

    internal SortableOperationPlan? Prepare(SortableJsEvent sortableEvent)
    {
        if (!_containers.TryGetValue(sortableEvent.SourceId, out var source) ||
            !_containers.TryGetValue(sortableEvent.DestinationId, out var destination))
        {
            return null;
        }

        var oldIndexes = ValidDistinctIndexes(sortableEvent.OldIndexes, source.Count);
        if (oldIndexes.Count == 0)
        {
            return null;
        }

        var sourceItems = source.ReadItems(oldIndexes);
        IReadOnlyList<object?> destinationItems = Array.Empty<object?>();
        if (!sortableEvent.IsSpillRemoval && !sortableEvent.IsSwap)
        {
            destinationItems = sourceItems.Select(item => destination.ConvertIncoming(item, sortableEvent.IsClone)).ToArray();
        }

        return new SortableOperationPlan(sortableEvent, source, destination, oldIndexes, sourceItems, destinationItems);
    }

    internal void Apply(SortableOperationPlan plan)
    {
        var sortableEvent = plan.SortableEvent;
        var source = plan.Source;
        var destination = plan.Destination;
        source.LastMovedItems = plan.SourceItems;

        if (sortableEvent.IsSpillRemoval)
        {
            foreach (var index in plan.OldIndexes.OrderByDescending(index => index))
            {
                source.RemoveAt(index);
            }

            source.RequestRender();
            return;
        }

        if (sortableEvent.IsSwap && ReferenceEquals(source, destination) && sortableEvent.NewIndexes.Length > 0)
        {
            var newIndex = sortableEvent.NewIndexes[0];
            if (newIndex >= 0 && newIndex < source.Count)
            {
                source.Swap(plan.OldIndexes[0], newIndex);
                source.RequestRender();
            }

            return;
        }

        destination.LastMovedItems = plan.DestinationItems;
        if (!sortableEvent.IsClone)
        {
            foreach (var index in plan.OldIndexes.OrderByDescending(index => index))
            {
                source.RemoveAt(index);
            }
        }

        // The removal above is unconditional, so every item must be placed somewhere. SortableJS
        // reports a destination index of -1 in some drops; discarding those insertions removed the
        // item from its source and put it nowhere, losing it silently. Fall back to appending.
        // See SortableItemLossTests.
        var insertions = plan.DestinationItems
            .Select((item, position) => new
            {
                Item = item,
                Index = position < sortableEvent.NewIndexes.Length && sortableEvent.NewIndexes[position] >= 0
                    ? sortableEvent.NewIndexes[position]
                    : destination.Count
            })
            .OrderBy(insertion => insertion.Index)
            .ToArray();

        foreach (var insertion in insertions)
        {
            destination.Insert(Math.Min(insertion.Index, destination.Count), insertion.Item);
        }

        source.RequestRender();
        if (!ReferenceEquals(source, destination))
        {
            destination.RequestRender();
        }
    }

    private static IReadOnlyList<int> ValidDistinctIndexes(IEnumerable<int> indexes, int count)
    {
        return indexes.Where(index => index >= 0 && index < count).Distinct().ToArray();
    }
}

internal sealed class SortableOperationPlan
{
    internal SortableOperationPlan(
        SortableJsEvent sortableEvent,
        ISortableContainer source,
        ISortableContainer destination,
        IReadOnlyList<int> oldIndexes,
        IReadOnlyList<object?> sourceItems,
        IReadOnlyList<object?> destinationItems)
    {
        SortableEvent = sortableEvent;
        Source = source;
        Destination = destination;
        OldIndexes = oldIndexes;
        SourceItems = sourceItems;
        DestinationItems = destinationItems;
    }

    internal SortableJsEvent SortableEvent { get; }
    internal ISortableContainer Source { get; }
    internal ISortableContainer Destination { get; }
    internal IReadOnlyList<int> OldIndexes { get; }
    internal IReadOnlyList<object?> SourceItems { get; }
    internal IReadOnlyList<object?> DestinationItems { get; }
}

internal interface ISortableContainer
{
    string Id { get; }
    int Count { get; }
    IReadOnlyList<object?> LastMovedItems { get; set; }
    IReadOnlyList<object?> ReadItems(IReadOnlyList<int> indexes);
    object? ConvertIncoming(object? item, bool isClone);
    void RemoveAt(int index);
    void Insert(int index, object? item);
    void Swap(int firstIndex, int secondIndex);
    void RequestRender();
}

/// <summary>
/// Hands out the auto-generated DOM ids for <see cref="Sortable{TItem}"/>.
/// </summary>
/// <remarks>
/// Deliberately non-generic. A static counter declared inside <c>Sortable&lt;TItem&gt;</c> gets its
/// own storage per closed generic type, so <c>Sortable&lt;Foo&gt;</c> and <c>Sortable&lt;Bar&gt;</c>
/// would both start at 1 and collide in the shared registry. See
/// <c>SortableRegistryTests.Generated_ids_are_unique_across_item_types</c>.
/// </remarks>
internal static class SortableElementId
{
    private static long _next;

    internal static string Next()
    {
        return $"kebechet-sortable-{Interlocked.Increment(ref _next).ToString(CultureInfo.InvariantCulture)}";
    }
}
