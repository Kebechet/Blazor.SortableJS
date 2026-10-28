[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Blazor.SortableJS
[![NuGet Version](https://img.shields.io/nuget/v/Kebechet.Blazor.SortableJS)](https://www.nuget.org/packages/Kebechet.Blazor.SortableJS/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Kebechet.Blazor.SortableJS)](https://www.nuget.org/packages/Kebechet.Blazor.SortableJS/)
[![Build](https://github.com/Kebechet/Blazor.SortableJS/actions/workflows/build.yml/badge.svg)](https://github.com/Kebechet/Blazor.SortableJS/actions/workflows/build.yml)
[![codecov](https://codecov.io/gh/Kebechet/Blazor.SortableJS/graph/badge.svg)](https://codecov.io/gh/Kebechet/Blazor.SortableJS)
[![Storybook](https://img.shields.io/badge/storybook-live%20demo-ff4785)](https://kebechet.github.io/Blazor.SortableJS/)
![Last updated](https://img.shields.io/github/last-commit/Kebechet/Blazor.SortableJS/main?label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

A typed Blazor wrapper for [SortableJS 1.15.7](https://github.com/SortableJS/Sortable). The pinned bundle is a static web asset and is registered automatically: no npm install, CDN, or script tag is required.

**[Live storybook](https://kebechet.github.io/Blazor.SortableJS/)** - interactive stories for every feature.

## Installation

```bash
dotnet add package Kebechet.Blazor.SortableJS
```

## Usage

The bound `IList<T>` is reordered in place. Item objects are never serialized through JavaScript, so their reference identity is preserved.

```razor
@using Kebechet.Blazor.SortableJS

<Sortable Items="_exercises" Context="exercise"
          Options="_options"
          ItemClass="exercise-card">
    <ItemTemplate>@exercise.Name</ItemTemplate>
</Sortable>

@code {
    private readonly List<Exercise> _exercises = new()
    {
        new("Squat"),
        new("Bench press"),
        new("Deadlift")
    };

    private readonly SortableOptions _options = new()
    {
        AnimationDuration = 150,
        GhostClass = "drag-ghost"
    };

    private sealed record Exercise(string Name);
}
```

Connected and nested lists only need the same group name. Each component registers itself automatically, regardless of nesting depth.

```razor
<Sortable Items="_backlog" Options="_connected" Context="item">
    <ItemTemplate>@item</ItemTemplate>
</Sortable>
<Sortable Items="_done" Options="_connected" Context="item">
    <ItemTemplate>@item</ItemTemplate>
</Sortable>

@code {
    private readonly List<string> _backlog = new() { "Design", "Implement" };
    private readonly List<string> _done = new() { "Verify" };
    private readonly SortableOptions _connected = new()
    {
        Group = new SortableGroupOptions
        {
            Name = "work",
            PullMode = PullMode.Enabled,
            PutMode = PutMode.Enabled
        }
    };
}
```

Use `CloneFunction` to create a distinct object for clone mode, or `TryConvertFunction` on a destination whose item type differs from the source - returning `false` refuses the item and leaves both collections untouched, so a rejection does not have to be an exception. `ShouldUseItemKeys` is enabled by default; set `ItemKeySelector` when the item itself is not the desired stable key.

`Items` may be null, which makes the list accept-only: it takes drops and raises the usual callbacks but stores nothing, and items still leave their source collection. That is a delete or archive zone without a throwaway list behind it. `IsItemDraggable` marks individual rows undraggable without hand-rolling a marker class and filter selector.

### Defaults

Register them so they are scoped like any other service:

```csharp
builder.Services.AddSortableJs(options => options.AnimationDuration = 150);
```

`SortableDefaults.Options` still works and is fine to assign once at startup. Avoid it on Blazor Server for anything user-specific: the static is shared by every circuit, so a per-user or per-tenant default would change behaviour for everyone connected. A registered `ISortableDefaults` takes precedence.

### Callbacks observe; decisions decide

All fifteen callbacks use `SortableEventArgs<TItem>`. `OnAdd` runs before collection mutation, while the moved reference is still in the source; `OnRemove` follows. `OldIndexes` and `NewIndexes` contain every affected index for MultiDrag, not only the primary item.

They are **observational**. SortableJS reads the return value of `onMove`, `group.pull` and `group.put` synchronously, and an `EventCallback` is asynchronous, so `OnMove` cannot veto a drop or steer placement. Three separate parameters do that:

```csharp
<Sortable Items="_items"
          MoveDecision="context => context.RelatedItem?.IsPinned == true
              ? SortableMoveDecision.Reject
              : SortableMoveDecision.Default"
          CanAcceptItem="context => context.Item?.IsArchived == false" />
```

`MoveDecision` can reject a move or force insertion before or after the item under the pointer; `CanAcceptItem` and `CanReleaseItem` are the per-item `put` and `pull` predicates the fixed group modes cannot express.

`CanAcceptItem` and `CanReleaseItem` work everywhere. They are wired to the SortableJS group functions on WebAssembly, so the drag is refused as the pointer moves, and they are enforced again in .NET when the drop is applied - which is what makes them hold on Blazor Server, and for **every** item of a MultiDrag selection rather than only the one under the pointer, since SortableJS asks its group functions about that one alone.

`MoveDecision` is **WebAssembly only**: it steers where an item lands while the pointer is still moving, which nothing can do after the fact. Setting it under Blazor Server throws `PlatformNotSupportedException` rather than silently never taking effect.

## Coverage vs. SortableJS 1.15.7

| Axis | SortableJS 1.15.7 | This package |
|---|---:|---:|
| Options | 47 | 47 |
| Events | 15 | 15 |

The counts come from the actual vendored 1.15.7 source: 33 core options, 6 AutoScroll options, 2 OnSpill options, 2 Swap options, and 4 MultiDrag options. The event count is the 12 core callbacks plus MultiDrag `select`/`deselect` and OnSpill `spill`. Internal plugin hooks are excluded. Function-valued JavaScript options are represented by typed Blazor-friendly forms: local-storage `SortableStoreOptions`, `SetDataText`/`SetDataTextSelector`, `SortableDirection`, CSS filter and scroll-container selectors, and `ShouldContinueNativeScrolling`.

## Patched SortableJS

The vendored `Sortable.min.js` is the official 1.15.7 build plus one change. The change is the fix proposed in [SortableJS pull request 2483](https://github.com/SortableJS/Sortable/pull/2483) for [SortableJS issue 2307](https://github.com/SortableJS/Sortable/issues/2307), carried until a SortableJS release includes it.

**What it changes.** During a fallback drag (any touch drag, or `ForceFallback`), `_emulateDragOver` and the empty-list detector now ignore the drag once the dragged element is no longer in the DOM. Without the change:

- `_emulateDragOver` throws `Cannot read properties of null (reading 'Sortable…')` every 50 ms.
- The empty-list detector puts the removed element back into a nearby empty list.
- The drop then reports a move for a row Blazor has already removed, so a different item moves and the DOM no longer matches the collection.

**How a Blazor app gets there.** After a drop, the interop restores the DOM and queues the event for .NET. The move appears only with the next render, which on Blazor Server and in a MAUI BlazorWebView is a round trip away. If the user grabs the restored row again before that render arrives, the render removes the very element being dragged.

The patch is removed once a SortableJS release contains the fix. Until then, `PackagingTests` fails if a bundle upgrade drops it.

## Features

- In-place same-list, cross-list, and multi-item moves with reference identity preserved
- Automatic depth-independent registration for recursive lists
- Pull/put group policies, clone mode, and cross-type conversion that can decline an item
- Synchronous move, put and pull decisions on WebAssembly - veto a drop or override its position
- Accept-only drop zones, and per-item draggability
- MultiDrag, Swap, AutoScroll, RevertOnSpill, and RemoveOnSpill
- Stable keyed rendering, and defaults through DI or a static
- DOM rollback before Blazor mutation, full event coverage, and deterministic disposal
- `net6.0` through `net10.0`

## License

[MIT](LICENSE)
