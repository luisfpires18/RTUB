using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RTUB.Integration.Tests;

/// <summary>
/// Addresses, files and types that no longer exist must stay gone. Every page is the React shell or the static /Error,
/// and any other address answers a plain 404 (no redirect), for visitors and members alike. Games, Bets and MyTuno,
/// the /images slideshow admin and /labels have no replacement; their database tables are covered by
/// docs/db-cleanup-inventory.md. The Messages tables are kept for Messaging v2 (docs/messages-removal.md).
/// This file checks addresses; RetiredRoutesTests.Code.cs checks the source, the solution and the assemblies.
/// </summary>
public partial class RetiredRoutesTests : IntegrationTestBase
{
    private const string MessagesHubNegotiate = "/hubs/messages/negotiate?negotiateVersion=1";

    public RetiredRoutesTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    private HttpClient Anonymous() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static TheoryData<string> RetiredGameUrls =>
    [
        "/games",
        "/games/avoid-questions",
        "/games/bmr-bebe-mais-rui",
        "/games/passaro-maluco",
        "/games/tomato-thrower",
        "/bets",
        "/my-tuno",
        "/my-tuno/arena",
        "/my-tuno/stages",
        "/my-tuno/boss-mode",
        "/my-tuno/survive",
        "/my-tuno/all-characters",
        "/owner/stage-enemies",
        "/owner/weapon-drink-config",
        "/api/cdn/image?path=images/production/drinks/fino.webp",
    ];

    [Theory]
    [MemberData(nameof(RetiredGameUrls))]
    [InlineData("/images")]
    [InlineData("/labels")]
    [InlineData("/this-page-does-not-exist")]
    [InlineData("/portal")]
    [InlineData("/portal/")]
    [InlineData("/portal/privacy")]
    [InlineData("/portal/profile")]
    [InlineData("/portal/request")]
    [InlineData("/portal/request?utm_source=cartaz")]
    [InlineData("/portal/unknown")]
    [InlineData("/portal/privacidade")]
    [InlineData("/portal/perfil")]
    [InlineData("/portal/pedidos")]
    [InlineData("/member/profile")]
    [InlineData("/member/map")]
    [InlineData("/music/songs/12")]
    [InlineData("/music/songs/3?from=share")]
    public async Task RetiredUrl_IsNotFound_ForVisitors(string url)
    {
        (await Anonymous().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} must not come back", url);
    }

    [Fact]
    public async Task MemberEvents_IsNotFound_ForEveryMethod()
    {
        var client = Anonymous();

        (await client.GetAsync("/member/events")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/member/events"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync("/member/events", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} belonged to the Blazor host, which is gone", url);
    }

    [Fact]
    public async Task RetiredGameUrls_AreNotFound_ForAnOwner_AndTheShellOffersNoneOfThem()
    {
        var (owner, _) = await CookieTestSession.SignInAsync(Factory, "retired-games-owner", "10.93.0.1", "Owner");

        foreach (var url in RetiredGameUrls)
        {
            (await owner.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} has no replacement", url);
        }

        // The signed-in React shell and the menu it is given (MemberShellTests covers the menu's links) carry none of it.
        var page = await owner.GetAsync("/events");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync() + await owner.GetStringAsync("/api/account/me");
        html.Should().Contain("id=\"root\"");
        html.Should().NotContain("href=\"/games\"").And.NotContain("href=\"/bets\"").And.NotContain("href=\"/my-tuno\"")
            .And.NotContain("gamesDropdown").And.NotContain("pixi").And.NotContain("cdn.jsdelivr.net")
            .And.NotContain("bmrGame.js").And.NotContain("avoidQuestions.js").And.NotContain("tomatoThrower.js")
            .And.NotContain("passaroMaluco.js");
    }

    [Fact]
    public async Task OnlyTheBareImagesUrl_IsGone_TheImageFilesUnderItAreStillServed()
    {
        var client = Anonymous();

        (await client.GetAsync("/images")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/images/default-avatar.webp")).StatusCode.Should().Be(HttpStatusCode.OK,
            "the static files middleware serves wwwroot/images");
    }

    [Fact]
    public async Task ImagesAndLabels_AreNotFound_ForAnOwner_AndTheShellOffersNeither()
    {
        var (admin, _) = await CookieTestSession.SignInAsync(Factory, "retired-images-admin", "10.95.0.1", "Owner");

        foreach (var url in new[] { "/images", "/labels" })
        {
            (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} has no replacement", url);
        }

        // The signed-in React shell and its menu carry none of it.
        var page = await admin.GetAsync("/events");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync() + await admin.GetStringAsync("/api/account/me");
        html.Should().Contain("id=\"root\"");
        html.Should().NotContain("href=\"/images\"").And.NotContain("href=\"/labels\"");
    }

    /// <summary>
    /// /messages is the React Messaging v2 page: a visitor goes to sign in. /messages/{id} and the SignalR hub are plain
    /// 404s, and the inbox scripts are not served.
    /// </summary>
    [Fact]
    public async Task OldInboxUrls_StayNotFound_AndMessagesSendsVisitorsToSignIn()
    {
        var client = Anonymous();

        (await client.GetAsync("/messages/1")).StatusCode.Should().Be(HttpStatusCode.NotFound, "/messages/{id} is not a page");
        var page = await client.GetAsync("/messages");
        page.StatusCode.Should().Be(HttpStatusCode.Redirect, "Messaging v2 is for members");
        page.Headers.Location!.OriginalString.Should().Be("/login?returnUrl=%2Fmessages");
    }

    [Fact]
    public async Task TheMessagesHub_IsNotFound_ForVisitors()
    {
        (await Anonymous().PostAsync(MessagesHubNegotiate, content: null)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "no SignalR hub is mapped");
    }

    [Fact]
    public async Task AMember_GetsTheReactV2Page_AndNothingOfTheOldInbox()
    {
        var (member, _) = await CookieTestSession.SignInAsync(Factory, "retired-messages-member", "10.94.0.2");

        (await member.GetAsync("/messages/1")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await member.PostAsync(MessagesHubNegotiate, content: null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var page = await member.GetAsync("/messages");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("id=\"root\"").And.NotContain("blazor.web.js").And.NotContain("_blazor");
        html.Should().NotContain("navbar-message-badge")
            .And.NotContain("unreadMessages.js").And.NotContain("messageComposer.js").And.NotContain("messageScroller.js");
    }

    [Fact]
    public async Task PwaManifest_HasNoMessagesShortcut()
    {
        using var manifest = JsonDocument.Parse(await Factory.CreateClient().GetStringAsync("/manifest.webmanifest"));

        var urls = manifest.RootElement.GetProperty("shortcuts").EnumerateArray()
            .Select(s => s.GetProperty("url").GetString())
            .ToList();
        urls.Should().NotBeEmpty().And.NotContain("/messages");
    }
}
