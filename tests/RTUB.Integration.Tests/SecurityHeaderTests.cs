using System.Net;
using FluentAssertions;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Pins the security headers unit 021 configures in Program.cs, plus the enforced
/// Content-Security-Policy added by unit 025.
///
/// The CSP assertions here are about how the header is DELIVERED - enforced rather than
/// report-only, exactly once, and on documents rather than on every response. The policy's
/// contents are pinned by RTUB.Web.Tests' ContentSecurityPolicyTests, against the builder.
///
/// Strict-Transport-Security is likewise not asserted: UseHsts only runs outside Development,
/// and the test host is not a production environment.
/// </summary>
public class SecurityHeaderTests : IntegrationTestBase
{
    public SecurityHeaderTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private const string Nosniff = "X-Content-Type-Options";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string FrameOptions = "X-Frame-Options";
    private const string PermissionsPolicy = "Permissions-Policy";
    private const string Csp = "Content-Security-Policy";
    private const string CspReportOnly = "Content-Security-Policy-Report-Only";

    private static string Single(HttpResponseMessage response, string name)
    {
        response.Headers.TryGetValues(name, out var values).Should().BeTrue(
            "response to {0} should carry {1}", response.RequestMessage?.RequestUri, name);
        return values!.Single();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/Events")]
    public async Task Page_CarriesEverySecurityHeader(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        Single(response, Nosniff).Should().Be("nosniff");
        Single(response, ReferrerPolicy).Should().Be("strict-origin-when-cross-origin");
        Single(response, FrameOptions).Should().Be("DENY");
        Single(response, PermissionsPolicy)
            .Should().Be("camera=(), microphone=(), geolocation=(), payment=()");
    }

    [Fact]
    public async Task StaticFile_CarriesSecurityHeaders()
    {
        // The middleware is registered ahead of UseStaticFiles, so assets served by the
        // static-file branch - not just endpoint responses - are covered.
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/manifest.webmanifest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Single(response, Nosniff).Should().Be("nosniff");
        Single(response, FrameOptions).Should().Be("DENY");
    }

    [Fact]
    public async Task NotFound_CarriesSecurityHeaders()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/this-route-does-not-exist");

        Single(response, Nosniff).Should().Be("nosniff");
        Single(response, FrameOptions).Should().Be("DENY");
    }

    [Fact]
    public async Task HealthEndpoint_CarriesSecurityHeaders()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Single(response, Nosniff).Should().Be("nosniff");
        Single(response, FrameOptions).Should().Be("DENY");
    }

    /// <summary>
    /// The policy must be enforced. A report-only header collects violations and blocks nothing,
    /// so shipping one in its place would look identical in a header dump and do nothing at all.
    /// </summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/Events")]
    public async Task Page_CarriesExactlyOneEnforcedContentSecurityPolicy(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        response.Headers.GetValues(Csp).Should().ContainSingle();
        response.Headers.Contains(CspReportOnly).Should().BeFalse(
            "025 ships the enforced header, not a report-only one");

        var policy = Single(response, Csp);
        policy.Should().NotContain("'unsafe-inline'").And.NotContain("'unsafe-eval'");
        policy.Should().Contain("script-src-attr 'none'").And.Contain("style-src-attr 'none'");
        policy.Should().Contain("object-src 'none'").And.Contain("frame-ancestors 'none'");
        policy.Should().Contain("base-uri 'self'").And.Contain("form-action 'self'");
    }

    /// <summary>
    /// The WebSocket source has to match the host the browser actually connected to, or the
    /// Blazor circuit is refused by the very policy meant to protect it.
    /// </summary>
    [Fact]
    public async Task Page_AllowsTheBlazorCircuitOnTheRequestHost()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/");
        var requestUri = response.RequestMessage!.RequestUri!;
        var expected = requestUri.Scheme == "https"
            ? $"wss://{requestUri.Authority}"
            : $"ws://{requestUri.Authority}";

        Single(response, Csp).Should().Contain($"connect-src 'self' {expected}");
    }

    /// <summary>
    /// CSP is scoped to documents. A policy served with service-worker.js would govern the worker
    /// itself, and the worker re-fetches the cross-origin resources it caches - none of which
    /// connect-src lists, because the page never fetches them directly.
    /// </summary>
    [Theory]
    [InlineData("/service-worker.js")]
    [InlineData("/manifest.webmanifest")]
    [InlineData("/js/avatarFallback.js")]
    public async Task NonDocumentResponse_CarriesNoContentSecurityPolicy(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains(Csp).Should().BeFalse(
            "{0} is not a document; a policy here would govern the worker/script context", path);
        Single(response, Nosniff).Should().Be("nosniff");
    }

    [Fact]
    public async Task Headers_AreSetOnce_NotAppendedPerPass()
    {
        // Assigned by indexer, so an UseExceptionHandler re-execution cannot double them up.
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/");

        response.Headers.GetValues(Nosniff).Should().ContainSingle();
        response.Headers.GetValues(FrameOptions).Should().ContainSingle();
        response.Headers.GetValues(ReferrerPolicy).Should().ContainSingle();
        response.Headers.GetValues(PermissionsPolicy).Should().ContainSingle();
        response.Headers.GetValues(Csp).Should().ContainSingle();
    }
}
