using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Components;
using Xunit;

namespace RTUB.Web.Tests.Components;

/// <summary>
/// Tests for the VersionedAsset component that generates versioned URLs for static assets.
/// Verifies that CSS and JS files are properly versioned to prevent cache issues after deployments.
/// </summary>
public class VersionedAssetTests : BunitContext
{
    private readonly Mock<IFileVersionProvider> _mockFileVersionProvider;

    public VersionedAssetTests()
    {
        _mockFileVersionProvider = new Mock<IFileVersionProvider>();

        // Register mock in DI container
        Services.AddSingleton(_mockFileVersionProvider.Object);
    }

    [Fact]
    public void VersionedAsset_Css_GeneratesLinkTagWithVersionedUrl()
    {
        // Arrange
        var path = "/css/site.css";
        var versionedPath = "/css/site.css?v=abc123";
        _mockFileVersionProvider
            .Setup(p => p.AddFileVersionToPath(It.IsAny<PathString>(), It.Is<string>(s => s == path)))
            .Returns(versionedPath);

        // Act
        var cut = Render<VersionedAsset>(parameters => parameters
            .Add(p => p.Path, path)
            .Add(p => p.Type, VersionedAsset.AssetType.Css));

        // Assert
        cut.Markup.Should().Contain($"href=\"{versionedPath}\"");
        cut.Markup.Should().Contain("rel=\"stylesheet\"");
        cut.Markup.Should().StartWith("<link");
    }

    [Fact]
    public void VersionedAsset_Js_GeneratesScriptTagWithVersionedUrl()
    {
        // Arrange
        var path = "/js/app.js";
        var versionedPath = "/js/app.js?v=xyz789";
        _mockFileVersionProvider
            .Setup(p => p.AddFileVersionToPath(It.IsAny<PathString>(), It.Is<string>(s => s == path)))
            .Returns(versionedPath);

        // Act
        var cut = Render<VersionedAsset>(parameters => parameters
            .Add(p => p.Path, path)
            .Add(p => p.Type, VersionedAsset.AssetType.Js));

        // Assert
        cut.Markup.Should().Contain($"src=\"{versionedPath}\"");
        cut.Markup.Should().StartWith("<script");
        cut.Markup.Should().EndWith("</script>");
    }

    [Fact]
    public void VersionedAsset_Css_WithCustomRel_UsesCustomRelAttribute()
    {
        // Arrange
        var path = "/css/print.css";
        var versionedPath = "/css/print.css?v=def456";
        _mockFileVersionProvider
            .Setup(p => p.AddFileVersionToPath(It.IsAny<PathString>(), It.Is<string>(s => s == path)))
            .Returns(versionedPath);

        // Act
        var cut = Render<VersionedAsset>(parameters => parameters
            .Add(p => p.Path, path)
            .Add(p => p.Type, VersionedAsset.AssetType.Css)
            .Add(p => p.Rel, "preload"));

        // Assert
        cut.Markup.Should().Contain("rel=\"preload\"");
    }

    [Fact]
    public void VersionedAsset_WithIntegrity_IncludesIntegrityAttribute()
    {
        // Arrange
        var path = "/css/site.css";
        var versionedPath = "/css/site.css?v=abc123";
        var integrity = "sha384-abc123";
        _mockFileVersionProvider
            .Setup(p => p.AddFileVersionToPath(It.IsAny<PathString>(), It.Is<string>(s => s == path)))
            .Returns(versionedPath);

        // Act
        var cut = Render<VersionedAsset>(parameters => parameters
            .Add(p => p.Path, path)
            .Add(p => p.Type, VersionedAsset.AssetType.Css)
            .Add(p => p.Integrity, integrity));

        // Assert
        cut.Markup.Should().Contain($"integrity=\"{integrity}\"");
    }

    [Fact]
    public void VersionedAsset_WithCrossOrigin_IncludesCrossOriginAttribute()
    {
        // Arrange
        var path = "/css/site.css";
        var versionedPath = "/css/site.css?v=abc123";
        _mockFileVersionProvider
            .Setup(p => p.AddFileVersionToPath(It.IsAny<PathString>(), It.Is<string>(s => s == path)))
            .Returns(versionedPath);

        // Act
        var cut = Render<VersionedAsset>(parameters => parameters
            .Add(p => p.Path, path)
            .Add(p => p.Type, VersionedAsset.AssetType.Css)
            .Add(p => p.CrossOrigin, "anonymous"));

        // Assert
        cut.Markup.Should().Contain("crossorigin=\"anonymous\"");
    }

    [Fact]
    public void VersionedAsset_CallsFileVersionProvider_WithCorrectParameters()
    {
        // Arrange
        var path = "/css/site.css";
        _mockFileVersionProvider
            .Setup(p => p.AddFileVersionToPath(It.Is<PathString>(ps => ps == PathString.Empty), It.Is<string>(s => s == path)))
            .Returns("/css/site.css?v=test")
            .Verifiable();

        // Act
        Render<VersionedAsset>(parameters => parameters
            .Add(p => p.Path, path)
            .Add(p => p.Type, VersionedAsset.AssetType.Css));

        // Assert
        _mockFileVersionProvider.Verify(
            p => p.AddFileVersionToPath(It.Is<PathString>(ps => ps == PathString.Empty), It.Is<string>(s => s == path)),
            Times.Once);
    }

    /// <summary>
    /// Tests that verify critical static assets are properly configured with versioning in MainLayout.
    /// </summary>
    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/RTUB.styles.css")]
    [InlineData("/lib/bootstrap/bootstrap.min.css")]
    [InlineData("/lib/bootstrap-icons/bootstrap-icons.min.css")]
    public void MainLayout_CriticalCssFiles_AreVersioned(string cssPath)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var mainLayoutPath = Path.Combine(projectRoot, "src", "RTUB.Web", "Shared", "MainLayout.razor");

        // Act
        var content = File.ReadAllText(mainLayoutPath);

        // Assert
        content.Should().Contain($"<VersionedAsset Path=\"{cssPath}\"",
            $"{cssPath} should use VersionedAsset component in MainLayout for cache busting");
    }

    /// <summary>
    /// Tests that verify critical JavaScript files are properly configured with versioning in MainLayout.
    /// </summary>
    [Theory]
    [InlineData("/js/fileDownload.js")]
    [InlineData("/js/imageCropper.js")]
    [InlineData("/js/profilePictureRefresh.js")]
    [InlineData("/js/familyTree.js")]
    [InlineData("/js/roleBadge.js")]
    [InlineData("/js/scrollToTop.js")]
    [InlineData("/js/kanban.js")]
    [InlineData("/lib/bootstrap/bootstrap.bundle.min.js")]
    public void MainLayout_CriticalJsFiles_AreVersioned(string jsPath)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var mainLayoutPath = Path.Combine(projectRoot, "src", "RTUB.Web", "Shared", "MainLayout.razor");

        // Act
        var content = File.ReadAllText(mainLayoutPath);

        // Assert
        content.Should().Contain($"<VersionedAsset Path=\"{jsPath}\"",
            $"{jsPath} should use VersionedAsset component in MainLayout for cache busting");
    }

    /// <summary>
    /// Tests that MainLayout does not contain unversioned local asset references.
    /// External CDN resources are excluded as they are already versioned.
    /// </summary>
    [Theory]
    [InlineData("<link href=\"/css/", "Local CSS files should use VersionedAsset instead of direct link tags")]
    [InlineData("<link href=\"/lib/", "Local library CSS files should use VersionedAsset instead of direct link tags")]
    [InlineData("<script src=\"/js/", "Local JS files should use VersionedAsset instead of direct script tags")]
    [InlineData("<script src=\"/lib/", "Local library JS files should use VersionedAsset instead of direct script tags")]
    public void MainLayout_DoesNotContainUnversionedLocalAssets(string pattern, string reason)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var mainLayoutPath = Path.Combine(projectRoot, "src", "RTUB.Web", "Shared", "MainLayout.razor");

        // Act
        var content = File.ReadAllText(mainLayoutPath);

        // Filter out framework scripts and external CDN resources which are expected
        var lines = content.Split('\n')
            .Where(l => !l.Contains("_framework/") && !l.Contains("cdnjs.cloudflare.com"))
            .ToList();
        var filteredContent = string.Join('\n', lines);

        // Assert
        filteredContent.Should().NotContain(pattern, reason);
    }

    /// <summary>
    /// Helper method to find the project root directory.
    /// </summary>
    private static string GetProjectRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        // Navigate up from test output directory to find src folder
        var directory = new DirectoryInfo(currentDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("Could not find project root directory");
        }

        return directory.FullName;
    }
}
