using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;
using TestContext = Bunit.TestContext;

namespace Kebechet.Blazor.SortableJS.Tests;

public sealed class SortableElementIdTests : IDisposable
{
    private const string ModulePath = "./_content/Kebechet.Blazor.SortableJS/sortable-interop.js";

    private readonly TestContext _context;

    public SortableElementIdTests()
    {
        _context = new TestContext();
        _context.JSInterop.SetupModule(ModulePath).Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// A counter declared inside <c>Sortable&lt;TItem&gt;</c> would get separate storage per closed
    /// generic type, so two item types would both start at 1 and collide in the shared registry.
    /// </summary>
    [Fact]
    public void Generated_ids_are_unique_across_item_types()
    {
        // Arrange & Act
        var first = _context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(x => x.Items, new List<string> { "a" })
            .Add(x => x.ItemTemplate, (RenderFragment<string>)(item => builder => builder.AddContent(0, item))));
        var second = _context.RenderComponent<Sortable<int>>(parameters => parameters
            .Add(x => x.Items, new List<int> { 1 })
            .Add(x => x.ItemTemplate, (RenderFragment<int>)(item => builder => builder.AddContent(0, item))));

        // Assert
        var firstId = first.Find("[id^='kebechet-sortable-']").Id;
        var secondId = second.Find("[id^='kebechet-sortable-']").Id;
        firstId.ShouldNotBe(secondId);
    }
}
