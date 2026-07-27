# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

`Kebechet.Blazor.SortableJS` is a typed Blazor wrapper over [SortableJS](https://github.com/SortableJS/Sortable).
The pinned upstream bundle ships inside the package as a static web asset - there is no npm step,
no CDN and no `<script>` tag for consumers to add.

## Layout

| Path | What it is |
|---|---|
| `src/Blazor.SortableJS/` | The package. RCL, `Microsoft.NET.Sdk.Razor`, root namespace `Kebechet.Blazor.SortableJS` |
| `src/Blazor.SortableJS/wwwroot/Sortable.min.js` | Vendored upstream bundle, pinned. Never swap for a CDN reference |
| `src/Blazor.SortableJS/wwwroot/sortable-interop.js` | The interop module - the JS side of every event |
| `demo/` | BlazingStory storybook, eight stories. Also the app the e2e tests drive |
| `tests/Blazor.SortableJS.Tests/` | bUnit + xUnit v3 unit tests |
| `tests/Blazor.SortableJS.E2E/` | Playwright tests driving real Chrome |

Style follows the global Kebechet conventions (tabs, no `#region`, LINQ one method per line,
`.IsNullOrEmpty()`, `is null`, no comments on self-explaining code).

## Build & test

```bash
dotnet build src/Blazor.SortableJS.slnx -c Release
dotnet test  tests/Blazor.SortableJS.Tests/Blazor.SortableJS.Tests.csproj -c Release
dotnet test  tests/Blazor.SortableJS.E2E/Blazor.SortableJS.E2E.csproj   -c Release
```

The e2e suite starts the demo itself on a dynamic port and launches Chrome via `Channel="chrome"`.
It runs on `pull_request` and `workflow_dispatch` only, never on push - it is far too slow for a
push gate.

`.gitignore` carries an unignore for the e2e project. The stock Visual Studio template ignores
`*.e2e`, and git matches case-insensitively on Windows, so the pattern silently swallowed the whole
`tests/Blazor.SortableJS.E2E/` directory. Do not remove `!/tests/Blazor.SortableJS.E2E/`.

`GeneratePackageOnBuild` is on. Produce the `.nupkg` with `dotnet build -c Release`, and verify the
static web assets actually made it in:

```bash
unzip -l *.nupkg | grep -E "staticwebassets|build/"
```

## What the tests cannot see

This matters more than it sounds. Six real defects shipped past seventeen bUnit tests **and** two
code reviews, because all of them were invisible to the kind of test being written:

- bUnit never executes `sortable-interop.js`. A dangling reference in that file passes every unit
  test and `node --check`, and only surfaces in a browser.
- The drag tests address `iframe.html` directly, because the BlazingStory shell does not forward
  the `ForceFallback` arg. That is the right call - but it means the shell is invisible to them.
  `StorybookShellTests` exists to cover it, and it deliberately does **not** fail on errors raised
  by BlazingStory itself, so an upstream bug cannot hold the suite hostage.
- A drag can leave the model perfectly correct while Blazor throws rendering the result. Every
  scenario therefore asserts the console (`AssertNoJsErrors`) as well as the data. Without that,
  eyeballing the storybook was passing changes that were actually broken.

When a bug is reported, reproduce it as a failing test first. A fix verified by looking at the page
is not verified.

## Library gotchas, learned the hard way

**Render-tree sequence numbers must be source-location constants.** Never feed a loop counter to
`builder.OpenElement`/`AddAttribute`. Blazor's diff algorithm treats them as positions in the
source, not as identities, and incrementing them per item corrupts the diff.

**Static fields are per closed generic type.** A counter declared inside `Sortable<TItem>` gives
`Sortable<Foo>` and `Sortable<Bar>` separate storage, so both start at 1 and collide in the shared
registry. This is why `SortableElementId` is a separate non-generic class.

**Absent data is not a delete signal.** SortableJS reports a destination index of `-1` on some
drops. The removal from the source is unconditional, so discarding such an insertion took the item
out of one list and put it nowhere - silent data loss. Every item must land somewhere; fall back to
appending. Pinned by `SortableItemLossTests`.

**Capture drag snapshots on `choose`, not `start`.** By `start` the fallback ghost is already in the
DOM and gets recorded as a real item, which resurrects it on restore.

**Guard the .NET reference after disposal.** Check `isDestroyed` at the moment the queued callback
runs, not only when it is enqueued - a queued event can outlive its component.

## Storybook gotchas

- Stories are keyed by **title**. Two `[Stories("...")]` with the same title silently collide, and
  one of them vanishes from the sidebar.
- `Story.Description` renders **only** on the Docs tab (`ShowDetails => ViewMode == ViewMode.Docs`).
  Anything a reader needs while looking at the canvas has to be in the canvas markup too.
- Args travel in the query string as `Key:Value;Key2:Value2`, with `:` and `;` escaped to `%3A` and
  `%3B`.
- `BlazingStoryApp.Title` drives both the HTML title and the brand in the sidebar.
- Kill whatever is on the dev port before restarting the demo. A stale server serves the previous
  build's `dotnet.<hash>.js`, and the app dies with "Failed to start platform" - which looks like a
  code fault and is not one.

## Driving SortableJS from Playwright

Synthesising a drag that SortableJS actually honours is harder than it looks. `SortableDragTests`
encodes four rules; breaking any one of them makes an item land one position short:

1. **Aim well inside the intended half of the target row, never at its midpoint.** The midpoint is
   the before/after decision boundary itself, so aiming there makes the outcome depend on sub-pixel
   rounding.
2. **Step from the pointer's current position, never interpolate from mouse-down.** The destination
   moves while the drag is in flight, so a fraction of a stale origin-to-target line can place the
   pointer *behind* where it already was. SortableJS reads that as a drag in the opposite direction
   and undoes the swap it just made.
3. **Keep moving on the target for longer than `AnimationDuration` before releasing.** SortableJS
   discards a `dragover` whose target is still animating and only reconsiders on the next pointer
   move, so arriving and stopping inside that window loses the final swap outright.
4. **A list container is not a row.** Dropping onto a list means appending to it; the row fractions
   aim a fifth of the way down the whole list and land before its rows.

Also: measure the row carrying `data-sortable-item`, not the inner content span - the span's box is
text-sized and the pointer never crosses a sibling's midpoint. And a tree row *encloses* its own
child list, so its box spans the whole subtree and its midpoint sits inside the nested list rather
than in the list the row belongs to.

Flakiness here is almost never the harness mechanics. Read the failure payload - it prints the last
model state - before touching readiness waits or retry loops. Three attempts were spent on harness
plumbing that changed nothing, while the payload said plainly that two different causes were at
work.
