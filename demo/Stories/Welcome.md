---
$attribute: CustomPage("Blazor.SortableJS/Welcome")
---

# Blazor.SortableJS

A typed Blazor wrapper for [SortableJS](https://github.com/SortableJS/Sortable). The pinned bundle
ships inside the package as a static web asset and is registered on startup, so there is no npm
install, no CDN and no `<script>` tag to add.

```bash
dotnet add package Kebechet.Blazor.SortableJS
```

```razor
@using Kebechet.Blazor.SortableJS

<Sortable Items="_exercises" Context="exercise">
    <ItemTemplate>@exercise.Name</ItemTemplate>
</Sortable>
```

## The thing worth knowing

**The bound `IList<T>` is reordered in place, and item objects never travel through JavaScript.**
JavaScript reports indexes and container ids; the move itself happens in .NET against the very same
instances. Reference equality survives a drag, including a drag between two lists, so `Contains`,
`Equals` and any child component state keep working.

Every story prints its live C# collection underneath, including each item's CLR identity captured
at construction. Drag something and watch the order change while the identities do not - that is
the guarantee, made visible.

## The stories

| Story | Shows |
|---|---|
| **Basic** | Reordering one list, with identities preserved |
| **Groups** | Two connected lists, and a `put`-disabled list refusing a drop |
| **Nesting** | Lists nested to arbitrary depth, sharing one group |
| **MultiDrag** | Selecting several rows and moving them as one |
| **Clone** | A palette that keeps its items and hands out copies |
| **Swap** | Exchanging two positions instead of shifting the list |
| **Auto-scroll** | A scroll container that follows the pointer to its edge |
| **OnSpill** | Revert-on-spill and remove-on-spill, side by side |

Each story's **Docs** tab lists every parameter with its description; the **Controls** panel lets
you change them live.

## Links

- [Source and issues](https://github.com/Kebechet/Blazor.SortableJS)
- [NuGet package](https://www.nuget.org/packages/Kebechet.Blazor.SortableJS/)
- [SortableJS itself](https://github.com/SortableJS/Sortable)
