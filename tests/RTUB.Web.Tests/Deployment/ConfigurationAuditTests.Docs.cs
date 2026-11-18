// Configuration documentation truths and secret scan
using System.Text.RegularExpressions;
using FluentAssertions;

namespace RTUB.Web.Tests.Deployment;

public partial class ConfigurationAuditTests
{
    // ---------- documentation truths ----------

    [Fact]
    public void Docs_DoNotSayTheMessagingTablesShouldBeDropped()
    {
        var negation = new Regex(@"(?i)\b(never|not|no|cancelled|preserved|preserve)\b");
        var mention = new Regex(@"(?i)(conversation|messag)");
        var drop = new Regex(@"(?i)\bdrop");

        foreach (var (file, lines) in DocLines())
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (!drop.IsMatch(lines[i]) || !mention.IsMatch(lines[i])) continue;
                var window = string.Join(' ', lines.Skip(Math.Max(0, i - 1)).Take(i == 0 ? 2 : 3));
                negation.IsMatch(window).Should().BeTrue("{0}:{1} must not plan a messaging drop: {2}", file, i + 1, lines[i]);
            }
        }
    }

    [Fact]
    public void Docs_DoNotSayBlazorIsActive()
    {
        var active = new Regex(@"(?i)(Blazor Web App|Blazor Web Application|Blazor Interactive Server app|built with \**Blazor|Blazor Server host|src/RTUB\.Shared/Components|bUnit \(component tests)");
        foreach (var (file, lines) in DocLines())
        {
            for (var i = 0; i < lines.Length; i++)
            {
                active.IsMatch(lines[i]).Should().BeFalse("{0}:{1} describes Blazor as active: {2}", file, i + 1, lines[i]);
            }
        }

        var stack = File.ReadAllText(Path.Combine(Root, "README.md")) + File.ReadAllText(Path.Combine(Root, "docs", "frontend-practices.md"));
        stack.Should().Contain("React 19").And.Contain("no Blazor", "the stack docs say the frontend is React-only");
    }

    [Fact]
    public void Docs_DoNotPresentIDriveAsTheCurrentStorage()
    {
        var historical = new Regex(@"(?i)(historical|dead|stale|leftover|delete|removed?|absent|deprecated|nothing reads|no code)");
        foreach (var (file, lines) in DocLines())
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("IDrive", StringComparison.OrdinalIgnoreCase)) continue;
                var window = string.Join(' ', lines.Skip(Math.Max(0, i - 1)).Take(i == 0 ? 2 : 3));
                historical.IsMatch(window).Should().BeTrue("{0}:{1} mentions IDrive as if current: {2}", file, i + 1, lines[i]);
            }
        }

        File.ReadAllText(Path.Combine(Root, "README.md")).Should().Contain("Cloudflare R2");
    }

    [Fact]
    public void ConfigDocsAndScripts_CarryNoRealLookingSecret()
    {
        var files = new[]
        {
            Path.Combine(Root, "docs", "configuration-runbook.md"),
            Path.Combine(Root, "README.md"),
            Path.Combine(Root, "docs", "ci-cd-and-azure-environments.md"),
            ManifestPath,
        }.Concat(Directory.GetFiles(Path.Combine(Root, "scripts", "config"), "*.ps1"));

        // A long token with letters and several digits (keys, tokens), not a setting name or a hyphenated file name.
        var token = new Regex(@"(?<![A-Za-z0-9_\-])(?=(?:[A-Za-z_\-]*\d){4})(?=[A-Za-z0-9_\-]*[A-Za-z])[A-Za-z0-9_\-]{32,}(?![A-Za-z0-9_\-])");
        var assignment = new Regex(@"(?i)(password|secret|accesskey|privatekey)[A-Za-z_:]*[""']?\s*[=:]\s*[""']?(?<value>[^\s""'<`,)]{8,})");

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("-----BEGIN", file);
            token.Matches(text).Select(m => m.Value).Where(v => !v.Contains("__") && !Regex.IsMatch(v, "^[a-z0-9]+(-[a-z0-9]+)+$"))
                .Should().BeEmpty("{0} must hold placeholders only", file);
            if (file.EndsWith(".md") || file.EndsWith(".json"))
            {
                assignment.Matches(text).Select(m => m.Groups["value"].Value)
                    .Where(v => !v.StartsWith('<') && !v.StartsWith('$') && !v.StartsWith('{'))
                    .Should().BeEmpty("{0} must not assign a secret a literal value", file);
            }
        }
    }

    /// <summary>Every maintained Markdown doc: root docs, docs/ (ADRs excluded: append-only history), .github templates, RTUB skills.</summary>
    private static IEnumerable<(string File, string[] Lines)> DocLines()
    {
        var files = new List<string> { "README.md", "CLAUDE.md", ".ai/STATE.md" }.Select(f => Path.Combine(Root, f))
            .Concat(Directory.GetFiles(Path.Combine(Root, "docs"), "*.md", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.Combine("architecture", "adr"))))
            .Concat(Directory.GetFiles(Path.Combine(Root, ".github"), "*.md", SearchOption.AllDirectories))
            .Concat(Directory.GetDirectories(Path.Combine(Root, ".claude", "skills"), "rtub-*")
                .SelectMany(d => Directory.GetFiles(d, "*.md", SearchOption.AllDirectories)));
        foreach (var file in files.Where(File.Exists))
        {
            yield return (Path.GetRelativePath(Root, file), File.ReadAllLines(file));
        }
    }
}
