using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RTUB.Integration.Tests;

/// <summary>
/// Every page is the React shell or the static /Error. Members' pages send a visitor to sign in. What is gone is in
/// RetiredRoutesTests.
/// </summary>
public class ShellPagesTests : IntegrationTestBase
{
    public ShellPagesTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private HttpClient Anonymous() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

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
    [InlineData("/share")]
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

    /// <summary>/share is the React Web Share Target (GET); nothing is ever posted to it.</summary>
    [Fact]
    public async Task Share_PostIsNotAllowed_AndTheManifestOffersTheShareTarget()
    {
        (await Anonymous().PostAsync("/share", content: null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await Anonymous().GetStringAsync("/manifest.webmanifest")).Should().Contain("\"share_target\"");
    }

    [Fact]
    public void TheAnnouncementListener_IsInTheReactShell_AndSaysNothingAboutMigration()
    {
        var portal = Path.Combine(RepoRoot(), "src", "RTUB.Web", "portal", "src");
        var app = PortalSource.Cat(portal, "components/App*.tsx");

        app.Should().Contain("<SystemAnnouncement />").And.Contain("getAnnouncement()");
        File.ReadAllText(Path.Combine(portal, "data/apiSession.ts")).Should().Contain("fetch('/api/announcement'");
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
