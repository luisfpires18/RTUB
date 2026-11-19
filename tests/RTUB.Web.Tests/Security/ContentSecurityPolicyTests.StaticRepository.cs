// CSP static repository policy checks on application scripts
using System.Text.RegularExpressions;
using FluentAssertions;

namespace RTUB.Web.Tests.Security;

public partial class ContentSecurityPolicyTests
{
    // ---------- static repository policy ----------

    /// <summary>
    /// The one script-src blocker unit 023 missed was the Blazor memberMap.js, which built its Leaflet popup markup as a
    /// string carrying an inline onerror attribute. The React map (task 032) replaced it: popups are DOM nodes, names
    /// go in as text, and the avatar fallback is a listener, so no member data or handler is ever written as markup.
    /// </summary>
    [Fact]
    public void MembersMap_BuildsPopupsAsDomNodesWithoutInlineHandlers()
    {
        File.Exists(Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "wwwroot", "js", "memberMap.js"))
            .Should().BeFalse("the Blazor map script was retired in 032");

        var content = ReadRepoFile("src", "RTUB.Web", "portal", "src", "pages/MembersMap.tsx");

        JsMarkupEventHandler.IsMatch(content).Should().BeFalse(
            "the popup avatar must not carry an inline error handler under a strict script-src");
        content.Should().Contain("textContent = member.displayName", "names go into the popup as text, not markup")
            .And.Contain("addEventListener('error'", "the avatar fallback is a listener")
            .And.NotContain("innerHTML");
    }

    /// <summary>
    /// Task 040: CARTO's basemap started answering "API KEY REQUIRED". The map uses OpenStreetMap's standard tiles (no
    /// key), the CSP allows exactly that tile host, and nothing in the portal, the CSP or the built bundle still asks
    /// CARTO for tiles.
    /// </summary>
    [Fact]
    public void MembersMap_UsesNoKeyOpenStreetMapTiles_AllowedByTheCsp_AndNoCartoTiles()
    {
        var map = ReadRepoFile("src", "RTUB.Web", "portal", "src", "pages/MembersMap.tsx");
        map.Should().Contain("const TILES = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';")
            .And.Contain("OpenStreetMap</a> contributors")
            .And.NotContainAny("cartocdn", "carto.com", "apikey", "api_key", "access_token", "?key=");

        Directives(Policy())["img-src"].Should().Contain("https://tile.openstreetmap.org")
            .And.NotContain(s => s.Contains("cartocdn"));

        var sources = Directory.GetFiles(Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "portal", "src"), "*", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "wwwroot", "portal", "assets"), "*.js"))
            .Append(Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "Security", "ContentSecurityPolicyBuilder.cs"));
        sources.Where(f => File.ReadAllText(f).Contains("basemaps.cartocdn.com"))
            .Should().BeEmpty("no CARTO tile dependency is left");
    }

    /// <summary>
    /// Repository-wide sweep, aggregated into one failure like the 023/024 policy tests: no
    /// application JavaScript may build markup carrying an inline on* handler. This is the check
    /// that would have caught memberMap.js, and it is what stops the next one regressing silently.
    ///
    /// Vendored bundles under wwwroot/lib and build outputs are excluded; a handler written as a
    /// DOM property (<c>el.onerror = fn</c>) is not a CSP violation and is not matched.
    /// </summary>
    [Fact]
    public void ApplicationJavaScript_BuildsNoMarkupCarryingInlineEventHandlers()
    {
        var violations = new List<string>();
        var root = RepoPaths.Root();

        foreach (var file in EnumerateApplicationScripts())
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var content = File.ReadAllText(file);

            foreach (Match match in JsMarkupEventHandler.Matches(content))
            {
                var line = content.AsSpan(0, match.Index).Count('\n') + 1;
                violations.Add($"  {relative}:{line}  {match.Value.Trim()}");
            }
        }

        violations.Should().BeEmpty(
            "JavaScript that builds HTML strings must not write on* handler attributes into them; " +
            "a strict script-src blocks those exactly as it blocks handlers written in markup." +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Unit 021 found a Google Fonts preconnect in the (since retired, 037) Blazor App.razor with no matching
    /// stylesheet and no @font-face anywhere. Removing it is what lets font-src stay 'self'; if the hint comes back
    /// without a font source, it is either dead weight or a directive the policy is missing.
    /// </summary>
    [Fact]
    public void NoWebFontServiceIsReferenced()
    {
        ReadRepoFile("src", "RTUB.Web", "portal", "index.html")
            .Should().NotContain("fonts.googleapis.com",
                "the preconnect had no matching stylesheet; font-src is 'self'");

        var cssRoot = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "wwwroot", "css");

        Directory.EnumerateFiles(cssRoot, "*.css", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("@font-face", StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty("a local @font-face would need its own font-src review");
    }
}
