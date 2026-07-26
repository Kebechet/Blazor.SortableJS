using System.Runtime.CompilerServices;
using Shouldly;
using Xunit;

namespace Kebechet.Blazor.SortableJS.Tests;

public class PublicSurfaceTests
{
    [Fact]
    public void Options_surface_has_all_typed_configuration_members()
    {
        // Arrange
        const int expectedOptionMemberCount = 48;

        // Act
        var optionMemberCount = typeof(SortableOptions).GetProperties().Length;

        // Assert
        optionMemberCount.ShouldBe(expectedOptionMemberCount);
    }

    [Fact]
    public void Component_exposes_all_fifteen_SortableJS_events()
    {
        // Arrange
        const int expectedEventCount = 15;

        // Act
        var eventCount = typeof(Sortable<string>)
            .GetProperties()
            .Count(property => property.Name.StartsWith("On", StringComparison.Ordinal) &&
                               property.PropertyType.IsGenericType &&
                               property.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.AspNetCore.Components.EventCallback<>));

        // Assert
        eventCount.ShouldBe(expectedEventCount);
    }

    [Fact]
    public void Interop_reverts_element_children_and_never_indexes_child_nodes()
    {
        // Arrange
        var interopPath = Path.Combine(RepositoryRoot, "src", "Blazor.SortableJS", "wwwroot", "sortable-interop.js");

        // Act
        var interop = File.ReadAllText(interopPath);

        // Assert
        interop.ShouldContain("element.children");
        interop.ShouldNotContain("childNodes");
        interop.ShouldContain("state.sortable.destroy()");
        interop.ShouldContain("waitForSortable");
    }

    private static string RepositoryRoot
    {
        get
        {
            var testsDirectory = Path.GetDirectoryName(GetThisFilePath())!;
            return Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));
        }
    }

    private static string GetThisFilePath([CallerFilePath] string path = "")
    {
        return path;
    }
}

