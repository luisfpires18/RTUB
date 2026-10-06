using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Task 026 retired Games, Bets and MyTuno with no replacement: their Blazor pages and the PixiJS image proxy are gone,
/// so every old URL is a plain 404 (no redirect) for visitors and for an Owner alike, and the Blazor layout no longer
/// links to them or loads their scripts. The database tables stay until the later contract task (N-1 rule).
/// </summary>
public class RetiredGamesRoutesTests : IntegrationTestBase
{
    public RetiredGamesRoutesTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    public static TheoryData<string> RetiredUrls =>
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
    [MemberData(nameof(RetiredUrls))]
    public async Task RetiredUrl_IsNotFound_ForVisitors(string url)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was retired in 026 and nothing replaces it", url);
    }

    [Fact]
    public async Task RetiredUrls_AreNotFound_ForAnOwner_AndTheLayoutNoLongerOffersThem()
    {
        var (owner, _) = await CookieTestSession.SignInAsync(Factory, "retired-games-owner", "10.93.0.1", "Owner");

        foreach (var url in RetiredUrls)
        {
            (await owner.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} was retired in 026", url);
        }

        var page = await owner.GetAsync("/naipes");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("href=\"/leaderboard\"", "the signed-in nav is rendered, so the absences below mean something");
        html.Should().NotContain("href=\"/games\"").And.NotContain("href=\"/bets\"").And.NotContain("href=\"/my-tuno\"")
            .And.NotContain("gamesDropdown").And.NotContain("pixi").And.NotContain("cdn.jsdelivr.net")
            .And.NotContain("bmrGame.js").And.NotContain("avoidQuestions.js").And.NotContain("tomatoThrower.js")
            .And.NotContain("passaroMaluco.js");
    }
}
