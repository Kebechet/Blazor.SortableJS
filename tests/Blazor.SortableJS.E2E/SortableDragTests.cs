using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Blazor.SortableJS.E2E;

[Collection(DemoCollectionDefinition.Name)]
public sealed class SortableDragTests(DemoFixture fixture)
{
    private const string BasicStory = "sortablejs-basic--reorder-in-place";
    private const string GroupsStory = "sortablejs-groups--connected-groups-with-pull-and-put-policies";
    private const string NestingStory = "sortablejs-nesting--arbitrarily-nested-lists";
    private const string MultiDragStory = "sortablejs-multidrag--multidrag";
    private const string CloneStory = "sortablejs-clone--clone-mode";
    private const string SwapStory = "sortablejs-swap--swap-plugin";
    private const string SpillStory = "sortablejs-onspill--onspill-policies";
    private const string AutoScrollStory = "sortablejs-auto-scroll--auto-scroll";

    [Fact]
    public async Task SameListReorderMutatesTheModelInPlace()
    {
        var frame = await fixture.NavigateToStoryAsync(BasicStory);
        var before = await ReadModelAsync(frame);
        var originalIdentity = before.Item("items", "read-brief").Identity;

        await DragAsync(frame.GetByTestId("item-read-brief"), frame.GetByTestId("item-verify-package"), afterTarget: true);

        await AssertInvariantAsync(
            frame,
            Expected("items", "basic-list", "build-api", "verify-package", "read-brief"));
        var after = await ReadModelAsync(frame);
        Assert.Equal(originalIdentity, after.Item("items", "read-brief").Identity);
    }

    [Fact]
    public async Task CrossListTransferPreservesTheItemInstance()
    {
        var frame = await fixture.NavigateToStoryAsync(GroupsStory);
        var before = await ReadModelAsync(frame);
        var transferredIdentity = before.Item("backlog", "design").Identity;

        await DragAsync(frame.GetByTestId("item-design"), frame.GetByTestId("done-list"));

        await AssertInvariantAsync(
            frame,
            Expected("backlog", "backlog-list", "implement"),
            Expected("done", "done-list", "scaffold", "design"));
        var after = await ReadModelAsync(frame);
        Assert.Equal(transferredIdentity, after.Item("done", "design").Identity);
    }

    [Fact]
    public async Task DeepLeafCanMoveToTheRootCollection()
    {
        var frame = await fixture.NavigateToStoryAsync(NestingStory);
        var before = await ReadModelAsync(frame);
        var leafIdentity = before.Item("web-children", "components").Identity;

        await DragAsync(frame.GetByTestId("item-components"), frame.GetByTestId("item-archive"));

        await AssertInvariantAsync(
            frame,
            Expected("root", "tree-root", "applications", "libraries", "components", "archive"),
            Expected("applications-children", "tree-children-applications", "web", "mobile"),
            Expected("web-children", "tree-children-web", "services"),
            Expected("libraries-children", "tree-children-libraries", "sortablejs"));
        var after = await ReadModelAsync(frame);
        Assert.Equal(leafIdentity, after.Item("root", "components").Identity);
    }

    [Fact]
    public async Task MultiDragMovesThreeNonAdjacentItemsInRelativeOrder()
    {
        var frame = await fixture.NavigateToStoryAsync(MultiDragStory);
        var controlClick = new LocatorClickOptions { Modifiers = [KeyboardModifier.Control] };
        await frame.GetByTestId("item-alpha").ClickAsync(controlClick);
        await frame.GetByTestId("item-charlie").ClickAsync(controlClick);
        await frame.GetByTestId("item-echo").ClickAsync(controlClick);

        // Dropping onto the list rather than its last row: MultiDrag pulls the selected rows out of
        // the flow, so the last row moves while the drag is in flight and its "after" edge is a
        // moving target. The container's lower region stays unambiguously past every row.
        await DragAsync(frame.GetByTestId("item-alpha"), frame.GetByTestId("multidrag-list"), afterTarget: true);

        await AssertInvariantAsync(
            frame,
            Expected("items", "multidrag-list", "bravo", "delta", "foxtrot", "alpha", "charlie", "echo"));
    }

    [Fact]
    public async Task CloneModeKeepsTheSourceAndCreatesADistinctInstance()
    {
        var frame = await fixture.NavigateToStoryAsync(CloneStory);
        var before = await ReadModelAsync(frame);
        var sourceIdentity = before.Item("palette", "heading").Identity;

        await DragAsync(frame.GetByTestId("item-heading"), frame.GetByTestId("canvas-list"));

        await AssertInvariantAsync(
            frame,
            Expected("palette", "palette-list", "heading", "paragraph", "button"),
            Expected("canvas", "canvas-list", "heading-clone-1"));
        var after = await ReadModelAsync(frame);
        Assert.Equal(sourceIdentity, after.Item("palette", "heading").Identity);
        Assert.NotEqual(sourceIdentity, after.Item("canvas", "heading-clone-1").Identity);
    }

    [Fact]
    public async Task SwapPluginExchangesTheTwoItems()
    {
        var frame = await fixture.NavigateToStoryAsync(SwapStory);

        await DragAsync(frame.GetByTestId("item-one"), frame.GetByTestId("item-three"));

        await AssertInvariantAsync(
            frame,
            Expected("items", "swap-list", "three", "two", "one", "four"));
    }

    [Fact]
    public async Task PutDisabledRejectsTheDropWithoutChangingEitherModel()
    {
        var frame = await fixture.NavigateToStoryAsync(GroupsStory);

        await DragAsync(frame.GetByTestId("item-scaffold"), frame.GetByTestId("item-design"));
        await fixture.Page.WaitForTimeoutAsync(500);

        await AssertInvariantAsync(
            frame,
            Expected("backlog", "backlog-list", "design", "implement"),
            Expected("done", "done-list", "scaffold"));
    }

    /// <summary>Which list a row sits in, and where in it - enough to tell whether a drag took.</summary>
    private static async Task<string> DescribePositionAsync(ILocator row)
    {
        return await row.EvaluateAsync<string>(
            "element => (element.parentElement?.getAttribute('data-testid') ?? '?') + ':' + Array.from(element.parentElement?.children ?? []).indexOf(element)");
    }

    private async Task<bool> HasDraggedElementMovedAsync(string originalSignature)
    {
        var dragged = fixture.Page.Locator(".sortable-chosen, .sortable-drag, [data-sortable-item].sortable-ghost").First;
        if (await dragged.CountAsync() == 0)
        {
            return false;
        }

        return await DescribePositionAsync(dragged) != originalSignature;
    }

    /// <summary>
    /// Chooses the point to drop on, in viewport coordinates.
    /// </summary>
    /// <remarks>
    /// A row in a tree encloses its own child list, so its box spans the whole subtree and its
    /// midpoint lands inside the nested list rather than in the list the row belongs to. Aiming at
    /// the row's own label keeps the pointer in the parent list, and above the row's midpoint,
    /// which is what makes SortableJS insert before it.
    /// </remarks>
    private static async Task<(float X, float Y)> ResolveDropPointAsync(
        ILocator target,
        ILocator targetLocator,
        bool afterTarget,
        LocatorBoundingBoxResult rowBox)
    {
        var hasNestedList = await target.Locator("[data-sortable-item]").CountAsync() > 0;
        var box = hasNestedList ? await targetLocator.BoundingBoxAsync() ?? rowBox : rowBox;
        return (box.X + box.Width / 2, box.Y + box.Height * (afterTarget ? 0.9f : 0.5f));
    }

    [Fact]
    public async Task SpillRevertPutsTheItemBackWhenDroppedOutsideEveryList()
    {
        var frame = await fixture.NavigateToStoryAsync(SpillStory);
        var before = await ReadModelAsync(frame);
        var identity = before.Item("reverting", "comes-back").Identity;

        await DragOutsideAsync(frame.GetByTestId("item-comes-back"));

        await AssertInvariantAsync(
            frame,
            Expected("reverting", "revert-list", "comes-back", "stays-put"),
            Expected("removing", "remove-list", "gets-removed", "stays-here"));
        var after = await ReadModelAsync(frame);
        Assert.Equal(identity, after.Item("reverting", "comes-back").Identity);
    }

    [Fact]
    public async Task SpillRemoveDropsTheItemFromTheModelWhenDroppedOutsideEveryList()
    {
        var frame = await fixture.NavigateToStoryAsync(SpillStory);

        await DragOutsideAsync(frame.GetByTestId("item-gets-removed"));

        await AssertInvariantAsync(
            frame,
            Expected("reverting", "revert-list", "comes-back", "stays-put"),
            Expected("removing", "remove-list", "stays-here"));
    }

    [Fact]
    public async Task AutoScrollScrollsTheContainerWhileDraggingTowardsItsEdge()
    {
        var frame = await fixture.NavigateToStoryAsync(AutoScrollStory);
        var container = frame.GetByTestId("scroll-container");
        var scrollTopBefore = await container.EvaluateAsync<int>("element => element.scrollTop");
        Assert.Equal(0, scrollTopBefore);

        var source = await ResolveDraggableAsync(frame.GetByTestId("item-row-1"));
        var sourceBox = await source.BoundingBoxAsync();
        var containerBox = await container.BoundingBoxAsync();
        Assert.NotNull(sourceBox);
        Assert.NotNull(containerBox);

        await fixture.Page.Mouse.MoveAsync(sourceBox.X + sourceBox.Width / 2, sourceBox.Y + sourceBox.Height / 2);
        await fixture.Page.Mouse.DownAsync();
        await fixture.Page.WaitForTimeoutAsync(50);

        // Hold just inside the bottom edge, within ScrollSensitivity, and let the scroll loop run.
        var edgeX = containerBox.X + containerBox.Width / 2;
        var edgeY = containerBox.Y + containerBox.Height - 8;
        for (var step = 1; step <= 30; step++)
        {
            await fixture.Page.Mouse.MoveAsync(edgeX, edgeY - step % 2);
            await fixture.Page.WaitForTimeoutAsync(50);
        }

        var scrollTopDuringDrag = await container.EvaluateAsync<int>("element => element.scrollTop");
        await fixture.Page.Mouse.UpAsync();
        await fixture.Page.WaitForTimeoutAsync(300);

        Assert.True(
            scrollTopDuringDrag > 0,
            $"The container should have auto-scrolled while dragging at its edge, but scrollTop stayed at {scrollTopDuringDrag}.");
    }

    /// <summary>
    /// Drags an item well clear of every list so SortableJS treats the release as a spill.
    /// </summary>
    private async Task DragOutsideAsync(ILocator sourceLocator)
    {
        var source = await ResolveDraggableAsync(sourceLocator);
        var sourceBox = await source.BoundingBoxAsync();
        Assert.NotNull(sourceBox);

        var viewport = fixture.Page.ViewportSize!;
        var startX = sourceBox.X + sourceBox.Width / 2;
        var startY = sourceBox.Y + sourceBox.Height / 2;
        var endX = viewport.Width - 40;
        var endY = viewport.Height - 40;

        await fixture.Page.Mouse.MoveAsync(startX, startY);
        await fixture.Page.Mouse.DownAsync();
        await fixture.Page.WaitForTimeoutAsync(50);
        for (var step = 1; step <= 16; step++)
        {
            var progress = (float)step / 16;
            await fixture.Page.Mouse.MoveAsync(startX + (endX - startX) * progress, startY + (endY - startY) * progress);
            await fixture.Page.WaitForTimeoutAsync(30);
        }

        await fixture.Page.WaitForTimeoutAsync(150);
        await fixture.Page.Mouse.UpAsync();
        await fixture.Page.WaitForTimeoutAsync(150);
    }

    private static ExpectedCollection Expected(string modelName, string listTestId, params string[] keys) =>
        new(modelName, listTestId, keys);

    /// <summary>
    /// Resolves a locator to the element SortableJS actually treats as draggable.
    /// </summary>
    /// <remarks>
    /// Stories put their <c>data-testid</c> on the inner content span, but the component marks the
    /// row with <c>data-sortable-item</c> and SortableJS is configured with
    /// <c>draggable: "&gt; [data-sortable-item]"</c>. Measuring the span yields a text-sized box,
    /// so the pointer never crosses the row's midpoint and no sort fires. List containers have no
    /// such ancestor, so fall back to the locator itself.
    /// </remarks>
    private static async Task<ILocator> ResolveDraggableAsync(ILocator locator)
    {
        var row = locator.Locator("xpath=ancestor-or-self::*[@data-sortable-item][1]");
        return await row.CountAsync() > 0 ? row : locator;
    }

    private async Task DragAsync(ILocator sourceLocator, ILocator targetLocator, bool afterTarget = false)
    {
        var source = await ResolveDraggableAsync(sourceLocator);
        var target = await ResolveDraggableAsync(targetLocator);

        var sourceBox = await source.BoundingBoxAsync();
        Assert.NotNull(sourceBox);
        Assert.NotNull(await target.BoundingBoxAsync());

        var startX = sourceBox.X + sourceBox.Width / 2;
        var startY = sourceBox.Y + sourceBox.Height / 2;
        await fixture.Page.Mouse.MoveAsync(startX, startY);
        await fixture.Page.Mouse.DownAsync();
        await fixture.Page.WaitForTimeoutAsync(50);

        // MultiDrag and nested lists reflow once fallback dragging activates, so resolve the
        // destination after the drag has started rather than before.
        var targetBox = await target.BoundingBoxAsync();
        Assert.NotNull(targetBox);

        // A row in a tree encloses its own child list, so its box spans the whole subtree and its
        // midpoint lands inside the nested list rather than in the list the row belongs to.
        // Aiming at the row's own label keeps the pointer in the parent list, and above the row's
        // midpoint, which is what makes SortableJS insert before it.
        var (endX, endY) = await ResolveDropPointAsync(target, targetLocator, afterTarget, targetBox);

        // Step along the path in increments no larger than half a row. SortableJS's fallback path
        // decides a swap when the pointer crosses a *sibling's* midpoint, so a few small jitters
        // inside the source row - or one long jump past several rows - produce no sort at all.
        var distance = Math.Max(Math.Abs(endX - startX), Math.Abs(endY - startY));
        var stride = Math.Max(sourceBox.Height / 2, 8);
        var steps = Math.Max(8, (int)Math.Ceiling(distance / stride));
        for (var step = 1; step <= steps; step++)
        {
            // The destination shifts while the drag is in flight: rows are removed from the source
            // list and inserted into whichever list the pointer is over, so a drop point computed
            // at mouse-down is stale by the time the pointer arrives. Re-steer as we go.
            if (step % 3 == 0)
            {
                var currentBox = await target.BoundingBoxAsync();
                if (currentBox is not null)
                {
                    (endX, endY) = await ResolveDropPointAsync(target, targetLocator, afterTarget, currentBox);
                }
            }

            var progress = (float)step / steps;
            await fixture.Page.Mouse.MoveAsync(
                startX + (endX - startX) * progress,
                startY + (endY - startY) * progress);
            await fixture.Page.WaitForTimeoutAsync(30);
        }

        // Settle on the final position so the last dragover lands where we intend.
        var finalBox = await target.BoundingBoxAsync();
        if (finalBox is not null)
        {
            var (finalX, finalY) = await ResolveDropPointAsync(target, targetLocator, afterTarget, finalBox);
            await fixture.Page.Mouse.MoveAsync(finalX, finalY);
            await fixture.Page.WaitForTimeoutAsync(60);
            await fixture.Page.Mouse.MoveAsync(finalX, finalY + 1);
            await fixture.Page.WaitForTimeoutAsync(60);
        }

        await fixture.Page.WaitForTimeoutAsync(150);
        await fixture.Page.Mouse.UpAsync();
        await fixture.Page.WaitForTimeoutAsync(150);
    }

    private async Task AssertInvariantAsync(ILocator frame, params ExpectedCollection[] expected)
    {
        // A drag can leave the model perfectly correct while Blazor throws rendering the result,
        // so every scenario checks the console as well as the data.
        fixture.AssertNoJsErrors();

        var state = await WaitForExpectedModelAsync(frame, expected, TimeSpan.FromSeconds(10));
        Assert.Equal(expected.Select(item => item.ModelName).Order(), state.Collections.Keys.Order());

        var actualItems = state.Collections.Values.SelectMany(items => items).ToArray();
        var expectedKeys = expected.SelectMany(collection => collection.Keys).ToArray();
        Assert.Equal(expectedKeys.Order(), actualItems.Select(item => item.Key).Order());
        Assert.Equal(actualItems.Length, actualItems.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(actualItems, item => Assert.Equal(item.InitialIdentity, item.Identity));

        foreach (var collection in expected)
        {
            var modelOrder = state.Collections[collection.ModelName].Select(item => item.Key).ToArray();
            Assert.Equal(collection.Keys, modelOrder);

            var domOrder = await WaitForDomOrderAsync(frame, collection.ListTestId, modelOrder, TimeSpan.FromSeconds(10));
            Assert.Equal(modelOrder, domOrder);
        }
    }

    /// <summary>
    /// Polls the rendered row order until it agrees with the model.
    /// </summary>
    /// <remarks>
    /// Fallback dragging leaves a cloned row in the list for a moment after the drop, and Blazor
    /// re-renders the model readout before the rows settle - so the DOM can still show the old
    /// order (plus the clone) when the model is already correct. Reading it once turns that lag
    /// into a spurious failure.
    /// </remarks>
    private static async Task<string[]> WaitForDomOrderAsync(
        ILocator frame,
        string listTestId,
        IReadOnlyList<string> expectedOrder,
        TimeSpan timeout)
    {
        var domItems = frame.Locator($"[data-testid='{listTestId}'] > [data-sortable-item] > [data-item-key]");
        var deadline = DateTime.UtcNow + timeout;
        var domOrder = Array.Empty<string>();
        while (DateTime.UtcNow < deadline)
        {
            domOrder = await domItems.EvaluateAllAsync<string[]>(
                "elements => elements.map(element => element.getAttribute('data-item-key'))");
            if (domOrder.SequenceEqual(expectedOrder))
            {
                return domOrder;
            }

            await Task.Delay(100);
        }

        return domOrder;
    }

    private static async Task<ModelState> WaitForExpectedModelAsync(
        ILocator frame,
        IReadOnlyList<ExpectedCollection> expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        ModelState? last = null;
        while (DateTime.UtcNow < deadline)
        {
            last = await ReadModelAsync(frame);
            if (expected.All(collection =>
                    last.Collections.TryGetValue(collection.ModelName, out var items) &&
                    items.Select(item => item.Key).SequenceEqual(collection.Keys)))
            {
                return last;
            }

            await Task.Delay(100);
        }

        var rendered = last is null ? "<unavailable>" : JsonSerializer.Serialize(last);
        throw new Xunit.Sdk.XunitException($"The C# model did not reach the expected state. Last state: {rendered}");
    }

    private static async Task<ModelState> ReadModelAsync(ILocator frame)
    {
        var json = await frame.GetByTestId("model-state").TextContentAsync();
        Assert.False(string.IsNullOrWhiteSpace(json));
        return JsonSerializer.Deserialize<ModelState>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new Xunit.Sdk.XunitException("The story's model-state JSON was null.");
    }

    private sealed record ExpectedCollection(string ModelName, string ListTestId, string[] Keys);

    private sealed record ModelState(Dictionary<string, List<ModelItem>> Collections)
    {
        public ModelItem Item(string collection, string key) =>
            Collections[collection].Single(item => item.Key == key);
    }

    private sealed record ModelItem(string Key, string Label, int Identity, int InitialIdentity);
}
