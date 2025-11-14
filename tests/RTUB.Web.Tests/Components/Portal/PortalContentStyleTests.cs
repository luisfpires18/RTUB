using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Components.Portal;

/// <summary>
/// Tests for portal content component styling and layout structure.
/// Verifies that visual refinements maintain proper CSS classes and text alignment.
/// These tests verify the CSS file structure and class naming conventions used across portal sections.
/// </summary>
public class PortalContentStyleTests
{

    /// <summary>
    /// Tests that verify CSS file organization for portal sections.
    /// Each portal section should have its own dedicated CSS file in the 4-pages directory.
    /// </summary>
    [Theory]
    [InlineData("about-us.css")]
    [InlineData("join-us.css")]
    [InlineData("history.css")]
    [InlineData("hierarchy.css")]
    [InlineData("fitab.css")]
    public void PortalSection_HasDedicatedCSSFile(string cssFileName)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var cssFilePath = Path.Combine(projectRoot, "src", "RTUB.Web", "wwwroot", "css", "4-pages", cssFileName);

        // Act & Assert
        File.Exists(cssFilePath).Should().BeTrue($"{cssFileName} should exist for isolated portal section styles in 4-pages directory");
    }

    /// <summary>
    /// Tests that verify shared portal styles exist (either in site.css or in component files).
    /// After CSS refactoring, portal styles are in 3-components/portal-headers.css and imported via site.css.
    /// </summary>
    [Fact]
    public void PortalSections_HaveSharedStylesInSiteCSS()
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var siteCssPath = Path.Combine(projectRoot, "src", "RTUB.Web", "wwwroot", "css", "site.css");
        var portalHeadersCssPath = Path.Combine(projectRoot, "src", "RTUB.Web", "wwwroot", "css", "3-components", "portal-headers.css");

        // Act
        var siteCssContent = File.ReadAllText(siteCssPath);
        var portalHeadersContent = File.ReadAllText(portalHeadersCssPath);

        // Assert - site.css should import portal headers
        siteCssContent.Should().Contain("portal-headers.css", "site.css should import portal headers component");
        
        // Assert - portal headers CSS should contain the actual styles
        portalHeadersContent.Should().Contain(".portal-section-header", "Shared portal header styles should exist in portal-headers.css");
        portalHeadersContent.Should().Contain(".portal-section-title", "Shared portal title styles should exist in portal-headers.css");
        portalHeadersContent.Should().Contain(".portal-section-divider", "Shared portal divider styles should exist in portal-headers.css");
    }

    /// <summary>
    /// Tests that verify text alignment is properly configured in CSS files.
    /// </summary>
    [Theory]
    [InlineData("about-us.css", ".about-us-content")]
    [InlineData("join-us.css", ".join-us-intro-text")]
    [InlineData("history.css", ".history-intro-text")]
    [InlineData("hierarchy.css", ".hierarchy-intro-text")]
    [InlineData("fitab.css", ".fitab-intro-text")]
    public void PortalSection_ContentHasLeftAlignmentInCSS(string cssFileName, string cssClass)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var cssFilePath = Path.Combine(projectRoot, "src", "RTUB.Web", "wwwroot", "css", "4-pages", cssFileName);

        // Act
        var content = File.ReadAllText(cssFilePath);

        // Assert
        content.Should().Contain(cssClass, $"{cssClass} should be defined in {cssFileName}");
        content.Should().Contain("text-align: left", $"{cssFileName} should have text-align: left for proper content alignment");
    }

    /// <summary>
    /// Tests that verify intro sections do not have text-center class for proper left alignment.
    /// </summary>
    [Theory]
    [InlineData("hierarchy.css", ".hierarchy-intro")]
    [InlineData("fitab.css", ".fitab-intro")]
    public void PortalSection_IntroDoesNotHaveTextCenterInCSS(string cssFileName, string introClass)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var cssFilePath = Path.Combine(projectRoot, "src", "RTUB.Web", "wwwroot", "css", "4-pages", cssFileName);

        // Act
        var content = File.ReadAllText(cssFilePath);

        // Assert
        var introClassIndex = content.IndexOf(introClass);
        introClassIndex.Should().BeGreaterThan(-1, $"{introClass} should exist in {cssFileName}");

        // Find the CSS block for this class
        var blockStart = introClassIndex;
        var blockEnd = content.IndexOf('}', blockStart);
        var cssBlock = content.Substring(blockStart, blockEnd - blockStart + 1);

        cssBlock.Should().NotContain("text-align: center", 
            $"{introClass} should not have text-align: center in {cssFileName}");
    }

    /// <summary>
    /// Tests responsive breakpoints exist for mobile layouts.
    /// </summary>
    [Theory]
    [InlineData("about-us.css")]
    [InlineData("join-us.css")]
    [InlineData("history.css")]
    [InlineData("hierarchy.css")]
    [InlineData("fitab.css")]
    public void PortalSection_HasResponsiveBreakpoints(string cssFileName)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var cssFilePath = Path.Combine(projectRoot, "src", "RTUB.Web", "wwwroot", "css", "4-pages", cssFileName);

        // Act
        var content = File.ReadAllText(cssFilePath);

        // Assert
        content.Should().Contain("@media", $"{cssFileName} should have responsive breakpoints");
    }

    /// <summary>
    /// Tests that dedicated CSS files do not contain generic site-wide styles.
    /// </summary>
    [Theory]
    [InlineData("about-us.css", "about-us")]
    [InlineData("join-us.css", "join-us")]
    [InlineData("history.css", "history")]
    [InlineData("hierarchy.css", "hierarchy")]
    [InlineData("fitab.css", "fitab")]
    public void PortalSection_CSSIsProperlyScoped(string cssFileName, string expectedPrefix)
    {
        // Arrange
        var projectRoot = GetProjectRoot();
        var cssFilePath = Path.Combine(projectRoot, "src", "RTUB.Web", "wwwroot", "css", "4-pages", cssFileName);

        // Act
        var content = File.ReadAllText(cssFilePath);

        // Assert
        // CSS should be scoped with the section-specific prefix
        var classMatches = System.Text.RegularExpressions.Regex.Matches(content, @"\.([\w-]+)");
        var scopedClasses = classMatches.Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value)
            .Where(c => c.StartsWith(expectedPrefix))
            .ToList();

        scopedClasses.Should().NotBeEmpty($"{cssFileName} should have scoped CSS classes starting with '{expectedPrefix}'");
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
