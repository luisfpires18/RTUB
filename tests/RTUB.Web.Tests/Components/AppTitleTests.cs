using System.Text.RegularExpressions;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using RTUB.Components;
using Xunit;

namespace RTUB.Web.Tests.Components;

/// <summary>
/// Page-title contract (UI refactor 034, docs/design/RTUB_UI_REFACTOR.md section 21): every
/// routable page names itself through AppTitle, which renders "&lt;page&gt; - RTUB".
/// </summary>
public class AppTitleTests : BunitContext
{
    public AppTitleTests()
    {
        // HeadOutlet applies head content through JS interop; the rendered markup is what matters here.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void AppTitle_WithContent_AddsTheRtubSuffix()
    {
        var cut = Render(builder =>
        {
            builder.OpenComponent<HeadOutlet>(0);
            builder.CloseComponent();
            builder.OpenComponent<AppTitle>(1);
            builder.AddAttribute(2, nameof(AppTitle.ChildContent),
                (Microsoft.AspNetCore.Components.RenderFragment)(b => b.AddContent(0, "Ensaios")));
            builder.CloseComponent();
        });

        cut.Find("title").TextContent.Should().Be("Ensaios - RTUB");
    }

    [Fact]
    public void AppTitle_WithoutContent_IsTheFullName()
    {
        var cut = Render(builder =>
        {
            builder.OpenComponent<HeadOutlet>(0);
            builder.CloseComponent();
            builder.OpenComponent<AppTitle>(1);
            builder.CloseComponent();
        });

        cut.Find("title").TextContent.Should().Be("RTUB - Real Tuna Universitária de Bragança");
    }

    [Fact]
    public void EveryRoutablePage_SetsItsTitleThroughAppTitle()
    {
        var pagesRoot = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "Pages");
        var pages = Directory.EnumerateFiles(pagesRoot, "*.razor", SearchOption.AllDirectories)
            .Select(file => (file, content: File.ReadAllText(file)))
            .Where(p => Regex.IsMatch(p.content, @"^@page\s", RegexOptions.Multiline))
            .ToList();

        pages.Should().NotBeEmpty();
        pages.Where(p => !p.content.Contains("<AppTitle"))
            .Select(p => Path.GetFileName(p.file))
            .Should().BeEmpty("every routable page needs a meaningful document title");
        pages.Where(p => p.content.Contains("<PageTitle>"))
            .Select(p => Path.GetFileName(p.file))
            .Should().BeEmpty("pages use AppTitle so the RTUB suffix lives in one place");
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
