using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;

namespace Kebechet.Blazor.SortableJS.Tests;

public class SortableEventTests
{
    [Fact]
    public async Task Cross_type_add_callback_receives_the_converted_item_once()
    {
        // Arrange
        using var context = CreateContext();
        var sourceItems = new List<int> { 7 };
        var destinationItems = new List<string>();
        var conversionCount = 0;
        string? callbackItem = null;
        context.RenderComponent<Sortable<int>>(parameters => parameters
            .Add(component => component.Id, "typed-source")
            .Add(component => component.Items, sourceItems));
        var destination = context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, "typed-destination")
            .Add(component => component.Items, destinationItems)
            .Add(component => component.ConvertFunction, item =>
            {
                conversionCount++;
                return $"Value {item}";
            })
            .Add(component => component.OnAdd, EventCallback.Factory.Create<SortableEventArgs<string>>(
                this,
                args => callbackItem = args.Items.Single())));
        var sortableEvent = CreateEvent("add", "typed-source", "typed-destination", new[] { 0 }, new[] { 0 });

        // Act
        await destination.Instance.HandleEventAsync(sortableEvent);

        // Assert
        callbackItem.ShouldBe("Value 7");
        conversionCount.ShouldBe(1);
        destinationItems.ShouldHaveSingleItem().ShouldBe("Value 7");
    }

    [Fact]
    public async Task Remove_callback_receives_the_item_that_already_left_the_source()
    {
        // Arrange
        using var context = CreateContext();
        var moved = new EventItem("moved");
        var stays = new EventItem("stays");
        var sourceItems = new List<EventItem> { moved, stays };
        var destinationItems = new List<EventItem>();
        EventItem? callbackItem = null;
        var source = context.RenderComponent<Sortable<EventItem>>(parameters => parameters
            .Add(component => component.Id, "remove-source")
            .Add(component => component.Items, sourceItems)
            .Add(component => component.OnRemove, EventCallback.Factory.Create<SortableEventArgs<EventItem>>(
                this,
                args => callbackItem = args.Items.Single())));
        var destination = context.RenderComponent<Sortable<EventItem>>(parameters => parameters
            .Add(component => component.Id, "remove-destination")
            .Add(component => component.Items, destinationItems));
        var addEvent = CreateEvent("add", "remove-source", "remove-destination", new[] { 0 }, new[] { 0 });
        var removeEvent = CreateEvent("remove", "remove-source", "remove-destination", new[] { 0 }, new[] { 0 });
        await destination.Instance.HandleEventAsync(addEvent);

        // Act
        await source.Instance.HandleEventAsync(removeEvent);

        // Assert
        callbackItem.ShouldBeSameAs(moved);
        sourceItems.ShouldHaveSingleItem().ShouldBeSameAs(stays);
    }

    private static Bunit.TestContext CreateContext()
    {
        var context = new Bunit.TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    private static SortableJsEvent CreateEvent(
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

    private sealed record EventItem(string Name);
}
