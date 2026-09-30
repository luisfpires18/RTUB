using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Route ownership for the React public-portal pilot (React track 001, docs/react-portal-pilot.md).
/// React owns exactly /portal, /portal/privacidade, /portal/perfil and /portal/pedidos, served from the committed build in
/// wwwroot/portal; every other page, including the public ones the portal links to, stays Blazor.
/// </summary>
public class PortalRouteTests : IntegrationTestBase
{
    public PortalRouteTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("/portal")]
    [InlineData("/portal/")]
    [InlineData("/portal/privacidade")]
    [InlineData("/portal/perfil")]
    [InlineData("/portal/pedidos")]
    public async Task ReactRoutes_ServeTheUncachedPortalShellUnderTheEnforcedCsp(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        response.Headers.CacheControl!.NoCache.Should().BeTrue("a cached shell would point at a previous deploy's hashed assets");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle();

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"root\"").And.Contain("id=\"splash\"");
        html.Should().Contain("href=\"/manifest.webmanifest\"", "the portal must keep the one installed-app identity");
        html.Should().Contain("src=\"/js/sw-register.js\"", "the portal reuses the single service-worker registration path");
    }

    [Fact]
    public async Task PortalShell_ReferencesOnlyAssetsThatExist()
    {
        var client = Factory.CreateClient();
        var html = await client.GetStringAsync("/portal");

        var assets = Regex.Matches(html, "(?:src|href)=\"(/portal/assets/[^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        assets.Should().Contain(a => a.EndsWith(".js")).And.Contain(a => a.EndsWith(".css"),
            "wwwroot/portal is the committed build; run `npm run build:portal` after changing portal/");

        foreach (var asset in assets)
        {
            var response = await client.GetAsync(asset);
            response.StatusCode.Should().Be(HttpStatusCode.OK, "{0} is referenced by the portal shell", asset);
        }
    }

    [Fact]
    public async Task PathsOutsideThePortalRoutes_AreNotServedTheShell()
    {
        var client = Factory.CreateClient();

        (await client.GetAsync("/portal/unknown")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync("/portal", null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/privacy")]
    [InlineData("/login")]
    [InlineData("/request")]
    [InlineData("/events")]
    [InlineData("/music")]
    [InlineData("/gallery")]
    [InlineData("/roles")]
    public async Task LegacyBlazorRoutes_StayBlazor(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("id=\"splash\"",
            "{0} is owned by Blazor during the pilot", path);
    }
}
