using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 005: no public section of the old site is forgotten by the React shell. The coverage table in
/// docs/react-portal-pilot.md must name every known public section and give each one a decision, and
/// every home anchor the shell links to must exist on the home page.
/// </summary>
public class PortalContentCoverageTests
{
    private static readonly string[] RequiredSections =
    {
        "Quem somos", "Atuações", "Pedidos", "Música", "Galeria", "Órgãos Sociais", "Junta-te a nós",
        "FITAB", "Hierarquia", "Política de Privacidade", "Área de membros", "Instalar a app", "Novidades",
    };

    private static readonly string[] Decisions =
    {
        "homepage now", "navbar now", "footer only", "temporary Blazor bridge", "future React page", "exclude/defer",
    };

    [Fact]
    public void CoverageTable_NamesEveryKnownPublicSection()
    {
        var sections = CoverageRows().Select(row => row[0]).ToList();

        RequiredSections.Where(required => !sections.Any(s => s.Contains(required, StringComparison.Ordinal)))
            .Should().BeEmpty("docs/react-portal-pilot.md must account for every public section");
    }

    [Fact]
    public void CoverageTable_GivesEveryRowADecision()
    {
        var rows = CoverageRows();

        rows.Should().HaveCountGreaterThanOrEqualTo(RequiredSections.Length);
        rows.Where(row => !Decisions.Any(d => row[^1].Contains(d, StringComparison.Ordinal)))
            .Select(row => row[0])
            .Should().BeEmpty("each row ends with one of: " + string.Join(", ", Decisions));
    }

    [Fact]
    public void HomeAnchors_LinkedByTheShell_ExistOnTheHomePage()
    {
        var src = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "portal", "src");
        var home = File.ReadAllText(Path.Combine(src, "Home.tsx"));

        // Literal "/#id" links, and the { id: '...' } section lists that App.tsx turns into "/#id".
        var anchors = Directory.GetFiles(src, "*.tsx")
            .SelectMany(f =>
            {
                var text = File.ReadAllText(f);
                return Regex.Matches(text, @"['""`]/#([a-z-]+)").Concat(Regex.Matches(text, @"\{ id: '([a-z-]+)'"));
            })
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        anchors.Should().Contain(new[] { "events", "fitab", "join", "app" });
        anchors.Where(id => !home.Contains($"id=\"{id}\"", StringComparison.Ordinal))
            .Should().BeEmpty("a footer, menu or shortcut link to /#id needs that id on the home page");
    }

    private static List<string[]> CoverageRows()
    {
        var doc = File.ReadAllText(Path.Combine(GetProjectRoot(), "docs", "react-portal-pilot.md"));
        var start = doc.IndexOf("## Public content coverage", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "the coverage section must exist");
        var end = doc.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        var section = end < 0 ? doc[start..] : doc[start..end];

        return section.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('|') && !line.StartsWith("| ---") && !line.StartsWith("| Section"))
            .Select(line => line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
            .ToList();
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
