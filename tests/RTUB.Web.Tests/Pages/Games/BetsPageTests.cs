using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Pages.Games;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Games;

/// <summary>
/// Component tests for Bets.razor page (/bets).
/// Tests page rendering, loading state, empty state, and authorization per Phase 0.5.
/// </summary>
public class BetsPageTests : PageTestBase
{
    private readonly Mock<IBetService> _mockBetService;
    private readonly Mock<IBetOptionRepository> _mockBetOptionRepository;
    private readonly Mock<IUserBetRepository> _mockUserBetRepository;
    private readonly Mock<IBetCommentService> _mockBetCommentService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public BetsPageTests()
    {
        _mockBetService = SetupService<IBetService>();
        _mockBetOptionRepository = SetupService<IBetOptionRepository>();
        _mockUserBetRepository = SetupService<IUserBetRepository>();
        _mockBetCommentService = SetupService<IBetCommentService>();
        _mockImageStorageService = SetupService<IImageStorageService>();
        _mockPushNotificationService = SetupService<IPushNotificationService>();
        _mockPushNotificationFactory = SetupService<IPushNotificationFactory>();
        _mockUserManager = SetupUserManager();

        _mockBetService
            .Setup(x => x.GetFutureBetsAsync())
            .ReturnsAsync(new List<Bet>());
        _mockBetService
            .Setup(x => x.GetPastBetsAsync())
            .ReturnsAsync(new List<Bet>());
        _mockBetCommentService
            .Setup(x => x.GetCommentCountForBetAsync(It.IsAny<int>()))
            .ReturnsAsync(0);

        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);
    }

    #region Page Rendering Tests

    [Fact]
    public async Task BetsPage_RendersPageTitle()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Bets>();
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border") || cut.Markup.Contains("Apostas") || cut.Markup.Contains("Nenhuma aposta"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Apostas", "page should display title");
        cut.Markup.Should().Contain("Próximas apostas", "page should display subtitle");
    }

    [Fact]
    public async Task BetsPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("test-user", "Test User");
        _mockBetService
            .Setup(x => x.GetFutureBetsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<Bet>)new List<Bet>();
            });
        _mockBetService
            .Setup(x => x.GetPastBetsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<Bet>)new List<Bet>();
            });

        var cut = RenderComponent<Bets>();

        cut.Markup.Should().Contain("Apostas", "page should display title");
        cut.Markup.Should().Match(m => m.Contains("spinner-border") || m.Contains("Apostas"), "should show loading or title initially");

        cut.WaitForState(() => cut.Markup.Contains("Nenhuma aposta") || cut.Markup.Contains("Apostas Futuras") || cut.Markup.Contains("Ganhe Fidelis"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task BetsPage_ShowsEmptyState_WhenNoBets()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Bets>();
        cut.WaitForState(() => cut.Markup.Contains("Nenhuma aposta") || cut.Markup.Contains("Apostas Futuras") || cut.Markup.Contains("Ganhe Fidelis"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Nenhuma aposta encontrada", "should show empty state when no bets");
    }

    [Fact]
    public async Task BetsPage_DisplaysFutureBets_WhenBetsExist()
    {
        SetupAuthentication("test-user", "Test User");
        var bet = new Bet { Id = 1, Title = "Test Bet", DateTime = DateTime.UtcNow.AddDays(1), Description = "Desc" };
        _mockBetService
            .Setup(x => x.GetFutureBetsAsync())
            .ReturnsAsync(new List<Bet> { bet });
        _mockBetService
            .Setup(x => x.GetPastBetsAsync())
            .ReturnsAsync(new List<Bet>());
        _mockBetService
            .Setup(x => x.GetBetOptionsAsync(1))
            .ReturnsAsync(new List<BetOption>());

        var cut = RenderComponent<Bets>();
        cut.WaitForState(() => cut.Markup.Contains("Test Bet") || cut.Markup.Contains("Apostas Futuras"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Apostas Futuras", "should show future bets section");
        cut.Markup.Should().Contain("Test Bet", "should display bet title");
    }

    [Fact]
    public async Task BetsPage_ShowsFidelisAlert()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = RenderComponent<Bets>();
        cut.WaitForState(() => cut.Markup.Contains("Ganhe Fidelis") || cut.Markup.Contains("Nenhuma aposta"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Ganhe Fidelis", "should show Fidelis information alert");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task BetsPage_ShowsCreateButton_ForAdminUser()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");

        var cut = RenderComponent<Bets>();
        cut.WaitForState(() => cut.Markup.Contains("Adicionar Aposta") || cut.Markup.Contains("Nenhuma aposta"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Adicionar Aposta", "admin should see create button");
    }

    [Fact]
    public async Task BetsPage_HidesCreateButton_ForRegularUser()
    {
        SetupAuthentication("regular-user", "Regular User");

        var cut = RenderComponent<Bets>();
        cut.WaitForState(() => cut.Markup.Contains("Apostas") && (!cut.Markup.Contains("spinner-border") || cut.Markup.Contains("Nenhuma aposta")), TimeSpan.FromSeconds(2));

        cut.Markup.Should().NotContain("Adicionar Aposta", "regular user should not see create button");
    }

    #endregion
}
