using System.Runtime.CompilerServices;
using Shouldly;
using Xunit;

namespace Kebechet.Blazor.SortableJS.Tests;

/// <summary>
/// Guards the packaging contract: the vendored SortableJS bundle and the Blazor JS initializer
/// must ship as static web assets, at the pinned version, referenced by their packaged path.
/// Get any of it wrong and the package publishes cleanly but loads no JS at runtime.
/// </summary>
public class PackagingTests
{
    /// <summary>
    /// Resolved from this file's compile-time path rather than the test assembly's location:
    /// an RCL's <c>wwwroot</c> is not copied into a referencing project's output, and these
    /// assertions are about repository content, not build output.
    /// </summary>
    private static string _wwwrootDirectory => Path.Combine(_repositoryRoot, "src", "Blazor.SortableJS", "wwwroot");

    private static string _repositoryRoot
    {
        get
        {
            var testsDirectory = Path.GetDirectoryName(GetThisFilePath())!;
            return Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));
        }
    }

    [Theory]
    [InlineData("Sortable.min.js")]
    [InlineData("Kebechet.Blazor.SortableJS.lib.module.js")]
    public void Static_web_asset_is_present(string fileName)
    {
        // Arrange
        var assetPath = Path.Combine(_wwwrootDirectory, fileName);

        // Act
        var doesAssetExist = File.Exists(assetPath);

        // Assert
        doesAssetExist.ShouldBeTrue($"'{fileName}' is missing from src/Blazor.SortableJS/wwwroot.");
    }

    [Fact]
    public void Vendored_bundle_is_the_pinned_SortableJS_version()
    {
        // Arrange
        var bundlePath = Path.Combine(_wwwrootDirectory, "Sortable.min.js");

        // Act
        var banner = File.ReadLines(bundlePath).First();

        // Assert
        banner.ShouldContain("Sortable 1.15.7");
    }

    [Fact]
    public void Initializer_loads_the_bundle_locally_and_never_from_a_cdn()
    {
        // Arrange
        var initializerPath = Path.Combine(_wwwrootDirectory, "Kebechet.Blazor.SortableJS.lib.module.js");

        // Act
        var initializer = File.ReadAllText(initializerPath);

        // Assert
        initializer.ShouldContain("_content/Kebechet.Blazor.SortableJS/Sortable.min.js");
        initializer.ShouldNotContain("cdn.");
        initializer.ShouldNotContain("http://");
        initializer.ShouldNotContain("https://");
    }

    private static string GetThisFilePath([CallerFilePath] string path = "")
    {
        return path;
    }
}
