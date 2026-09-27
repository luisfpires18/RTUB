using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.Components;

/// <summary>
/// Guards the two global CSS contracts from the UI refactor (unit 033, see
/// docs/design/RTUB_UI_REFACTOR.md section 20). Both regressions are invisible to component
/// tests and made almost every screen look or lay out wrong:
/// <list type="bullet">
/// <item>The base <c>.btn</c> rule painted colors and sizing directly, which overrode every
/// Bootstrap variant (outline, warning, link) and <c>btn-sm</c>.</item>
/// <item><c>html</c>/<c>body</c> were clipped horizontally, which hid layout bugs by cutting
/// content off instead of fixing them.</item>
/// </list>
/// </summary>
public class VisualFoundationCssTests
{
    private static readonly string CssRoot = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot", "css");

    [Fact]
    public void BaseButtonRule_LeavesColorsAndSizingToVariants()
    {
        var css = StripComments(File.ReadAllText(Path.Combine(CssRoot, "3-components", "buttons.css")));

        // Top-level rules whose whole selector is `.btn` (not `.btn-primary`, `.btn:hover`, ...).
        var baseBlocks = Regex.Matches(css, @"(?:^|\})\s*\.btn\s*\{(?<body>[^}]*)\}")
            .Select(m => m.Groups["body"].Value);

        foreach (var body in baseBlocks)
        {
            var declarations = body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(d => !d.StartsWith("--", StringComparison.Ordinal)); // --bs-btn-* variables are fine

            declarations.Should().NotContain(
                d => Regex.IsMatch(d, @"^(background|background-color|border|border-color|color|padding|font-size)\s*:"),
                "`.btn` must not paint or size buttons directly; variants and btn-sm/btn-lg do that through --bs-btn-* variables");
        }
    }

    [Fact]
    public void RootElements_AreNotClippedHorizontally()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(CssRoot, "*.css", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}")))
        {
            var css = StripComments(File.ReadAllText(file));

            foreach (Match rule in Regex.Matches(css, @"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}"))
            {
                var selectors = rule.Groups["selector"].Value.Split(',').Select(s => s.Trim());
                var targetsRoot = selectors.Any(s => s is "html" or "body" or ".content-main");
                var clips = Regex.IsMatch(rule.Groups["body"].Value, @"overflow-x\s*:\s*(clip|hidden)");

                if (targetsRoot && clips)
                {
                    offenders.Add($"{Path.GetFileName(file)}: {rule.Groups["selector"].Value.Trim()}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "html, body and the main content area must not clip horizontal overflow globally; fix the overflowing component instead");
    }

    private static string StripComments(string css) => Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

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
