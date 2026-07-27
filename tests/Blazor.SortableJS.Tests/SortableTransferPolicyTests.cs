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
    public async Task A_declined_conversion_leaves_both_collections_untouched()
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
    public async Task An_accepted_conversion_transfers_the_converted_item()
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
    public async Task An_accept_only_list_takes_the_item_off_its_source_and_stores_nothing()
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
    public void An_undraggable_item_is_marked_for_the_filter_selector()
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
    public void Registered_defaults_are_preferred_over_the_static_ones()
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
