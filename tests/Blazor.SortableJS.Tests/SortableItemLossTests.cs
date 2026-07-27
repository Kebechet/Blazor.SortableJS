using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;
using TestContext = Bunit.TestContext;

namespace Kebechet.Blazor.SortableJS.Tests;

/// <summary>
/// A move must never destroy an item. SortableJS can report a destination index of -1, and an
/// implementation that removes unconditionally but only inserts for a valid index loses the item
/// with no error, no exception and a still-plausible looking list.
/// </summary>
public sealed class SortableItemLossTests : IDisposable
{
    private const string ModulePath = "./_content/Kebechet.Blazor.SortableJS/sortable-interop.js";

    private readonly TestContext _context;

    public SortableItemLossTests()
    {
        _context = new TestContext();
        _context.JSInterop.SetupModule(ModulePath).Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task Move_UnusableDestinationIndex_KeepsTheItem(int destinationIndex)
    {
        // Arrange
        var items = new List<string> { "first", "second", "third" };
        var component = RenderSortable(items);
        var elementId = component.Find("[id^='kebechet-sortable-']").Id;

        // Act
        await component.InvokeAsync(() => component.Instance.HandleEventAsync(new SortableJsEvent
        {
            EventName = "update",
            SourceId = elementId,
            DestinationId = elementId,
            OldIndexes = [0],
            NewIndexes = [destinationIndex]
        }));

        // Assert
        items.Count.ShouldBe(3);
        items.ShouldContain("first");
    }

    [Fact]
    public async Task Move_NoDestinationIndex_KeepsTheItem()
    {
        // Arrange
        var items = new List<string> { "first", "second" };
        var component = RenderSortable(items);
        var elementId = component.Find("[id^='kebechet-sortable-']").Id;

        // Act
        await component.InvokeAsync(() => component.Instance.HandleEventAsync(new SortableJsEvent
        {
            EventName = "update",
            SourceId = elementId,
            DestinationId = elementId,
            OldIndexes = [0],
            NewIndexes = []
        }));

        // Assert
        items.Count.ShouldBe(2);
        items.ShouldContain("first");
    }

    private IRenderedComponent<Sortable<string>> RenderSortable(List<string> items)
    {
        return _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(x => x.Items, items)
            .Add(x => x.ItemTemplate, (RenderFragment<string>)(item => builder => builder.AddContent(0, item))));
    }
}
