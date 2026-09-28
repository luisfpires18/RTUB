using System.Text.RegularExpressions;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Moq;
using RTUB.Components;
using Xunit;

namespace RTUB.Web.Tests.Components;

/// <summary>
/// The global stylesheet cache contract (UI refactor 034, docs/design/RTUB_UI_REFACTOR.md
/// section 21). /css/site.css lists the global sheets; GlobalStylesheets links each one with a
/// content-hash version, so a deploy that changes a sheet changes its URL. Before, the sheets
/// were @imported without a version and could stay cached for 30 days after a deploy.
/// </summary>
public class GlobalStylesheetsTests : BunitContext
{
    private static readonly string WebRoot = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot");
    private static readonly string SiteCss = File.ReadAllText(Path.Combine(WebRoot, "css", "site.css"));

    [Fact]
    public void Parse_ReturnsImportsInOrder_AndSkipsCommentedOutOnes()
    {
        const string css = """
            /* header */
            @import url('1-base/variables.css');
            /* @import url('old/disabled.css'); */
            @import url("2-layout/navbar.css");
            """;

        GlobalStylesheets.Parse(css).Should().Equal("/css/1-base/variables.css", "/css/2-layout/navbar.css");
    }

    [Fact]
    public void SiteCss_HoldsOnlyImportsAndComments()
    {
        // Anything else in site.css would be silently dropped, because site.css itself is not linked.
        var withoutComments = Regex.Replace(SiteCss, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var withoutImports = Regex.Replace(withoutComments, @"@import\s+url\([^)]*\)\s*;", string.Empty);

        withoutImports.Trim().Should().BeEmpty("site.css is a list of @imports; put rules in the listed sheets");
    }

    [Fact]
    public void SiteCss_ListsExistingSheets_EachOnce()
    {
        var paths = GlobalStylesheets.Parse(SiteCss);

        paths.Should().NotBeEmpty();
        paths.Should().OnlyHaveUniqueItems();
        paths.Should().AllSatisfy(path =>
            File.Exists(Path.Combine(WebRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)))
                .Should().BeTrue($"{path} is listed in site.css"));
    }

    [Fact]
    public void Render_LinksEverySheetVersioned_InSiteCssOrder()
    {
        var versions = new Mock<IFileVersionProvider>();
        versions.Setup(v => v.AddFileVersionToPath(It.IsAny<PathString>(), It.IsAny<string>()))
            .Returns((PathString _, string path) => path + "?v=hash");
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.WebRootFileProvider).Returns(new PhysicalFileProvider(WebRoot));
        Services.AddSingleton(versions.Object);
        Services.AddSingleton(environment.Object);
        Services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));

        var cut = Render<GlobalStylesheets>();

        var hrefs = cut.FindAll("link[rel=stylesheet]").Select(l => l.GetAttribute("href")).ToList();
        hrefs.Should().Equal(GlobalStylesheets.Parse(SiteCss).Select(p => p + "?v=hash"));
        hrefs.Should().NotContain(h => h!.Contains("site.css"));
    }

    [Fact]
    public void MainLayout_LinksGlobalSheetsThroughGlobalStylesheets()
    {
        var layout = File.ReadAllText(Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "Shared", "MainLayout.razor"));

        layout.Should().Contain("<GlobalStylesheets />");
        layout.Should().NotContain("/css/site.css\"", "site.css @imports are unversioned; link the sheets instead");
    }

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find project root directory");
    }
}
