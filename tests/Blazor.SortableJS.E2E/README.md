# Real-browser drag suite

This project drives the existing BlazingStory app in `demo/`; it does not use a private test host. The fixture starts and stops the demo automatically, launches the installed system Google Chrome through Playwright's `chrome` channel, and navigates directly to each story URL.

Run from the repository root:

```powershell
dotnet test tests/Blazor.SortableJS.E2E/Blazor.SortableJS.E2E.csproj -c Release
```

Every e2e URL sets the registered `ForceFallback` story argument. The suite therefore exercises SortableJS's pointer-event fallback drag path, which Playwright can drive with real mouse input. The normal story default remains `false`, so people opening the demo still get SortableJS's native HTML5 behavior and can switch modes in the Controls panel.

The shared drag helper always sends intermediate mouse moves and short pauses. Tests assert the live C# collections rendered by the stories, CLR reference identities, direct DOM order, and global no-loss/no-duplication invariants after every scenario.

No Playwright browser download is needed. Google Chrome must be installed and discoverable by Playwright's `chrome` channel.
