using System.Net;
using FluentAssertions;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Pins the security headers unit 021 configures in Program.cs.
///
/// Only the headers RTUB intentionally sets are asserted. Content-Security-Policy is
/// deliberately absent (see STATE.md unit 021 - eval/inline blockers) and is therefore not
/// asserted in either direction, so enabling it later needs no edit here.
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
    }
}
