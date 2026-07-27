using Shouldly;
using Xunit;

namespace Kebechet.Blazor.SortableJS.Tests;

/// <summary>
/// Skipping an unchanged option update saves an interop round trip per render, but only where an
/// unchanged description really does mean unchanged behaviour.
/// </summary>
public sealed class SortableUpdatePolicyTests
{
    [Fact]
    public void ShouldSendUpdate_UnchangedOptions_SkipsTheInteropCall()
    {
        // Arrange & Act
        var shouldSend = SortableUpdatePolicy.ShouldSendUpdate("same", "same", hasScrollContainerSelector: false);

        // Assert
        shouldSend.ShouldBeFalse();
    }

    [Fact]
    public void ShouldSendUpdate_ChangedOptions_SendsTheInteropCall()
    {
        // Arrange & Act
        var shouldSend = SortableUpdatePolicy.ShouldSendUpdate("before", "after", hasScrollContainerSelector: false);

        // Assert
        shouldSend.ShouldBeTrue();
    }

    [Fact]
    public void ShouldSendUpdate_FirstRenderWithNothingApplied_SendsTheInteropCall()
    {
        // Arrange & Act
        var shouldSend = SortableUpdatePolicy.ShouldSendUpdate(null, "first", hasScrollContainerSelector: false);

        // Assert
        shouldSend.ShouldBeTrue();
    }

    [Fact]
    public void ShouldSendUpdate_ScrollContainerNamedBySelector_SendsEvenWhenUnchanged()
    {
        // Arrange - the selector is resolved to a concrete element on the JavaScript side, so if the
        // matching node is replaced the description stays identical while the element SortableJS
        // holds has left the document. Its serialized form cannot express that, so it never skips.

        // Act
        var shouldSend = SortableUpdatePolicy.ShouldSendUpdate("same", "same", hasScrollContainerSelector: true);

        // Assert
        shouldSend.ShouldBeTrue();
    }
}
