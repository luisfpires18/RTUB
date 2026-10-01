using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Route ownership of the React public shell (React track 001-004, docs/react-portal-pilot.md).
/// React owns exactly /, /privacy, /profile and /request, served from the committed build in
/// wwwroot/portal; the pilot's /portal... URLs redirect there; every other page stays Blazor.
/// </summary>
public class PortalRouteTests : IntegrationTestBase
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
        var root = FindRepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "RTUB.Web", "portal", "src"))
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

    [Theory]
    [InlineData("/portal", "/")]
    [InlineData("/portal/", "/")]
    [InlineData("/portal/privacy", "/privacy")]
    [InlineData("/portal/profile", "/profile")]
    [InlineData("/portal/request", "/request")]
    [InlineData("/portal/request?utm_source=cartaz", "/request?utm_source=cartaz")]
    public async Task PilotPortalUrls_RedirectTemporarilyToTheCleanRoute(string path, string target)
    {
        var response = await NoRedirectClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "temporary (302) while DEV is hybrid");
        response.Headers.Location!.ToString().Should().Be(target);
    }

    [Fact]
    public async Task PathsOutsideTheReactRoutes_AreNotServedTheShell()
    {
        var client = NoRedirectClient();

        (await client.GetAsync("/portal/unknown")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The Portuguese routes of the first drafts were renamed to English before any release.
        foreach (var old in new[] { "/portal/privacidade", "/portal/perfil", "/portal/pedidos" })
        {
            (await client.GetAsync(old)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was renamed", old);
        }

        foreach (var path in new[] { "/", "/privacy", "/profile", "/portal", "/portal/request", "/music", "/music/albums/1" })
        {
            (await client.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
                "{0} is GET/HEAD only", path);
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/privacy")]
    [InlineData("/profile")]
    [InlineData("/request")]
    [InlineData("/music")]
    [InlineData("/music/albums/{id:int}")]
    [InlineData("/music/songs/{AlbumId:int}")]
    public void NoBlazorComponent_OwnsAReactRoute(string route)
    {
        var owners = typeof(RTUB.App).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
                .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
                .Select(r => (Type: t, r.Template)))
            .Where(r => r.Template.Equals(route, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Type.FullName)
            .ToList();

        owners.Should().BeEmpty("React owns {0}; its Blazor page was retired", route);
    }

    // ---------- temporary Blazor bridges ----------

    [Theory]
    [InlineData("/login")]
    [InlineData("/events")]
    [InlineData("/gallery")]
    [InlineData("/roles")]
    public async Task BlazorBridgeRoutes_StayBlazor(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotContain("id=\"splash\"", "{0} is still owned by Blazor", path);
        html.Should().Contain("blazor.web.js", "{0} is still a Blazor page", path);
    }

    [Fact]
    public async Task BlazorMemberProfile_LivesAtMemberProfile_AndStillRequiresSignIn()
    {
        var response = await NoRedirectClient().GetAsync("/member/profile");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/login").And.Contain("ReturnUrl=%2Fmember%2Fprofile");
    }

    // ---------- retired Blazor Music (React track 006) ----------

    [Theory]
    [InlineData("/music/songs/12", "/music/albums/12")]
    [InlineData("/music/songs/3?from=share", "/music/albums/3?from=share")]
    public async Task RetiredBlazorAlbumPage_RedirectsToTheReactAlbum(string path, string target)
    {
        var response = await NoRedirectClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be(target);
    }

    [Fact]
    public void HomeMusicLink_GoesToTheReactMusicPage()
    {
        var src = Path.Combine(FindRepoRoot(), "src", "RTUB.Web", "portal", "src");

        File.ReadAllText(Path.Combine(src, "content.ts")).Should().Contain("music: '/music'");
        File.ReadAllText(Path.Combine(src, "Home.tsx")).Should().Contain("<MoreLink href={portal.music}>");
        File.ReadAllText(Path.Combine(src, "App.tsx")).Should().Contain("{ music: portal.music }",
            "the top bar, the mobile menu and the footer open the Music page, not the home section");
        Directory.GetFiles(src).Select(File.ReadAllText).Should().NotContain(t => Regex.IsMatch(t, "(?<!/api)/music/songs/"),
            "the React Music area links to /music/albums/{id}, never the retired Blazor route");
    }

    // ---------- retired Blazor /request (React track 003) ----------

    [Fact]
    public async Task RetiredBlazorRequest_HasNoSubmissionPathLeft()
    {
        var response = await NoRedirectClient().PostAsync("/request",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "x" }));

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
            "only the React shell (GET/HEAD) exists at /request; submissions go to POST /api/public/requests");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "RTUB.Web")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
