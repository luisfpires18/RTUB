// CSP DEV production-reference origin, malformed R2 settings and connect-src
using System.Text.RegularExpressions;
using FluentAssertions;
using RTUB.Security;
using Xunit;

namespace RTUB.Web.Tests.Security;

public partial class ContentSecurityPolicyTests
{
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
        new ContentSecurityPolicyBuilder(PublicUrl, AccountId, null).Build()
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

        directives["img-src"].Should().Equal("'self'", "data:", "https://tile.openstreetmap.org", PublicUrl);
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
        directives["img-src"].Should().Equal("'self'", "data:", "https://tile.openstreetmap.org");
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

    // ---------- connect-src ----------

    /// <summary>
    /// Task 037 retired the Blazor host: nothing opens a WebSocket, so connect-src is 'self' alone (it used to add the
    /// request's own ws:/wss: origin for the /_blazor circuit). The policy no longer depends on the request at all.
    /// </summary>
    [Fact]
    public void ConnectSrc_IsSelfOnly_WithNoWebSocketSource()
    {
        var policy = Policy();

        Directives(policy)["connect-src"].Should().Equal("'self'");
        policy.Should().NotContain("ws:").And.NotContain("wss:");
    }
}
