using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Kebechet.Blazor.SortableJS;

namespace Blazor.SortableJS.Demo.Stories;

/// <summary>Arguments shared by the sortable stories.</summary>
public sealed class SortableStoryArgs : ComponentBase
{
    /// <summary>Gets or sets whether SortableJS uses its pointer-event fallback implementation.</summary>
    [Parameter]
    public bool ForceFallback { get; set; }
}

/// <summary>Shared helpers for story arguments and live collection-state rendering.</summary>
internal static class StorySupport
{
    public static SortableOptions WithFallback(SortableOptions options, IReadOnlyDictionary<string, object?> args)
    {
        options.IsFallbackForced = IsFallbackForced(args);
        return options;
    }

    public static SortableOptions WithFallback(SortableOptions options, bool isFallbackForced)
    {
        options.IsFallbackForced = isFallbackForced;
        return options;
    }

    public static bool IsFallbackForced(IReadOnlyDictionary<string, object?> args)
    {
        return args.TryGetValue(nameof(SortableStoryArgs.ForceFallback), out var value) &&
            (value is true || string.Equals(value?.ToString(), bool.TrueString, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Renders the fallback flag as an explicit "true"/"false" string.
    /// </summary>
    /// <remarks>
    /// Blazor omits an attribute whose value is <c>false</c> and emits a valueless attribute for
    /// <c>true</c>, so binding the bool directly makes "fallback off" indistinguishable from
    /// "story failed to render". An explicit string keeps both states visible to a reader and
    /// assertable by the E2E suite.
    /// </remarks>
    public static string FallbackAttributeValue(IReadOnlyDictionary<string, object?> args)
    {
        return IsFallbackForced(args) ? "true" : "false";
    }

    public static string SerializeState(params DemoCollection[] collections)
    {
        return JsonSerializer.Serialize(new
        {
            collections = collections.ToDictionary(
                collection => collection.Name,
                collection => collection.Items.Select(item => new
                {
                    key = item.Key,
                    label = item.Label,
                    identity = item.Identity,
                    initialIdentity = item.InitialIdentity
                }))
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}

/// <summary>Associates a human-readable collection name with its live items.</summary>
internal sealed record DemoCollection(string Name, IEnumerable<IDemoIdentityItem> Items);
