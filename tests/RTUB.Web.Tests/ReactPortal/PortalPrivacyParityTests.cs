using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// The React portal pilot renders the Privacy Policy at /portal/privacy from a copy of
/// Pages/Public/Privacy.razor, which stays the legal source and keeps serving /privacy. Two copies
/// of legal text drift silently, so every heading, paragraph and list item of the Razor page must
/// appear, word for word, in portal/src/Privacy.tsx.
/// </summary>
public class PortalPrivacyParityTests
{
    private static readonly Regex TextBlock =
        new(@"<(h1|h2|p|li)\b[^>]*>(.*?)</\1>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Tag = new(@"<[^>]+>", RegexOptions.Compiled);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    [Fact]
    public void PortalPrivacy_CarriesEveryTextBlockOfThePrivacyPage()
    {
        var razor = ReadRepoFile("src", "RTUB.Web", "Pages", "Public", "Privacy.razor");
        var portal = Normalize(ReadRepoFile("src", "RTUB.Web", "portal", "src", "Privacy.tsx"));

        var blocks = TextBlock.Matches(razor)
            .Select(m => Normalize(m.Groups[2].Value))
            .Where(text => text.Length > 0)
            .ToList();

        blocks.Should().HaveCountGreaterThan(40, "the regex must actually find the policy's text");
        blocks.Where(text => !portal.Contains(text, StringComparison.Ordinal)).Should().BeEmpty(
            "portal/src/Privacy.tsx must match Pages/Public/Privacy.razor word for word - update both together");
    }

    private static string Normalize(string markup) =>
        Whitespace.Replace(Tag.Replace(markup, " "), " ").Trim();

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetProjectRoot() }.Concat(segments).ToArray()));

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
