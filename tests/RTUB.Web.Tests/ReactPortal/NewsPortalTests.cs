using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// React track 025 (Novidades, docs/react-news.md): the feed is a page reached from the navbar, not a block at the bottom
/// of the home page. The home keeps a small "Últimas novidades" preview right after the agenda, and post text is only
/// ever rendered as text.
/// </summary>
public class NewsPortalTests
{
    private static readonly string Src = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "portal", "src");

    private static string Read(string file) => File.ReadAllText(Path.Combine(Src, file));

    [Fact]
    public void Navbar_HasNovidades_LinkingToTheNewsPage()
    {
        var app = Read("App.tsx");

        // The section list feeds the top bar, the mobile menu and the footer.
        app.Should().Contain("{ id: 'news', label: 'Novidades' }");
        Regex.Match(app, @"const sectionPages[^}]*\}").Value.Should().Contain("news: portal.news", "Novidades opens /news, not a home anchor");
        Read("content.ts").Should().Contain("news: '/news'");
        Read("main.tsx").Should().Contain("'/news': lazy(() => import('./News'))");
    }

    [Fact]
    public void Home_ShowsTheLatestNewsRightAfterTheAgenda_AndDoesNotEndWithIt()
    {
        var home = Read("Home.tsx");
        var body = Regex.Match(home, @"export function Home\(\) \{.*?return \((.*?)\);\s*\}", RegexOptions.Singleline).Groups[1].Value;
        var sections = Regex.Matches(body, @"<(\w+) />").Select(m => m.Groups[1].Value).ToList();

        sections.Should().Contain("LatestNews");
        sections[sections.IndexOf("Events") + 1].Should().Be("LatestNews", "the preview sits right after the agenda");
        sections[^1].Should().NotBe("LatestNews", "the home must not end with a news block");
        home.Should().NotContain("NewsTeaser").And.NotContain("Em breve", "the old \"Novidades · Em breve\" strip is gone");
        Read("styles.css").Should().NotContain("news-teaser");

        // Hidden while there is nothing to show; three posts at most, and a way to the full feed.
        home.Should().Contain("if (posts.length === 0) return null;").And.Contain("getLatestNews(3)").And.Contain("Ver todas");
    }

    [Fact]
    public void PostText_IsNeverRenderedAsHtml()
    {
        foreach (var file in new[] { "News.tsx", "Home.tsx" })
        {
            Read(file).Should().NotContain("dangerouslySetInnerHTML", "{0} shows post text as text, never as markup", file);
        }
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
