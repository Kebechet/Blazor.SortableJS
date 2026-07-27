using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using TestContext = Bunit.TestContext;

namespace Kebechet.Blazor.SortableJS.Tests;

/// <summary>
/// Covers what a destination list is allowed to do with an incoming item: decline it, accept it
/// without storing it, or refuse to let one of its own rows be dragged at all.
/// </summary>
public sealed class SortableTransferPolicyTests : IDisposable
{
    private const string ModulePath = "./_content/Kebechet.Blazor.SortableJS/sortable-interop.js";

    private readonly TestContext _context;

    public SortableTransferPolicyTests()
    {
        _context = new TestContext();
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task TryConvert_ConversionDeclined_LeavesBothCollectionsUntouched()
    {
        // Arrange
        var source = new List<string> { "alpha", "beta" };
        var destination = new List<int> { 1 };
        var sourceComponent = RenderStrings("source", source);
        var destinationComponent = _context.RenderComponent<Sortable<int>>(parameters => parameters
            .Add(component => component.Id, "destination")
            .Add(component => component.Items, destination)
            .Add(component => component.TryConvertFunction, TryConvertNothing)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Act
        await destinationComponent.InvokeAsync(() => destinationComponent.Instance.HandleEventAsync(
            AddEvent("source", "destination")));

        // Assert
        source.ShouldBe(["alpha", "beta"]);
        destination.ShouldBe([1]);
        sourceComponent.Instance.ShouldNotBeNull();
    }

    [Fact]
    public async Task TryConvert_ConversionAccepted_TransfersTheConvertedItem()
    {
        // Arrange
        var source = new List<string> { "alpha" };
        var destination = new List<int>();
        RenderStrings("source", source);
        var destinationComponent = _context.RenderComponent<Sortable<int>>(parameters => parameters
            .Add(component => component.Id, "destination")
            .Add(component => component.Items, destination)
            .Add(component => component.TryConvertFunction, TryConvertToLength)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Act
        await destinationComponent.InvokeAsync(() => destinationComponent.Instance.HandleEventAsync(
            AddEvent("source", "destination")));

        // Assert
        source.ShouldBeEmpty();
        destination.ShouldBe([5]);
    }

    [Fact]
    public async Task AcceptOnlyList_ItemDropped_TakesItOffTheSourceAndStoresNothing()
    {
        // Arrange
        var source = new List<string> { "alpha", "beta" };
        RenderStrings("source", source);
        var destinationComponent = _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, "destination")
            .Add(component => component.Items, null)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Act
        await destinationComponent.InvokeAsync(() => destinationComponent.Instance.HandleEventAsync(
            AddEvent("source", "destination")));

        // Assert
        source.ShouldBe(["beta"]);
        destinationComponent.FindAll($"[{SortableInteropNames.ItemMarkerAttribute}]").ShouldBeEmpty();
    }

    [Fact]
    public void IsItemDraggable_ItemRejected_MarksTheRowForTheFilterSelector()
    {
        // Arrange & Act
        var component = _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(child => child.Id, "list")
            .Add(child => child.Items, new List<string> { "locked", "free" })
            .Add(child => child.ItemClass, "row")
            .Add(child => child.IsItemDraggable, item => item != "locked")
            .Add(child => child.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Assert
        var rows = component.FindAll($"[{SortableInteropNames.ItemMarkerAttribute}]");
        rows[0].GetAttribute("class").ShouldBe($"row {SortableInteropNames.UndraggableClass}");
        rows[1].GetAttribute("class").ShouldBe("row");
    }

    [Fact]
    public void Defaults_RegisteredThroughServices_TakePrecedenceOverTheStatic()
    {
        // Arrange
        SortableDefaults.Options = new SortableOptions { GhostClass = "from-static" };
        _context.Services.AddSortableJs(options => options.GhostClass = "from-services");

        try
        {
            // Act
            var component = _context.RenderComponent<Sortable<string>>(parameters => parameters
                .Add(child => child.Id, "list")
                .Add(child => child.Items, new List<string> { "alpha" })
                .Add(child => child.ItemTemplate, item => builder => builder.AddContent(0, item)));

            // Assert
            var create = _context.JSInterop.Invocations
                .Single(invocation => invocation.Identifier == "create");
            create.Arguments.OfType<SortableOptions>().First().GhostClass.ShouldBe("from-services");
            component.Instance.ShouldNotBeNull();
        }
        finally
        {
            SortableDefaults.Options = null;
        }
    }

    [Fact]
    public async Task CanAcceptItem_OneItemRefusedInAMultiDragSelection_BlocksTheWholeTransfer()
    {
        // Arrange - SortableJS asks the group put function once, about the primary dragged row, so
        // a forbidden row selected alongside an allowed one used to travel with it.
        var source = new List<string> { "allowed", "forbidden" };
        var destination = new List<string>();
        RenderStrings("source", source);
        var destinationComponent = _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, "destination")
            .Add(component => component.Items, destination)
            .Add(component => component.CanAcceptItem, context => context.Item != "forbidden")
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Act
        await destinationComponent.InvokeAsync(() => destinationComponent.Instance.HandleEventAsync(new SortableJsEvent
        {
            EventName = "add",
            SourceId = "source",
            DestinationId = "destination",
            OldIndexes = [0, 1],
            NewIndexes = [0, 1]
        }));

        // Assert
        source.ShouldBe(["allowed", "forbidden"]);
        destination.ShouldBeEmpty();
    }

    [Fact]
    public async Task CanAcceptItem_EveryItemPermitted_TransfersTheWholeSelection()
    {
        // Arrange
        var source = new List<string> { "first", "second" };
        var destination = new List<string>();
        RenderStrings("source", source);
        var destinationComponent = _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, "destination")
            .Add(component => component.Items, destination)
            .Add(component => component.CanAcceptItem, _ => true)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Act
        await destinationComponent.InvokeAsync(() => destinationComponent.Instance.HandleEventAsync(new SortableJsEvent
        {
            EventName = "add",
            SourceId = "source",
            DestinationId = "destination",
            OldIndexes = [0, 1],
            NewIndexes = [0, 1]
        }));

        // Assert
        source.ShouldBeEmpty();
        destination.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task CanReleaseItem_ItemRefused_KeepsItInTheSourceList()
    {
        // Arrange
        var source = new List<string> { "pinned" };
        var destination = new List<string>();
        var sourceComponent = _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, "source")
            .Add(component => component.Items, source)
            .Add(component => component.CanReleaseItem, context => context.Item != "pinned")
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));
        var destinationComponent = RenderStrings("destination", destination);

        // Act
        await destinationComponent.InvokeAsync(() => destinationComponent.Instance.HandleEventAsync(
            AddEvent("source", "destination")));

        // Assert
        source.ShouldBe(["pinned"]);
        destination.ShouldBeEmpty();
        sourceComponent.Instance.ShouldNotBeNull();
    }

    [Fact]
    public async Task OnRemove_TransferRefused_ReportsTheRefusedItemNotAnEarlierDrag()
    {
        // Arrange - a successful transfer first, so there is a previous result to go stale.
        var source = new List<string> { "moved", "refused" };
        var destination = new List<string>();
        var removedItems = new List<string>();
        var sourceComponent = _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, "source")
            .Add(component => component.Items, source)
            .Add(component => component.OnRemove, args => removedItems.AddRange(args.Items))
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));
        var destinationComponent = _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, "destination")
            .Add(component => component.Items, destination)
            .Add(component => component.CanAcceptItem, context => context.Item != "refused")
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));

        await destinationComponent.InvokeAsync(() => destinationComponent.Instance.HandleEventAsync(
            AddEvent("source", "destination")));
        removedItems.Clear();

        // Act - now refuse one, and let the matching remove follow as SortableJS emits it.
        await destinationComponent.InvokeAsync(() => destinationComponent.Instance.HandleEventAsync(
            AddEvent("source", "destination")));
        await sourceComponent.InvokeAsync(() => sourceComponent.Instance.HandleEventAsync(new SortableJsEvent
        {
            EventName = "remove",
            SourceId = "source",
            DestinationId = "destination",
            OldIndexes = [0],
            NewIndexes = [0]
        }));

        // Assert - the refused item, never the one that moved a moment ago.
        removedItems.ShouldBe(["refused"]);
        destination.ShouldBe(["moved"]);
    }

    private static bool TryConvertNothing(object item, out int converted)
    {
        converted = default;
        return false;
    }

    private static bool TryConvertToLength(object item, out int converted)
    {
        converted = item.ToString()!.Length;
        return true;
    }

    private static SortableJsEvent AddEvent(string sourceId, string destinationId)
    {
        return new SortableJsEvent
        {
            EventName = "add",
            SourceId = sourceId,
            DestinationId = destinationId,
            OldIndexes = [0],
            NewIndexes = [0]
        };
    }

    private IRenderedComponent<Sortable<string>> RenderStrings(string id, IList<string> items)
    {
        return _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, id)
            .Add(component => component.Items, items)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));
    }
}
