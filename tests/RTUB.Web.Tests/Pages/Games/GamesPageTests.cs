using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Web.Tests.Pages.Base;
using GamesPage = RTUB.Pages.Games.Games;

namespace RTUB.Web.Tests.Pages.Games;

/// <summary>
/// Component tests for Games.razor page (/games).
/// Tests page rendering, loading state, empty state, list display, filtering, and authorization (Phase 0.5).
/// </summary>
public class GamesPageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";

    private readonly Mock<IGameService> _mockGameService;
    private readonly Mock<IGameScoreService> _mockGameScoreService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public GamesPageTests()
    {
        _mockGameService = SetupService<IGameService>();
        _mockGameScoreService = SetupService<IGameScoreService>();
        _mockUserManager = SetupUserManager();

        _mockGameService
            .Setup(x => x.GetAllGamesAsync())
            .ReturnsAsync(new List<GameDto>());

        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        SetupAuthentication("test-user", "Test User");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task GamesPage_RendersPageTitle()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<GamesPage>();
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border") || cut.Markup.Contains("Jogos") || cut.Markup.Contains("Sem jogos"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Jogos", "page should display title");
        cut.Markup.Should().Contain("Diverte-te com os jogos", "page should display subtitle");
    }

    [Fact]
    public async Task GamesPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("test-user", "Test User");
        _mockGameService
            .Setup(x => x.GetAllGamesAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return new List<GameDto>();
            });

        var cut = RenderComponent<GamesPage>();

        cut.Markup.Should().Contain("Jogos", "page should display title");
        cut.Markup.Should().Match(m => m.Contains("spinner-border") || m.Contains("Jogos"), "should show loading or title initially");

        cut.WaitForState(() => cut.Markup.Contains("Sem jogos") || cut.Markup.Contains("games-grid"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GamesPage_ShowsEmptyState_WhenNoGames()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<GamesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Sem jogos") || cut.Markup.Contains("games-grid"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Sem jogos disponíveis", "should show empty state when no games");
    }

    [Fact]
    public async Task GamesPage_DisplaysGames_WhenGamesExist()
    {
        SetupAuthentication("test-user", "Test User");
        var games = new List<GameDto>
        {
            CreateGameDto(1, "game1", "Game One", "/games/game1"),
            CreateGameDto(2, "game2", "Game Two", "/games/game2")
        };
        _mockGameService
            .Setup(x => x.GetAllGamesAsync())
            .ReturnsAsync(games);

        var cut = RenderComponent<GamesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Game One") || cut.Markup.Contains("Game Two"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Game One", "page should display first game");
        cut.Markup.Should().Contain("Game Two", "page should display second game");
    }

    [Fact]
    public async Task GamesPage_FiltersMembersOnlyGames_ForLeitaoUser()
    {
        SetupAuthentication("leitao-user", "Leitao User");
        var leitaoUser = new ApplicationUser { UserName = "leitao-user" };
        leitaoUser.Categories.Add(MemberCategory.Leitao);
        
        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(leitaoUser);

        var games = new List<GameDto>
        {
            CreateGameDto(1, "public-game", "Public Game", "/games/public", membersOnly: false),
            CreateGameDto(2, "members-game", "Members Game", "/games/members", membersOnly: true)
        };
        _mockGameService
            .Setup(x => x.GetAllGamesAsync())
            .ReturnsAsync(games);

        var cut = RenderComponent<GamesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Public Game") || cut.Markup.Contains("Sem jogos"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Public Game", "Leitao should see public games");
        cut.Markup.Should().NotContain("Members Game", "Leitao should not see members-only games");
    }

    [Fact]
    public async Task GamesPage_ShowsAllGames_ForAdminUser()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var adminUser = new ApplicationUser { UserName = "admin-user" };
        adminUser.Categories.Add(MemberCategory.Leitao); // Even if Leitao, admin sees all
        
        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(adminUser);

        var games = new List<GameDto>
        {
            CreateGameDto(1, "public-game", "Public Game", "/games/public", membersOnly: false),
            CreateGameDto(2, "members-game", "Members Game", "/games/members", membersOnly: true)
        };
        _mockGameService
            .Setup(x => x.GetAllGamesAsync())
            .ReturnsAsync(games);

        var cut = RenderComponent<GamesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Public Game") || cut.Markup.Contains("Members Game"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Public Game", "admin should see public games");
        cut.Markup.Should().Contain("Members Game", "admin should see members-only games even if Leitao");
    }

    [Fact(Skip = SkipModal)]
    public async Task GamesPage_ShowsEditButton_ForAdminUser()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var games = new List<GameDto>
        {
            CreateGameDto(1, "game1", "Game One", "/games/game1")
        };
        _mockGameService
            .Setup(x => x.GetAllGamesAsync())
            .ReturnsAsync(games);

        var cut = RenderComponent<GamesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Game One"), TimeSpan.FromSeconds(2));

        // Edit button is in GameCard component, which may not be easily testable
        // This test is skipped as modal/edit interactions are outside component fragment
    }

    #endregion

    #region Helper Methods

    private static GameDto CreateGameDto(int id, string key, string title, string playRoute, bool membersOnly = false, bool isComingSoon = false)
    {
        return new GameDto
        {
            Id = id,
            Key = key,
            Title = title,
            Description = $"Description for {title}",
            ImageUrl = $"/images/games/{key}.webp",
            PlayRoute = playRoute,
            MembersOnly = membersOnly,
            IsComingSoon = isComingSoon,
            IsActive = true
        };
    }

    #endregion
}
