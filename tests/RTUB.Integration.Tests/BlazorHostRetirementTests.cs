using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Task 037 retired the Blazor host once 036 left no Blazor page: no Razor component is compiled, nothing maps
/// /_blazor or serves blazor.web.js, and RTUB.Shared is gone. Every page is the React shell or the static /Error.
/// </summary>
public class BlazorHostRetirementTests : IntegrationTestBase
{
    public BlazorHostRetirementTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private HttpClient Anonymous() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public void NoRazorComponent_IsLeftInTheSource_OrTheAssembly()
    {
        var src = Path.Combine(RepoRoot(), "src");
        Directory.EnumerateFiles(src, "*.razor*", SearchOption.AllDirectories)
            .Where(f => !f.Replace('\\', '/').Contains("/obj/") && !f.Replace('\\', '/').Contains("/bin/"))
            .Should().BeEmpty("App.razor, MainLayout, _Imports and every component went with the host");

        var web = typeof(RTUB.Program).Assembly;
        web.GetTypes().Where(t => typeof(IComponent).IsAssignableFrom(t)).Should().BeEmpty("no Blazor component is compiled");
        web.GetTypes().SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false)).Should().BeEmpty("0 Blazor pages");
    }

    [Fact]
    public void RTUBShared_IsGone_FromTheDisk_TheSolution_AndTheWebProject()
    {
        var root = RepoRoot();
        File.Exists(Path.Combine(root, "src", "RTUB.Shared", "RTUB.Shared.csproj")).Should().BeFalse();
        File.Exists(Path.Combine(root, "tests", "RTUB.Shared.Tests", "RTUB.Shared.Tests.csproj")).Should().BeFalse();
        File.ReadAllText(Path.Combine(root, "RTUB.sln")).Should().NotContain("RTUB.Shared");
        File.ReadAllText(Path.Combine(root, "src", "RTUB.Web", "RTUB.csproj")).Should().NotContain("RTUB.Shared");
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).Should().NotContain("RTUB.Shared");
    }

    [Fact]
    public void Startup_NoLongerHostsBlazor()
    {
        var src = Path.Combine(RepoRoot(), "src", "RTUB.Web");
        var startup = File.ReadAllText(Path.Combine(src, "Program.cs")) + File.ReadAllText(Path.Combine(src, "Extensions", "ServiceCollectionExtensions.cs"));

        startup.Should().NotContainAny("MapRazorComponents", "AddRazorComponents", "AddInteractiveServerComponents",
            "AddInteractiveServerRenderMode", "AddCascadingAuthenticationState", "ServerAuthenticationStateProvider",
            "CircuitHost", "_blazor", "HubOptions");
    }

    [Theory]
    [InlineData("GET", "/_framework/blazor.web.js")]
    [InlineData("GET", "/_blazor")]
    [InlineData("POST", "/_blazor/negotiate?negotiateVersion=1")]
    [InlineData("GET", "/_blazor/initializers")]
    [InlineData("GET", "/RTUB.styles.css")]
    [InlineData("GET", "/css/site.css")]
    [InlineData("GET", "/js/blazorStartup.js")]
    [InlineData("GET", "/lib/bootstrap/bootstrap.min.css")]
    public async Task BlazorRuntime_IsNotServed(string method, string url)
    {
        var response = await Anonymous().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} belonged to the retired Blazor host", url);
    }

    /// <summary>Every page answer is the React shell: never blazor.web.js, never a Blazor marker.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/forgot-password")]
    [InlineData("/reset-password")]
    [InlineData("/confirm-email")]
    [InlineData("/privacy")]
    [InlineData("/events")]
    [InlineData("/news")]
    public async Task PublicPages_AreTheReactShell(string url)
    {
        var response = await Anonymous().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"root\"").And.Contain("/js/sw-register.js");
        html.Should().NotContainAny("blazor.web.js", "_framework", "_blazor", "<!--Blazor");
    }

    [Theory]
    [InlineData("/profile")]
    [InlineData("/meetings")]
    [InlineData("/naipes")]
    [InlineData("/emails")]
    [InlineData("/notifications")]
    [InlineData("/owner/db")]
    public async Task MembersPages_SendVisitorsToSignIn_NotToABlazorPage(string url)
    {
        var response = await Anonymous().GetAsync(url);

        if (url == "/profile")
        {
            // Public: the editor shows a visitor how to sign in.
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("id=\"root\"").And.NotContain("blazor.web.js");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().StartWith("/login?returnUrl=");
    }

    [Fact]
    public async Task Error_IsTheStaticServerPage()
    {
        var response = await Anonymous().GetAsync("/Error");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Algo correu mal").And.NotContain("<script").And.NotContain("id=\"root\"");
    }

    [Theory]
    [InlineData("/share")]
    [InlineData("/games")]
    [InlineData("/bets")]
    [InlineData("/my-tuno")]
    [InlineData("/messages/1")]
    [InlineData("/images")]
    [InlineData("/labels")]
    [InlineData("/this-page-does-not-exist")]
    public async Task RemovedAndUnknownRoutes_StayNotFound(string url)
    {
        (await Anonymous().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} must not come back", url);
    }

    [Fact]
    public async Task Share_PostIsNotFound_AndTheManifestOffersNoShareTarget()
    {
        (await Anonymous().PostAsync("/share", content: null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Anonymous().GetStringAsync("/manifest.webmanifest")).Should().NotContain("share_target");
    }

    [Fact]
    public void ServiceWorker_PrecachesNoBlazorAsset()
    {
        var sw = File.ReadAllText(Path.Combine(RepoRoot(), "src", "RTUB.Web", "wwwroot", "service-worker.js"));

        sw.Should().NotContainAny("blazor.web.js", "_framework", "_blazor");
        sw.Should().Contain("'/offline.html'", "offline support stays");
        sw.Should().Contain("addEventListener('push'", "push support stays");
    }

    [Fact]
    public void TheAnnouncementListener_IsInTheReactShell_AndSaysNothingAboutMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        var app = File.ReadAllText(Path.Combine(portal, "App.tsx"));

        app.Should().Contain("<SystemAnnouncement />").And.Contain("getAnnouncement()");
        File.ReadAllText(Path.Combine(portal, "api.ts")).Should().Contain("fetch('/api/announcement'");
        app.Should().NotContainAny("migra", "Migra", "Blazor");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RTUB.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
