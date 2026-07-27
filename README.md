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

These need synchronous interop and so are **WebAssembly only**. Setting one under Blazor Server throws `PlatformNotSupportedException` rather than silently never taking effect.

## Coverage vs. SortableJS 1.15.7

| Axis | SortableJS 1.15.7 | This package |
|---|---:|---:|
| Options | 47 | 47 |
| Events | 15 | 15 |

The counts come from the actual vendored 1.15.7 source: 33 core options, 6 AutoScroll options, 2 OnSpill options, 2 Swap options, and 4 MultiDrag options. The event count is the 12 core callbacks plus MultiDrag `select`/`deselect` and OnSpill `spill`. Internal plugin hooks are excluded. Function-valued JavaScript options are represented by typed Blazor-friendly forms: local-storage `SortableStoreOptions`, `SetDataText`/`SetDataTextSelector`, `SortableDirection`, CSS filter and scroll-container selectors, and `ShouldContinueNativeScrolling`.

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
