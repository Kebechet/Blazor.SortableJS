using Microsoft.Playwright;
using Xunit;

namespace Blazor.SortableJS.E2E;

/// <summary>
/// Loads every story through the storybook shell, which is the URL a human actually opens.
/// </summary>
/// <remarks>
/// The drag tests address the story canvas directly, because the shell does not forward the
/// ForceFallback arg. That keeps them reliable but leaves the shell - sidebar, preview frame,
/// docs tabs - completely uncovered, and a component fault that only appears there would go
/// unnoticed. Errors raised by BlazingStory itself are reported but not failed on: they are not
/// ours to fix, and failing on them would make this suite hostage to an upstream bug.
/// </remarks>
[Collection(DemoCollectionDefinition.Name)]
public sealed class StorybookShellTests(DemoFixture fixture, ITestOutputHelper output)
{
    public static TheoryData<string> StoryPaths =>
    [
        "/story/sortablejs-basic--reorder-in-place",
        "/story/sortablejs-groups--connected-groups-with-pull-and-put-policies",
        "/story/sortablejs-nesting--arbitrarily-nested-lists",
        "/story/sortablejs-multidrag--multidrag",
        "/story/sortablejs-clone--clone-mode",
        "/story/sortablejs-swap--swap-plugin",
        "/story/sortablejs-auto-scroll--auto-scroll",
        "/story/sortablejs-onspill--onspill-policies",
        "/docs/sortablejs-basic--docs",
        "/docs/sortablejs-nesting--docs",
        "/docs/sortablejs-onspill--docs"
    ];

    [Theory]
    [MemberData(nameof(StoryPaths))]
    public async Task ShellPageLoadsWithoutErrorsFromThisLibrary(string path)
    {
        var errors = new List<string>();
        void OnConsole(object? _, IConsoleMessage message)
        {
            if (message.Type == "error") errors.Add(message.Text);
        }

        void OnPageError(object? _, string error) => errors.Add(error);

        fixture.Page.Console += OnConsole;
        fixture.Page.PageError += OnPageError;
        try
        {
            await fixture.Page.GotoAsync($"{fixture.BaseUrl}/?path={path}", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await fixture.Page.WaitForTimeoutAsync(3000);
        }
        finally
        {
            fixture.Page.Console -= OnConsole;
            fixture.Page.PageError -= OnPageError;
        }

        var ours = errors.Where(IsFromThisLibrary).Distinct().ToArray();
        foreach (var upstream in errors.Where(error => !IsFromThisLibrary(error)).Distinct())
        {
            output.WriteLine("upstream (not failed on): " + upstream.Split('\n')[0]);
        }

        Assert.True(
            ours.Length == 0,
            "The shell reported errors from this library:" + Environment.NewLine + string.Join(Environment.NewLine, ours));
    }

    private static bool IsFromThisLibrary(string error)
    {
        return error.Contains("Kebechet.Blazor.SortableJS", StringComparison.OrdinalIgnoreCase)
            || error.Contains("sortable-interop", StringComparison.OrdinalIgnoreCase);
    }
}
