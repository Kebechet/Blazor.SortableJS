using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Shouldly;
using Xunit;

namespace Kebechet.Blazor.SortableJS.Tests;

#pragma warning disable BL0006 // This regression test intentionally inspects Blazor's render batch.

public sealed class SortableRenderDiffTests
{
    [Fact]
    public async Task Keyed_reorder_does_not_emit_item_attribute_edits()
    {
        // Arrange
        using var services = new ServiceCollection()
            .AddSingleton<IJSRuntime, StubJSRuntime>()
            .BuildServiceProvider();
        using var renderer = new CapturingRenderer(services);
        var items = new List<TestItem> { new("first"), new("second"), new("third") };
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Sortable<TestItem>.Id)] = "render-diff",
            [nameof(Sortable<TestItem>.Items)] = items,
            [nameof(Sortable<TestItem>.ItemClass)] = "demo-item",
            [nameof(Sortable<TestItem>.ItemKeySelector)] = new Func<TestItem, object>(item => item.Name),
            [nameof(Sortable<TestItem>.ItemTemplate)] =
                (RenderFragment<TestItem>)(item => builder => builder.AddContent(0, item.Name))
        };
        var componentId = await renderer.AttachAsync<Sortable<TestItem>>();
        await renderer.RenderAsync(componentId, parameters);
        renderer.ClearEdits();

        // Act
        var moved = items[0];
        items.RemoveAt(0);
        items.Add(moved);
        await renderer.RenderAsync(componentId, parameters);

        // Assert
        renderer.Edits.ShouldNotContain(edit => edit.Type == RenderTreeEditType.SetAttribute);
        renderer.Edits.ShouldNotContain(edit => edit.Type == RenderTreeEditType.RemoveAttribute);
        renderer.Edits.Count(edit => edit.Type == RenderTreeEditType.PermutationListEntry).ShouldBe(3);
    }

    private sealed class CapturingRenderer(IServiceProvider services)
        : Renderer(services, NullLoggerFactory.Instance)
    {
        private readonly List<CapturedEdit> _edits = [];

        internal IReadOnlyList<CapturedEdit> Edits => _edits;

        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        internal Task<int> AttachAsync<TComponent>() where TComponent : IComponent
        {
            return Dispatcher.InvokeAsync(() =>
            {
                var component = InstantiateComponent(typeof(TComponent));
                return AssignRootComponentId(component);
            });
        }

        internal Task RenderAsync(int componentId, IDictionary<string, object?> parameters)
        {
            return Dispatcher.InvokeAsync(() =>
                RenderRootComponentAsync(componentId, ParameterView.FromDictionary(parameters)));
        }

        internal void ClearEdits()
        {
            _edits.Clear();
        }

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
        {
            for (var diffIndex = 0; diffIndex < renderBatch.UpdatedComponents.Count; diffIndex++)
            {
                var diff = renderBatch.UpdatedComponents.Array[diffIndex];
                for (var editIndex = 0; editIndex < diff.Edits.Count; editIndex++)
                {
                    var edit = diff.Edits[editIndex];
                    _edits.Add(new CapturedEdit(edit.Type));
                }
            }

            return Task.CompletedTask;
        }

        protected override void HandleException(Exception exception)
        {
            throw exception;
        }
    }

    private sealed class StubJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            return typeof(TValue) == typeof(IJSObjectReference)
                ? ValueTask.FromResult((TValue)(object)new StubJSObjectReference())
                : ValueTask.FromResult(default(TValue)!);
        }
    }

    private sealed class StubJSObjectReference : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            return typeof(TValue) == typeof(IJSObjectReference)
                ? ValueTask.FromResult((TValue)(object)new StubJSObjectReference())
                : ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed record CapturedEdit(RenderTreeEditType Type);

    private sealed record TestItem(string Name);
}

#pragma warning restore BL0006
