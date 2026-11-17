using System.Text.RegularExpressions;
using FluentAssertions;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Every home anchor the React shell links to must exist on the home page.
/// </summary>
public class PortalContentCoverageTests
{
    [Fact]
    public void HomeAnchors_LinkedByTheShell_ExistOnTheHomePage()
    {
        var src = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "portal", "src");
        // Task 113 split pages/Home.tsx into components/Home*.tsx, and moved the section lists out of App.tsx into data/sections.ts.
        var home = string.Join("\n", new[] { "pages/Home.tsx", "components/HomeEvents.tsx", "components/HomeHero.tsx", "components/HomeJoin.tsx",
            "components/HomeMedia.tsx", "components/HomeParts.tsx" }.Select(f => File.ReadAllText(Path.Combine(src, f))));

        // Literal "/#id" links, and the { id: '...' } section lists that App.tsx turns into "/#id" -
        // except the sections App.tsx's sectionPages sends to their own page (e.g. governance -> /roles).
        var app = File.ReadAllText(Path.Combine(src, "data/sections.ts"));
        var ownPages = Regex.Matches(Regex.Match(app, @"const sectionPages[^}]*\}").Value, @"(\w+): portal\.")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
        var anchors = Directory.GetFiles(src, "*.tsx", SearchOption.AllDirectories).Append(Path.Combine(src, "data/sections.ts"))
            .SelectMany(f =>
            {
                var text = File.ReadAllText(f);
                return Regex.Matches(text, @"['""`]/#([a-z-]+)").Select(m => m.Groups[1].Value)
                    .Concat(Regex.Matches(text, @"\{ id: '([a-z-]+)'").Select(m => m.Groups[1].Value).Where(id => !ownPages.Contains(id)));
            })
            .Distinct()
            .ToList();

        ownPages.Should().Contain("governance", "the home no longer has an Órgãos Sociais section; the link opens /roles");

        // Task 044: the footer no longer links FITAB, joining or the app, so only the anchors still linked from the menu/pages are required.
        anchors.Should().Contain(new[] { "events" });
        anchors.Where(id => !home.Contains($"id=\"{id}\"", StringComparison.Ordinal))
            .Should().BeEmpty("a footer, menu or shortcut link to /#id needs that id on the home page");
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
