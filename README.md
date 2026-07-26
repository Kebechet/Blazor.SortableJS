[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Blazor.SortableJS
[![NuGet Version](https://img.shields.io/nuget/v/Kebechet.Blazor.SortableJS)](https://www.nuget.org/packages/Kebechet.Blazor.SortableJS/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Kebechet.Blazor.SortableJS)](https://www.nuget.org/packages/Kebechet.Blazor.SortableJS/)
[![Build](https://github.com/Kebechet/Blazor.SortableJS/actions/workflows/build.yml/badge.svg)](https://github.com/Kebechet/Blazor.SortableJS/actions/workflows/build.yml)
[![codecov](https://codecov.io/gh/Kebechet/Blazor.SortableJS/graph/badge.svg)](https://codecov.io/gh/Kebechet/Blazor.SortableJS)
![Last updated](https://img.shields.io/github/last-commit/Kebechet/Blazor.SortableJS/main?label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

A typed Blazor wrapper for [SortableJS 1.15.7](https://github.com/SortableJS/Sortable). The pinned bundle is a static web asset and is registered automatically: no npm install, CDN, or script tag is required.

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

Use `CloneFunction` to create a distinct object for clone mode, or `ConvertFunction` on a destination whose item type differs from the source. `ShouldUseItemKeys` is enabled by default; set `ItemKeySelector` when the item itself is not the desired stable key. `SortableDefaults.Options` supplies app-wide defaults.

All fifteen callbacks use `SortableEventArgs<TItem>`. `OnAdd` runs before collection mutation, while the moved reference is still in the source; `OnRemove` follows. `OldIndexes` and `NewIndexes` contain every affected index for MultiDrag, not only the primary item.

## Coverage vs. SortableJS 1.15.7

| Axis | SortableJS 1.15.7 | This package |
|---|---:|---:|
| Options | 47 | 47 |
| Events | 15 | 15 |

The counts come from the actual vendored 1.15.7 source: 33 core options, 6 AutoScroll options, 2 OnSpill options, 2 Swap options, and 4 MultiDrag options. The event count is the 12 core callbacks plus MultiDrag `select`/`deselect` and OnSpill `spill`. Internal plugin hooks are excluded. Function-valued JavaScript options are represented by typed Blazor-friendly forms: local-storage `SortableStoreOptions`, `SetDataText`/`SetDataTextSelector`, `SortableDirection`, CSS filter and scroll-container selectors, and `ShouldContinueNativeScrolling`.

## Features

- In-place same-list, cross-list, and multi-item moves with reference identity preserved
- Automatic depth-independent registration for recursive lists
- Pull/put group policies, clone mode, and cross-type conversion
- MultiDrag, Swap, AutoScroll, RevertOnSpill, and RemoveOnSpill
- Stable keyed rendering and app-wide defaults
- DOM rollback before Blazor mutation, full event coverage, and deterministic disposal
- `net6.0` through `net10.0`

## License

[MIT](LICENSE)
