using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;

namespace Kebechet.Blazor.SortableJS.Tests;

public class SortableComponentTests
{
    [Fact]
    public async Task MultiDrag_SeveralRowsSelected_ReordersEveryReferenceInPlace()
    {
        // Arrange
        using var context = CreateContext();
        var first = new TestItem("first");
        var second = new TestItem("second");
        var third = new TestItem("third");
        var fourth = new TestItem("fourth");
        var items = new List<TestItem> { first, second, third, fourth };
        var component = Render(context, "multi", items);
        var sortableEvent = MoveEvent("update", "multi", "multi", new[] { 0, 2 }, new[] { 2, 3 });

        // Act
        await component.Instance.HandleEventAsync(sortableEvent);

        // Assert
        items.ShouldBe(new[] { second, fourth, first, third });
        ReferenceEquals(items[2], first).ShouldBeTrue();
        ReferenceEquals(items[3], third).ShouldBeTrue();
    }

    [Fact]
    public async Task CrossListMove_ItemTransferred_PreservesTheObjectReference()
    {
        // Arrange
        using var context = CreateContext();
        var moved = new TestItem("moved");
        var sourceItems = new List<TestItem> { moved, new("stays") };
        var destinationItems = new List<TestItem> { new("existing") };
        Render(context, "source", sourceItems);
        var destination = Render(context, "destination", destinationItems);
        var sortableEvent = MoveEvent("add", "source", "destination", new[] { 0 }, new[] { 1 });

        // Act
        await destination.Instance.HandleEventAsync(sortableEvent);

        // Assert
        sourceItems.ShouldHaveSingleItem();
        destinationItems.Count.ShouldBe(2);
        ReferenceEquals(destinationItems[1], moved).ShouldBeTrue();
    }

    [Fact]
    public async Task CloneMode_CloneFunctionSet_KeepsSourceAndCreatesTheConfiguredClone()
    {
        // Arrange
        using var context = CreateContext();
        var original = new TestItem("original");
        var sourceItems = new List<TestItem> { original };
        var destinationItems = new List<TestItem>();
        Render(context, "clone-source", sourceItems);
        var destination = context.RenderComponent<Sortable<TestItem>>(parameters => parameters
            .Add(component => component.Id, "clone-destination")
            .Add(component => component.Items, destinationItems)
            .Add(component => component.CloneFunction, item => new TestItem(item.Name + " clone"))
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item.Name)));
        var sortableEvent = MoveEvent("add", "clone-source", "clone-destination", new[] { 0 }, new[] { 0 });
        sortableEvent.IsClone = true;

        // Act
        await destination.Instance.HandleEventAsync(sortableEvent);

        // Assert
        sourceItems.ShouldHaveSingleItem().ShouldBeSameAs(original);
        destinationItems.ShouldHaveSingleItem().Name.ShouldBe("original clone");
        ReferenceEquals(destinationItems[0], original).ShouldBeFalse();
    }

    [Fact]
    public async Task CrossTypeMove_ConvertFunctionSet_ConvertsTheIncomingItem()
    {
        // Arrange
        using var context = CreateContext();
        var sourceItems = new List<int> { 42 };
        var destinationItems = new List<string>();
        context.RenderComponent<Sortable<int>>(parameters => parameters
            .Add(component => component.Id, "number-source")
            .Add(component => component.Items, sourceItems)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));
        var destination = context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, "text-destination")
            .Add(component => component.Items, destinationItems)
            .Add(component => component.ConvertFunction, item => $"Number {item}")
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));
        var sortableEvent = MoveEvent("add", "number-source", "text-destination", new[] { 0 }, new[] { 0 });

        // Act
        await destination.Instance.HandleEventAsync(sortableEvent);

        // Assert
        sourceItems.ShouldBeEmpty();
        destinationItems.ShouldHaveSingleItem().ShouldBe("Number 42");
    }

    [Fact]
    public async Task Swap_ItemsExchanged_ReusesTheSameInstances()
    {
        // Arrange
        using var context = CreateContext();
        var first = new TestItem("first");
        var second = new TestItem("second");
        var items = new List<TestItem> { first, second };
        var component = Render(context, "swap", items);
        var sortableEvent = MoveEvent("update", "swap", "swap", new[] { 0 }, new[] { 1 });
        sortableEvent.IsSwap = true;

        // Act
        await component.Instance.HandleEventAsync(sortableEvent);

        // Assert
        ReferenceEquals(items[0], second).ShouldBeTrue();
        ReferenceEquals(items[1], first).ShouldBeTrue();
    }

    [Fact]
    public async Task OnAdd_ItemArriving_RunsBeforeTheCollectionsAreMutated()
    {
        // Arrange
        using var context = CreateContext();
        var moved = new TestItem("moved");
        var sourceItems = new List<TestItem> { moved };
        var destinationItems = new List<TestItem>();
        var callbackObservedSource = false;
        Render(context, "event-source", sourceItems);
        var destination = context.RenderComponent<Sortable<TestItem>>(parameters => parameters
            .Add(component => component.Id, "event-destination")
            .Add(component => component.Items, destinationItems)
            .Add(component => component.OnAdd, EventCallback.Factory.Create<SortableEventArgs<TestItem>>(
                this,
                args => callbackObservedSource = sourceItems.Contains(args.Items.Single()))));
        var sortableEvent = MoveEvent("add", "event-source", "event-destination", new[] { 0 }, new[] { 0 });

        // Act
        await destination.Instance.HandleEventAsync(sortableEvent);

        // Assert
        callbackObservedSource.ShouldBeTrue();
        sourceItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveOnSpill_ItemDroppedOutside_RemovesTheOriginalReference()
    {
        // Arrange
        using var context = CreateContext();
        var removed = new TestItem("removed");
        var items = new List<TestItem> { removed, new("kept") };
        var component = Render(context, "spill", items);
        var sortableEvent = MoveEvent("spill", "spill", "spill", new[] { 0 }, Array.Empty<int>());
        sortableEvent.IsSpillRemoval = true;

        // Act
        await component.Instance.HandleEventAsync(sortableEvent);

        // Assert
        items.ShouldHaveSingleItem().Name.ShouldBe("kept");
        items.Contains(removed).ShouldBeFalse();
    }

    [Fact]
    public void Render_ItemsBound_MarksEveryItemWrapper()
    {
        // Arrange
        using var context = CreateContext();
        var items = new List<TestItem> { new("one"), new("two") };

        // Act
        var component = Render(context, "rendered", items);

        // Assert
        component.FindAll("#rendered > [data-sortable-item]").Count.ShouldBe(2);
        component.Markup.ShouldContain("data-id");
    }

    private static Bunit.TestContext CreateContext()
    {
        var context = new Bunit.TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    private static IRenderedComponent<Sortable<TestItem>> Render(Bunit.TestContext context, string id, IList<TestItem> items)
    {
        return context.RenderComponent<Sortable<TestItem>>(parameters => parameters
            .Add(component => component.Id, id)
            .Add(component => component.Items, items)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item.Name)));
    }

    private static SortableJsEvent MoveEvent(
        string eventName,
        string sourceId,
        string destinationId,
        int[] oldIndexes,
        int[] newIndexes)
    {
        return new SortableJsEvent
        {
            EventName = eventName,
            SourceId = sourceId,
            DestinationId = destinationId,
            OldIndexes = oldIndexes,
            NewIndexes = newIndexes
        };
    }

    private sealed class TestItem
    {
        internal TestItem(string name)
        {
            Name = name;
        }

        internal string Name { get; }
    }
}



