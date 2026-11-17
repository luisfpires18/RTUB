using System.Text.RegularExpressions;
using FluentAssertions;
using RTUB.Security;
using Xunit;

namespace RTUB.Web.Tests.Security;

/// <summary>
/// Pins the enforced Content-Security-Policy shipped by unit 025, and the last repository-wide
/// blocker it had to clear: inline event handlers written into markup <b>by JavaScript</b>, which
/// unit 023's markup-only sweep could not see.
///
/// The policy assertions go through the public builder rather than a hard-coded expected string,
/// so a deliberate directive change does not have to be restated in ten places - only a change
/// that actually weakens the policy fails.
/// </summary>
public partial class ContentSecurityPolicyTests
{
    private const string PublicUrl = "https://pub-test.r2.dev";
    private const string AccountId = "abc123";
    private const string EndpointOrigin = "https://abc123.r2.cloudflarestorage.com";

    /// <summary>Stand-in for the production public bucket a DEV snapshot inherits URLs from.</summary>
    private const string ProdPublicUrl = "https://pub-prod.r2.dev";

    private static string ReferencePolicy(string? referencePublicUrl) =>
        new ContentSecurityPolicyBuilder(PublicUrl, AccountId, referencePublicUrl).Build();

    private static string Policy(string? publicUrl = PublicUrl, string? accountId = AccountId) =>
        new ContentSecurityPolicyBuilder(publicUrl, accountId).Build();

    /// <summary>Splits a policy into directive name -> source list.</summary>
    private static Dictionary<string, string[]> Directives(string policy) =>
        policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(parts => parts[0], parts => parts.Skip(1).ToArray(), StringComparer.Ordinal);

    // ---------- the policy must stay restrictive ----------

    /// <summary>
    /// The whole point of units 022-024. If either of these ever reappears, the three units that
    /// removed every eval dispatch, inline script and inline style stop being worth anything.
    /// </summary>
    [Fact]
    public void Policy_NeverAllowsUnsafeInlineOrUnsafeEval()
    {
        var policy = Policy();

        policy.Should().NotContain("'unsafe-inline'");
        policy.Should().NotContain("'unsafe-eval'");
        policy.Should().NotContain("'unsafe-hashes'");
    }

    /// <summary>
    /// Bans the broad escapes that would quietly undo the policy: a bare <c>*</c>, a whole-scheme
    /// source such as <c>https:</c>, a wildcard at the scheme/host root, and <c>data:</c> anywhere
    /// it could carry executable content.
    /// </summary>
    [Fact]
    public void Policy_ContainsNoWildcardOrWholeSchemeSources()
    {
        var directives = Directives(Policy());

        foreach (var (name, sources) in directives)
        {
            foreach (var source in sources)
            {
                source.Should().NotBe("*", "{0} must not allow every origin", name);
                source.Should().NotBe("https:", "{0} must not allow every https origin", name);
                source.Should().NotBe("http:", "{0} must not allow every http origin", name);
                source.Should().NotBe("ws:", "{0} must not allow every ws origin", name);
                source.Should().NotBe("wss:", "{0} must not allow every wss origin", name);
                source.Should().NotStartWith("*.", "{0} must not allow a bare TLD wildcard", name);
            }
        }

        // data: is legitimate for the cropper/upload previews and nowhere else. In script-src it
        // is executable, and in frame-src it is a well-known sandbox escape.
        directives["script-src"].Should().NotContain("data:");
        directives["style-src"].Should().NotContain("data:");
        directives["frame-src"].Should().NotContain("data:");
        directives["object-src"].Should().NotContain("data:");
    }

    [Fact]
    public void Policy_PinsTheDirectivesThatLockOutInjectedContent()
    {
        var directives = Directives(Policy());

        directives["default-src"].Should().Equal("'self'");
        directives["base-uri"].Should().Equal("'self'");
        directives["form-action"].Should().Equal("'self'");
        directives["object-src"].Should().Equal("'none'");
        directives["frame-ancestors"].Should().Equal("'none'");
        directives["script-src-attr"].Should().Equal("'none'");
        directives["style-src-attr"].Should().Equal("'none'");
        directives["manifest-src"].Should().Equal("'self'");
        directives["worker-src"].Should().Equal("'self'");
        directives["font-src"].Should().Equal("'self'");
    }

    /// <summary>
    /// Every external origin is justified by something the browser actually loads, and must not
    /// leak into a directive that does not need it. The OpenStreetMap tiles are images only.
    /// No script or stylesheet comes from a CDN: cdnjs (Cropper.js) and unpkg (Leaflet) served the
    /// Blazor layout and were dropped in 042; jsdelivr served only pixi.min.js and went with MyTuno (026).
    /// </summary>
    [Fact]
    public void ExternalOrigins_AppearOnlyInTheDirectivesThatNeedThem()
    {
        var directives = Directives(Policy());

        directives["script-src"].Should().Equal("'self'");
        directives["style-src"].Should().Equal("'self'");

        directives["img-src"].Should().Contain("https://tile.openstreetmap.org")
            .And.Contain("data:");

        directives.Values.SelectMany(v => v).Should().NotContain(
            ["https://cdn.jsdelivr.net", "https://cdnjs.cloudflare.com", "https://unpkg.com"]);
    }

    // ---------- R2 is runtime configuration, not a literal ----------

    /// <summary>
    /// The public bucket holds images and video; the S3 API endpoint is where the pre-signed URLs
    /// used by the audio player and the two PDF iframes point. Neither origin is a source constant.
    /// </summary>
    [Fact]
    public void ConfiguredR2Origins_LandOnlyInTheDirectivesThatUseThem()
    {
        var directives = Directives(Policy());

        directives["img-src"].Should().Contain(PublicUrl);
        directives["media-src"].Should().Contain(PublicUrl).And.Contain(EndpointOrigin);
        directives["frame-src"].Should().Equal(EndpointOrigin);

        // No pre-signed image URLs exist, and nothing fetches R2 from the page - the service
        // worker does, and it deliberately never receives this header.
        directives["img-src"].Should().NotContain(EndpointOrigin);
        directives["connect-src"].Should().NotContain(PublicUrl);
        directives["script-src"].Should().NotContain(PublicUrl);
    }

    [Fact]
    public void ConfiguredR2PublicUrl_ContributesOnlyItsNormalizedOrigin()
    {
        var directives = Directives(Policy(publicUrl: "https://pub-test.r2.dev/bucket/path?x=1#f"));

        directives["img-src"].Should().Contain("https://pub-test.r2.dev");
        directives["img-src"].Should().NotContain(s => s.Contains("bucket") || s.Contains('?'));
    }

    [Fact]
    public void ConfiguredR2PublicUrl_KeepsANonDefaultPort()
    {
        Directives(Policy(publicUrl: "http://localhost:9000/bucket"))["img-src"]
            .Should().Contain("http://localhost:9000");
    }

    // ---------- helpers ----------

    /// <summary>
    /// An on* attribute inside a JavaScript string: the name is followed by <c>=</c> and a quote
    /// or backtick. The negative lookbehind keeps DOM property writes (<c>img.onerror = ...</c>),
    /// object keys and identifiers out of the match.
    /// </summary>
    private static readonly Regex JsMarkupEventHandler =
        new(@"(?<![.\w$@-])on[a-z]{2,}\s*=\s*[""'`]", RegexOptions.Compiled);

    /// <summary>
    /// Application-owned scripts: hand-written JS under wwwroot and the TypeScript it is built
    /// from. Vendored bundles, source maps and build intermediates are not ours to police.
    /// </summary>
    private static IEnumerable<string> EnumerateApplicationScripts()
    {
        var src = Path.Combine(GetProjectRoot(), "src");

        return new[] { "*.js", "*.ts" }
            .SelectMany(ext => Directory.EnumerateFiles(src, ext, SearchOption.AllDirectories))
            .Where(f =>
            {
                var path = f.Replace('\\', '/');

                return !path.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/wwwroot/lib/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    private static string ReadRepoFile(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetProjectRoot() }.Concat(segments).ToArray()));

    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("Could not find project root directory");
        }

        return directory.FullName;
    }
}
