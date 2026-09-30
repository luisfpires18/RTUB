using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Route ownership for the React public-portal pilot (React track 001, docs/react-portal-pilot.md).
/// React owns exactly /portal, /portal/privacy, /portal/profile and /portal/request, served from the committed build in
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
    [InlineData("/portal/privacy")]
    [InlineData("/portal/profile")]
    [InlineData("/portal/request")]
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

        // The Portuguese routes of the first drafts were renamed to English before any release.
        foreach (var old in new[] { "/portal/privacidade", "/portal/perfil", "/portal/pedidos" })
        {
            (await client.GetAsync(old)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was renamed", old);
        }
        (await client.PostAsync("/portal", null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/privacy")]
    [InlineData("/login")]
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

    // ---------- retired Blazor /request (React track 003) ----------

    [Theory]
    [InlineData("/request", "/portal/request")]
    [InlineData("/request?utm_source=cartaz", "/portal/request?utm_source=cartaz")]
    public async Task RetiredBlazorRequest_RedirectsToTheReactForm(string path, string target)
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be(target);
    }

    [Fact]
    public void NoBlazorComponent_OwnsTheRetiredRequestRoute()
    {
        var owners = typeof(RTUB.App).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
                .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
                .Select(r => (Type: t, r.Template)))
            .Where(r => r.Template.Equals("/request", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Type.FullName)
            .ToList();

        owners.Should().BeEmpty("React /portal/request is the only public request form");
    }

    [Fact]
    public async Task RetiredBlazorRequest_HasNoSubmissionPathLeft()
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/request", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "x" }));

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed, "only the redirect (GET/HEAD) exists at /request");
    }
}
