using Bunit;
using Shouldly;
using Xunit;
using TestContext = Bunit.TestContext;

namespace Kebechet.Blazor.SortableJS.Tests;

/// <summary>
/// SortableJS asks whether a move is allowed synchronously and acts on the answer, so these
/// decisions cannot travel over the asynchronous callback path the other fifteen events use.
/// </summary>
/// <remarks>
/// The components here are constructed rather than rendered. Rendering one would trip the guard
/// that refuses a synchronous decision off WebAssembly, which a test host is not - that guard has
/// its own test below. Without a lifecycle the generated id is empty, so an empty SourceId is how a
/// request says "this list", and any other value stands for a different one.
/// </remarks>
public sealed class SortableDecisionTests
{
    private const string ThisList = "";

    [Theory]
    [InlineData(SortableMoveDecision.Default, 0)]
    [InlineData(SortableMoveDecision.Reject, 1)]
    [InlineData(SortableMoveDecision.InsertBefore, 2)]
    [InlineData(SortableMoveDecision.InsertAfter, 3)]
    public void A_move_decision_crosses_the_boundary_as_its_numeric_value(SortableMoveDecision decision, int expected)
    {
        // Arrange
        var component = Create();
        component.MoveDecision = _ => decision;

        // Act
        var result = component.DecideMove(new SortableDecisionRequest { SourceId = ThisList });

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void A_move_decision_sees_the_dragged_and_related_items_of_its_own_list()
    {
        // Arrange
        SortableMoveContext<string>? observed = null;
        var component = Create();
        component.MoveDecision = context =>
        {
            observed = context;
            return SortableMoveDecision.Reject;
        };

        // Act
        component.DecideMove(new SortableDecisionRequest
        {
            SourceId = ThisList,
            DestinationId = ThisList,
            DraggedIndex = 0,
            RelatedIndex = 2,
            WillInsertAfter = true
        });

        // Assert
        observed.ShouldNotBeNull();
        observed.Item.ShouldBe("alpha");
        observed.RelatedItem.ShouldBe("charlie");
        observed.WillInsertAfter.ShouldBeTrue();
    }

    [Fact]
    public void An_index_belonging_to_another_list_does_not_resolve_to_a_local_item()
    {
        // Arrange
        SortableMoveContext<string>? observed = null;
        var component = Create();
        component.MoveDecision = context =>
        {
            observed = context;
            return SortableMoveDecision.Default;
        };

        // Act
        component.DecideMove(new SortableDecisionRequest
        {
            SourceId = "a-different-list",
            DestinationId = ThisList,
            DraggedIndex = 0,
            RelatedIndex = 1
        });

        // Assert
        observed.ShouldNotBeNull();
        observed.Item.ShouldBeNull();
        observed.RelatedItem.ShouldBe("bravo");
    }

    [Fact]
    public void Absent_decisions_accept_everything()
    {
        // Arrange
        var component = Create();

        // Act & Assert
        component.DecidePut(new SortableDecisionRequest()).ShouldBeTrue();
        component.DecidePull(new SortableDecisionRequest()).ShouldBeTrue();
        component.DecideMove(new SortableDecisionRequest()).ShouldBe((int)SortableMoveDecision.Default);
    }

    [Fact]
    public void A_put_decision_can_refuse_an_incoming_item()
    {
        // Arrange
        var component = Create();
        component.CanAcceptItem = context => context.SourceId == "trusted";

        // Act & Assert
        component.DecidePut(new SortableDecisionRequest { SourceId = "trusted" }).ShouldBeTrue();
        component.DecidePut(new SortableDecisionRequest { SourceId = "other" }).ShouldBeFalse();
    }

    [Fact]
    public void A_pull_decision_can_keep_an_item_in_its_list()
    {
        // Arrange
        var component = Create();
        component.CanReleaseItem = context => context.Item != "alpha";

        // Act & Assert
        component.DecidePull(new SortableDecisionRequest { SourceId = ThisList, DraggedIndex = 0 }).ShouldBeFalse();
        component.DecidePull(new SortableDecisionRequest { SourceId = ThisList, DraggedIndex = 1 }).ShouldBeTrue();
    }

    [Fact]
    public void Configuring_a_move_decision_off_webassembly_fails_loudly()
    {
        // Arrange
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        // Act
        var render = () => context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(child => child.Items, new List<string> { "alpha" })
            .Add(child => child.MoveDecision, _ => SortableMoveDecision.Reject)
            .Add(child => child.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Assert
        var exception = Should.Throw<PlatformNotSupportedException>(render);
        exception.Message.ShouldContain(nameof(Sortable<string>.MoveDecision));
    }

    [Fact]
    public void A_transfer_predicate_works_without_webassembly()
    {
        // Arrange & Act - only MoveDecision needs synchronous interop. The transfer predicates are
        // enforced in .NET when the drop is applied, so a test host, and Blazor Server, can use them.
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var render = () => context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(child => child.Items, new List<string> { "alpha" })
            .Add(child => child.CanAcceptItem, _ => true)
            .Add(child => child.CanReleaseItem, _ => true)
            .Add(child => child.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Assert
        Should.NotThrow(render);
    }

    [Fact]
    public void A_decision_assigned_after_the_first_render_still_fails_loudly()
    {
        // Arrange
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var component = context.RenderComponent<Sortable<string>>(parameters => parameters
            .Add(child => child.Items, new List<string> { "alpha" })
            .Add(child => child.ItemTemplate, item => builder => builder.AddContent(0, item)));

        // Act
        var assignLater = () => component.SetParametersAndRender(parameters => parameters
            .Add(child => child.MoveDecision, _ => SortableMoveDecision.Reject));

        // Assert
        Should.Throw<PlatformNotSupportedException>(assignLater);
    }

    private static Sortable<string> Create()
    {
        return new Sortable<string> { Items = new List<string> { "alpha", "bravo", "charlie" } };
    }
}
