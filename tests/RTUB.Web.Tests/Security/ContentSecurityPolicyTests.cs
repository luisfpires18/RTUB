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
public class ContentSecurityPolicyTests
{
    private const string PublicUrl = "https://pub-test.r2.dev";
    private const string AccountId = "abc123";
    private const string EndpointOrigin = "https://abc123.r2.cloudflarestorage.com";

    /// <summary>Stand-in for the production public bucket a DEV snapshot inherits URLs from.</summary>
    private const string ProdPublicUrl = "https://pub-prod.r2.dev";

    private static string ReferencePolicy(string? referencePublicUrl) =>
        new ContentSecurityPolicyBuilder(PublicUrl, AccountId, referencePublicUrl)
            .Build("https", "rtub.example");

    private static string Policy(
        string? publicUrl = PublicUrl,
        string? accountId = AccountId,
        string scheme = "https",
        string host = "rtub.example") =>
        new ContentSecurityPolicyBuilder(publicUrl, accountId).Build(scheme, host);

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
    /// leak into a directive that does not need it. cdnjs serves both cropper.min.js and
    /// cropper.min.css; unpkg serves leaflet.js and leaflet.css; jsdelivr serves only pixi.min.js;
    /// the Carto tiles are images only.
    /// </summary>
    [Fact]
    public void ExternalOrigins_AppearOnlyInTheDirectivesThatNeedThem()
    {
        var directives = Directives(Policy());

        directives["script-src"].Should().BeEquivalentTo(
            "'self'", "https://cdnjs.cloudflare.com", "https://unpkg.com", "https://cdn.jsdelivr.net");

        directives["style-src"].Should().BeEquivalentTo(
            "'self'", "https://cdnjs.cloudflare.com", "https://unpkg.com");

        directives["img-src"].Should().Contain("https://*.basemaps.cartocdn.com")
            .And.Contain("data:");

        // The tile CDN and the script CDNs are images / scripts respectively, never both.
        directives["script-src"].Should().NotContain("https://*.basemaps.cartocdn.com");
        directives["img-src"].Should().NotContain("https://cdn.jsdelivr.net");
        directives["style-src"].Should().NotContain("https://cdn.jsdelivr.net");
        directives["connect-src"].Should().NotContain("https://cdnjs.cloudflare.com");
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

    // ---------- the DEV production-reference origin (029 follow-up) ----------

    /// <summary>
    /// A DEV app running on a sanitized production snapshot inherits absolute production media
    /// URLs it must still be able to render. <c>Cloudflare:R2:ReferencePublicUrl</c> admits that
    /// one exact origin for reading, and nothing else: it reaches only the two directives that
    /// render media, never a directive that could execute or connect.
    /// </summary>
    [Fact]
    public void ReferencePublicUrl_AdmitsTheExactProductionOrigin_ForMediaOnly()
    {
        var directives = Directives(ReferencePolicy(ProdPublicUrl));

        directives["img-src"].Should().Contain(PublicUrl).And.Contain(ProdPublicUrl);
        directives["media-src"].Should().Contain(PublicUrl).And.Contain(ProdPublicUrl);

        directives["script-src"].Should().NotContain(ProdPublicUrl);
        directives["connect-src"].Should().NotContain(ProdPublicUrl);
        directives["frame-src"].Should().NotContain(ProdPublicUrl);
        directives["style-src"].Should().NotContain(ProdPublicUrl);
        directives["default-src"].Should().NotContain(ProdPublicUrl);
    }

    /// <summary>
    /// Production sets no reference origin, so its policy is byte-identical to what unit 025
    /// shipped. This is the regression guard for "production behaviour is unchanged".
    /// </summary>
    [Fact]
    public void NoReferencePublicUrl_LeavesThePolicyByteIdentical()
    {
        new ContentSecurityPolicyBuilder(PublicUrl, AccountId, null).Build("https", "rtub.example")
            .Should().Be(Policy());
    }

    /// <summary>
    /// An exact origin or nothing. Junk, a wildcard, a non-http scheme and a bare host all
    /// contribute no source at all rather than a permissive one.
    /// </summary>
    [Theory]
    [InlineData("*")]
    [InlineData("https://*")]
    [InlineData("https://*.r2.dev")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("pub-prod.r2.dev")]
    [InlineData("https://pub-prod.r2.dev; script-src 'unsafe-inline'")]
    public void ReferencePublicUrl_NeverAdmitsAWildcardOrInjectedText(string configured)
    {
        var directives = Directives(ReferencePolicy(configured));

        directives["img-src"].Should().Equal("'self'", "data:", "https://*.basemaps.cartocdn.com", PublicUrl);
        directives["media-src"].Should().Equal("'self'", PublicUrl, EndpointOrigin);
        directives.Should().NotContainKey("script-src 'unsafe-inline'");
        directives["script-src"].Should().NotContain("'unsafe-inline'");
    }

    /// <summary>
    /// A reference origin equal to the environment's own origin is dropped, so the header never
    /// lists the same source twice.
    /// </summary>
    [Fact]
    public void ReferencePublicUrl_EqualToTheCurrentOrigin_IsNotRepeated()
    {
        Directives(ReferencePolicy(PublicUrl))["img-src"]
            .Should().ContainSingle(s => s == PublicUrl);
    }

    /// <summary>
    /// Malformed configuration must never reach the header. A value carrying a <c>;</c> or a
    /// second directive, a non-http scheme, or plain junk contributes nothing - the source is
    /// dropped rather than replaced with something permissive.
    /// </summary>
    [Theory]
    [InlineData("https://evil.example; script-src 'unsafe-inline'")]
    [InlineData("' ; default-src *")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("pub-test.r2.dev")]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MalformedR2PublicUrl_CannotInjectPolicyText(string? configured)
    {
        var policy = Policy(publicUrl: configured, accountId: null);

        policy.Should().NotContain("unsafe-inline");
        policy.Should().NotContain("evil.example");
        policy.Should().NotContain("javascript:");
        policy.Should().NotContain("file:");

        var directives = Directives(policy);
        directives["img-src"].Should().Equal("'self'", "data:", "https://*.basemaps.cartocdn.com");
        directives["media-src"].Should().Equal("'self'");
    }

    /// <summary>
    /// The account id is interpolated into a hostname, so it is accepted only as a single DNS
    /// label. Anything else leaves the app framing nothing rather than framing everything.
    /// </summary>
    [Theory]
    [InlineData("abc 123")]
    [InlineData("abc.evil.example")]
    [InlineData("abc/../x")]
    [InlineData("abc; frame-src *")]
    [InlineData("-abc")]
    [InlineData("")]
    [InlineData(null)]
    public void MalformedR2AccountId_YieldsNoFrameSource(string? configured)
    {
        var policy = Policy(accountId: configured);

        policy.Should().NotContain("evil.example");
        policy.Should().NotContain("frame-src *");

        var directives = Directives(policy);
        directives["frame-src"].Should().Equal("'none'");
        directives["media-src"].Should().NotContain(s => s.Contains("cloudflarestorage"));
    }

    // ---------- Blazor's circuit ----------

    /// <summary>
    /// Blazor Server's circuit runs over a WebSocket, and <c>'self'</c> is not reliably read as
    /// covering ws:/wss: in every browser, so the exact origin of the current request is emitted.
    /// </summary>
    [Theory]
    [InlineData("https", "rtub.example", "wss://rtub.example")]
    [InlineData("https", "rtub.example:8443", "wss://rtub.example:8443")]
    [InlineData("http", "localhost:5000", "ws://localhost:5000")]
    [InlineData("http", "[::1]:5000", "ws://[::1]:5000")]
    public void WebSocketSource_MatchesTheRequestSchemeAndHost(string scheme, string host, string expected)
    {
        Directives(Policy(scheme: scheme, host: host))["connect-src"]
            .Should().Equal("'self'", expected);
    }

    /// <summary>
    /// The Host header is client-controlled. A value that is not a plain host[:port] is dropped
    /// rather than concatenated into the header - the circuit failing is recoverable, a policy
    /// with attacker-chosen text in it is not.
    /// </summary>
    [Theory]
    [InlineData("https", "evil.example; script-src 'unsafe-inline'")]
    [InlineData("https", "rtub.example/path")]
    [InlineData("https", "rtub.example:notaport")]
    [InlineData("ftp", "rtub.example")]
    [InlineData("https", "")]
    public void MalformedRequestHost_ContributesNoWebSocketSource(string scheme, string host)
    {
        var policy = Policy(scheme: scheme, host: host);

        policy.Should().NotContain("unsafe-inline");
        Directives(policy)["connect-src"].Should().Equal("'self'");
    }

    // ---------- static repository policy ----------

    /// <summary>
    /// The one script-src blocker unit 023 missed: memberMap.js built its Leaflet popup markup as
    /// a string, and that string carried an inline onerror attribute. Markup-only scans cannot see
    /// it. It now opts into the same delegated listener every avatar in .razor markup uses.
    /// </summary>
    [Fact]
    public void MemberMap_BuildsPopupAvatarsWithoutAnInlineHandler()
    {
        var content = ReadRepoFile("src", "RTUB.Web", "wwwroot", "js", "memberMap.js");

        JsMarkupEventHandler.IsMatch(content).Should().BeFalse(
            "the popup avatar must not carry an inline error handler under a strict script-src");
        content.Should().NotContain("this.src=",
            "the old handler body must be gone, not merely reformatted");
        content.Should().Contain("data-avatar-fallback",
            "the popup avatar must opt into the delegated fallback listener instead");
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
        var root = GetProjectRoot();

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
    /// Unit 021 found the Google Fonts preconnect in App.razor had no matching stylesheet and no
    /// @font-face anywhere. Removing it is what lets font-src stay 'self'; if the hint comes back
    /// without a font source, it is either dead weight or a directive the policy is missing.
    /// </summary>
    [Fact]
    public void NoWebFontServiceIsReferenced()
    {
        ReadRepoFile("src", "RTUB.Web", "App.razor")
            .Should().NotContain("fonts.googleapis.com",
                "the preconnect had no matching stylesheet; font-src is 'self'");

        var cssRoot = Path.Combine(GetProjectRoot(), "src", "RTUB.Web", "wwwroot", "css");

        Directory.EnumerateFiles(cssRoot, "*.css", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("@font-face", StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty("a local @font-face would need its own font-src review");
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
