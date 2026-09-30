using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// The React portal's public copy is written for the portal. The current site is a source of facts
/// (dates, names, album titles, positions), never of sentences: no run of six words in the portal's
/// copy may appear in what the Blazor site renders - its Razor pages and components, the seeded
/// labels behind the homepage, and its static HTML. Proper names are removed first, since repeating
/// "Real Tuna Universitária de Bragança" is identity, not copying.
///
/// The Privacy Policy is the deliberate exception (legal text, carried verbatim from the retired
/// Privacy.razor and now only in portal/src/Privacy.tsx), so that file is not checked here.
/// </summary>
public class PortalCopyOriginalityTests
{
    private const int ShingleWords = 6;

    private static readonly string[] ProperNames =
    {
        "real tuna universitária de bragança",
        "boémios e trovadores",
        "instituto politécnico de bragança",
        "festival internacional de tunas académicas",
        "quinta de santa apolónia",
    };

    private static readonly Regex Word = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly Regex InlineTag = new(@"</?(strong|em|br)\b[^>]*>", RegexOptions.Compiled);
    private static readonly Regex TextNode = new(@">([^<>{};=]+)<", RegexOptions.Compiled);
    private static readonly Regex StringLiteral = new(@"'([^'\n]+)'|""([^""\n]+)""", RegexOptions.Compiled);
    private static readonly Regex PlainWord = new(@"^\p{L}{2,}$", RegexOptions.Compiled);

    [Fact]
    public void PortalCopy_SharesNoSixWordRunWithTheCurrentSite()
    {
        var site = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in CurrentSiteFiles())
        {
            site.UnionWith(Shingles(File.ReadAllText(file)));
        }

        site.Should().HaveCountGreaterThan(10_000, "the current-site corpus must actually be read");

        var copied = PortalFiles()
            .SelectMany(file => CopyOf(File.ReadAllText(file))
                .SelectMany(Shingles)
                .Where(site.Contains)
                .Select(shingle => $"{Path.GetFileName(file)}: \"{shingle}\""))
            .Distinct()
            .ToList();

        copied.Should().BeEmpty(
            "portal copy must be original Portuguese, not lifted from the current site " +
            "(rewrite the sentence; facts and proper names are fine)");
    }

    [Fact]
    public void PortalSources_NeverSayMigration()
    {
        // Wording rule (docs/react-portal-pilot.md): "migration" is for EF/database migrations and
        // developer docs only - never in public UI, copy, routes or feature names, in any language.
        var files = PortalFiles().Append(Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "portal", "src", "Privacy.tsx"));

        files.Where(f => Regex.IsMatch(File.ReadAllText(f), "migra", RegexOptions.IgnoreCase))
            .Select(Path.GetFileName)
            .Should().BeEmpty();
    }

    /// <summary>Human-readable text only: JSX/HTML text nodes and multi-word string literals.</summary>
    private static IEnumerable<string> CopyOf(string source)
    {
        source = InlineTag.Replace(source, " ");

        foreach (Match m in TextNode.Matches(source))
        {
            yield return m.Groups[1].Value;
        }

        foreach (Match m in StringLiteral.Matches(source))
        {
            var literal = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            var plainWords = literal.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(w => PlainWord.IsMatch(w));
            if (plainWords >= 3 && literal.IndexOfAny(new[] { '/', '=', '{', '}' }) < 0)
            {
                yield return literal;
            }
        }
    }

    private static IEnumerable<string> Shingles(string text)
    {
        text = text.ToLowerInvariant();
        foreach (var name in ProperNames)
        {
            text = text.Replace(name, " ", StringComparison.Ordinal);
        }

        var words = Word.Matches(text).Select(m => m.Value).ToArray();
        for (var i = 0; i + ShingleWords <= words.Length; i++)
        {
            yield return string.Join(' ', words, i, ShingleWords);
        }
    }

    private static IEnumerable<string> CurrentSiteFiles()
    {
        var src = Path.Combine(GetProjectRoot(), "src");

        return new[]
            {
                Directory.EnumerateFiles(Path.Combine(src, "RTUB.Web"), "*.razor", SearchOption.AllDirectories),
                Directory.EnumerateFiles(Path.Combine(src, "RTUB.Shared"), "*.razor", SearchOption.AllDirectories),
                Directory.EnumerateFiles(Path.Combine(src, "RTUB.Application", "Data"), "SeedData*.cs"),
                Directory.EnumerateFiles(Path.Combine(src, "RTUB.Web", "wwwroot"), "*.html"),
            }
            .SelectMany(files => files)
            .Where(f => !Regex.IsMatch(f.Replace('\\', '/'), "/(bin|obj|publish|node_modules)/"));
    }

    private static IEnumerable<string> PortalFiles()
    {
        var portal = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "portal");

        return Directory.EnumerateFiles(Path.Combine(portal, "src"))
            .Where(f => f.EndsWith(".ts", StringComparison.Ordinal) || f.EndsWith(".tsx", StringComparison.Ordinal))
            .Where(f => !f.EndsWith("Privacy.tsx", StringComparison.Ordinal))
            .Append(Path.Combine(portal, "index.html"));
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
