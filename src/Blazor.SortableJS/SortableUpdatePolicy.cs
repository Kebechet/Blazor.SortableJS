namespace Kebechet.Blazor.SortableJS;

/// <summary>
/// Decides whether a render has to push its options across to JavaScript.
/// </summary>
internal static class SortableUpdatePolicy
{
    /// <summary>
    /// Returns true when the interop update must be sent.
    /// </summary>
    /// <param name="lastApplied">The description of the options last sent, or null if none.</param>
    /// <param name="current">The description of the options this render would send.</param>
    /// <param name="hasScrollContainerSelector">Whether a scroll container is named by selector.</param>
    /// <remarks>
    /// A selector is the one option whose meaning is not captured by how it serializes. It is
    /// resolved to a concrete element on the JavaScript side, so if something swaps the matching
    /// node for a new one, the description is unchanged, the update is skipped, and SortableJS goes
    /// on scrolling an element that has left the document. Skipping is safe for every other option,
    /// where equal descriptions really do mean equal behaviour.
    /// </remarks>
    internal static bool ShouldSendUpdate(string? lastApplied, string current, bool hasScrollContainerSelector)
    {
        if (hasScrollContainerSelector)
        {
            return true;
        }

        return !string.Equals(lastApplied, current, StringComparison.Ordinal);
    }
}
