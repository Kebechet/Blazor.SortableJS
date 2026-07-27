using Bunit;
using Shouldly;
using Xunit;
using TestContext = Bunit.TestContext;

namespace Kebechet.Blazor.SortableJS.Tests;

/// <summary>
/// The Swap plugin exchanges two positions instead of moving an item, so a swap plan carries no
/// items to insert. Across two lists that used to fall through to the ordinary transfer path, which
/// removes from the source and inserts what the plan carries - nothing - deleting the dragged item
/// outright while its counterpart stayed put.
/// </summary>
public sealed class SortableSwapTests : IDisposable
{
    private readonly TestContext _context;

    public SortableSwapTests()
    {
        _context = new TestContext();
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task Swap_AcrossTwoLists_ExchangesBothItems()
    {
        // Arrange
        var left = new List<string> { "alpha", "bravo" };
        var right = new List<string> { "charlie", "delta" };
        Render("left", left);
        var rightComponent = Render("right", right);

        // Act
        await rightComponent.InvokeAsync(() => rightComponent.Instance.HandleEventAsync(SwapEvent("left", "right", 0, 1)));

        // Assert
        left.ShouldBe(["delta", "bravo"]);
        right.ShouldBe(["charlie", "alpha"]);
    }

    [Fact]
    public async Task Swap_AcrossTwoLists_LosesNoItem()
    {
        // Arrange
        var left = new List<string> { "alpha" };
        var right = new List<string> { "charlie" };
        Render("left", left);
        var rightComponent = Render("right", right);

        // Act
        await rightComponent.InvokeAsync(() => rightComponent.Instance.HandleEventAsync(SwapEvent("left", "right", 0, 0)));

        // Assert
        left.Concat(right).OrderBy(item => item).ShouldBe(["alpha", "charlie"]);
    }

    [Fact]
    public async Task Swap_WithinOneList_ExchangesTheTwoPositions()
    {
        // Arrange
        var items = new List<string> { "alpha", "bravo", "charlie" };
        var component = Render("list", items);

        // Act
        await component.InvokeAsync(() => component.Instance.HandleEventAsync(SwapEvent("list", "list", 0, 2)));

        // Assert
        items.ShouldBe(["charlie", "bravo", "alpha"]);
    }

    [Fact]
    public async Task Swap_DestinationIndexOutOfRange_ChangesNothing()
    {
        // Arrange
        var left = new List<string> { "alpha" };
        var right = new List<string> { "charlie" };
        Render("left", left);
        var rightComponent = Render("right", right);

        // Act
        await rightComponent.InvokeAsync(() => rightComponent.Instance.HandleEventAsync(SwapEvent("left", "right", 0, 9)));

        // Assert
        left.ShouldBe(["alpha"]);
        right.ShouldBe(["charlie"]);
    }

    private static SortableJsEvent SwapEvent(string sourceId, string destinationId, int oldIndex, int newIndex)
    {
        return new SortableJsEvent
        {
            EventName = "update",
            SourceId = sourceId,
            DestinationId = destinationId,
            OldIndexes = [oldIndex],
            NewIndexes = [newIndex],
            IsSwap = true
        };
    }

    private IRenderedComponent<Sortable<string>> Render(string id, IList<string> items)
    {
        return _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(component => component.Id, id)
            .Add(component => component.Items, items)
            .Add(component => component.ItemTemplate, item => builder => builder.AddContent(0, item)));
    }
}
