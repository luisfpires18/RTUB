using System.Text.RegularExpressions;

namespace RTUB.Tests;

/// <summary>
/// The React portal's whole stylesheet as the browser gets it. <c>portal/src/styles.css</c> only lists
/// <c>@import './styles/NNN-area.css';</c> lines (one file per area, in cascade order); this follows them in that
/// order and returns the joined text, so a test that looks for a rule still sees every rule.
///
/// Source-linked into every test project by <c>tests/Directory.Build.props</c>.
/// </summary>
public static class PortalCss
{
    /// <param name="portalSrc">The portal's <c>src</c> folder (<c>src/RTUB.Web/portal/src</c>).</param>
    public static string Read(string portalSrc)
    {
        var entry = File.ReadAllText(Path.Combine(portalSrc, "styles.css"));
        var imports = Regex.Matches(entry, @"^@import '\./(?<path>[^']+)';", RegexOptions.Multiline);
        if (imports.Count == 0) throw new InvalidOperationException("styles.css lists no @import: the test would check nothing.");
        // Each file ends at its last rule; the blank line between areas is put back here.
        return string.Join("\n", imports.Select(m => File.ReadAllText(Path.Combine(portalSrc, m.Groups["path"].Value))));
    }
}
