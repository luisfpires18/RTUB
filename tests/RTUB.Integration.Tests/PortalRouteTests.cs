using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RTUB.Integration.Tests;

/// <summary>
/// Route ownership of the React shell (docs/react-portal-pilot.md): the pages are served from the committed build in
/// wwwroot/portal, GET/HEAD only. Addresses that no longer exist are in RetiredRoutesTests.
/// </summary>
public partial class PortalRouteTests : IntegrationTestBase
{
    public PortalRouteTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private HttpClient NoRedirectClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/")]
    [InlineData("/privacy")]
    [InlineData("/profile")]
    [InlineData("/request")]
    [InlineData("/music")]
    [InlineData("/music/albums/1")]
    [InlineData("/login")]
    [InlineData("/login?ReturnUrl=%2Fprofile")]
    [InlineData("/roles")]
    [InlineData("/roles?fy=2024-2025")]
    [InlineData("/gallery")]
    [InlineData("/gallery?item=1")]
    [InlineData("/events")]
    [InlineData("/events?season=2025-2026&type=Festival")]
    [InlineData("/events/1")]
    [InlineData("/news")]
    [InlineData("/share")]
    [InlineData("/share?title=x&text=y&url=z")]
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
        html.Should().NotContain("blazor.web.js", "{0} is React, not a Blazor page", path);
        html.Should().Contain("href=\"/manifest.webmanifest\"", "the portal must keep the one installed-app identity");
        html.Should().Contain("src=\"/js/sw-register.js\"", "the portal reuses the single service-worker registration path");
        html.Should().NotContain("sw-update-toast", "the update prompt was retired in 006");
    }

    /// <summary>
    /// 006: the "Versão de testes" strip and the "Nova versão disponível! · Atualizar · Depois" update
    /// prompt are gone from the shell, its sources, its committed build and the one script it shares
    /// with Blazor. Matched on ASCII fragments because the build escapes non-ASCII text.
    /// </summary>
    [Fact]
    public void ReactShell_CarriesNoTestBannerOrUpdatePrompt()
    {
        var root = RepoPaths.Root();
        var web = Path.Combine(root, "src", "RTUB.Web");
        var files = Directory.GetFiles(Path.Combine(web, "portal", "src"), "*", SearchOption.AllDirectories)
            .Append(Path.Combine(web, "portal", "index.html"))
            .Concat(Directory.GetFiles(Path.Combine(web, "wwwroot", "portal"), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".html") || f.EndsWith(".css") || (f.EndsWith(".js") && !Path.GetFileName(f).StartsWith("vendor-"))))
            .Append(Path.Combine(web, "wwwroot", "js", "sw-register.js"))
            .ToList();

        files.Should().HaveCountGreaterThan(5);
        foreach (var file in files)
        {
            File.ReadAllText(file).Should()
                .NotContain("de testes").And.NotContain("PilotBanner").And.NotContain("pilot__")
                .And.NotContain("Nova vers").And.NotContain("Atualizar").And.NotContain("rtub-sw-")
                .And.NotContain("sw-update-toast", "{0} must not bring back the test banner or the update prompt", Path.GetFileName(file));
        }
    }

    /// <summary>
    /// The shell itself is never cached (above), and every script, stylesheet and font it loads has a
    /// content hash in its name, so a deploy is picked up on the next navigation with no prompt.
    /// </summary>
    [Fact]
    public async Task PortalShell_LoadsOnlyContentHashedAssets()
    {
        var html = await Factory.CreateClient().GetStringAsync("/");

        var assets = Regex.Matches(html, "(?:src|href)=\"(/portal/assets/[^\"]+)\"").Select(m => m.Groups[1].Value).ToList();

        assets.Should().NotBeEmpty().And.OnlyContain(a => Regex.IsMatch(a, @"-[A-Za-z0-9_-]{8}\.(js|css)$"),
            "the React build must stay cache-busted by content hash");
    }

    [Fact]
    public async Task PortalShell_ReferencesOnlyAssetsThatExist()
    {
        var client = Factory.CreateClient();
        var html = await client.GetStringAsync("/");

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
    public void ReactSourcesAndBuild_LinkOnlyToCleanRoutes()
    {
        // A quoted "/portal", "/portal#…" or "/portal/<page>" is a link to the pilot URLs. The build's
        // own asset base ("/portal/" + file, /portal/assets/…) is a static-file folder, not a page.
        var pilotLink = new Regex(@"[""'`(]/portal(?:/(?:privacy|profile|request)\b|[#?""'`)])");
        var root = RepoPaths.Root();
        var files = Directory.GetFiles(Path.Combine(root, "src", "RTUB.Web", "portal", "src"), "*", SearchOption.AllDirectories)
            .Append(Path.Combine(root, "src", "RTUB.Web", "portal", "index.html"))
            .Concat(Directory.GetFiles(Path.Combine(root, "src", "RTUB.Web", "wwwroot", "portal"), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".html") || (f.EndsWith(".js") && !Path.GetFileName(f).StartsWith("vendor-"))))
            .ToList();

        files.Should().HaveCountGreaterThan(5);
        foreach (var file in files)
        {
            pilotLink.Matches(File.ReadAllText(file)).Select(m => m.Value)
                .Should().BeEmpty("{0} must link to the clean routes, not /portal", Path.GetFileName(file));
        }
    }

    [Fact]
    public async Task ShellRoutes_AreGetAndHeadOnly()
    {
        var client = NoRedirectClient();

        // A form post to /request is refused too: requests go to POST /api/public/requests. Unknown paths: RetiredRoutesTests.
        foreach (var path in new[] { "/", "/privacy", "/profile", "/request", "/music", "/music/albums/1", "/events", "/events/1" })
        {
            (await client.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
                "{0} is GET/HEAD only", path);
        }
    }
}
