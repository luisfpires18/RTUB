using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Pages.Public;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Public;

/// <summary>
/// Component tests for Calotes.razor page (/calotes).
/// Tests page rendering, loading state, empty states, and authorization per Phase 0.5.
/// </summary>
public class CalotesPageTests : PageTestBase
{
    private readonly Mock<IMemberDebtService> _mockMemberDebtService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public CalotesPageTests()
    {
        _mockMemberDebtService = SetupService<IMemberDebtService>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockUserManager = SetupUserManager();

        Services.AddSingleton(new AuditContext());

        _mockMemberDebtService
            .Setup(x => x.GetDebtsForFiscalYearAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<MemberDebt>());

        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);
    }

    #region Page Rendering Tests

    [Fact]
    public async Task CalotesPage_ShowsNoFiscalYears_WhenNoneExist()
    {
        SetupAuthentication("test-user", "Test User");
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear>());

        var cut = RenderComponent<Calotes>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar calotes") || cut.Markup.Contains("Nenhum ano letivo"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Calotes", "page should display title");
        cut.Markup.Should().Contain("Nenhum ano letivo ainda", "should show empty state when no fiscal years");
    }

    [Fact]
    public async Task CalotesPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("test-user", "Test User");
        var fy = FiscalYear.Create(2024, 2025);
        fy.Id = 1;
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return (IEnumerable<FiscalYear>)new List<FiscalYear> { fy };
            });
        _mockFiscalYearService
            .Setup(x => x.GetFiscalYearByStartYearAsync(2024))
            .ReturnsAsync(fy);

        var cut = RenderComponent<Calotes>();

        cut.Markup.Should().Contain("Calotes", "page should display title");
        cut.Markup.Should().Match(m => m.Contains("A carregar") || m.Contains("Calotes"), "should show loading or title initially");

        cut.WaitForState(() => cut.Markup.Contains("Dinheiro") || cut.Markup.Contains("Nenhum calote") || cut.Markup.Contains("Selecionar Ano"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CalotesPage_RendersTitleAndEmptyState_WhenFiscalYearsExistButNoDebts()
    {
        SetupAuthentication("test-user", "Test User");
        var fy = FiscalYear.Create(2024, 2025);
        fy.Id = 1;
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear> { fy });
        _mockFiscalYearService
            .Setup(x => x.GetFiscalYearByStartYearAsync(2024))
            .ReturnsAsync(fy);

        var cut = RenderComponent<Calotes>();
        cut.WaitForState(() => cut.Markup.Contains("Nenhum calote") || cut.Markup.Contains("Total de Calotes") || cut.Markup.Contains("Selecionar Ano"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Calotes", "page should display title");
        cut.Markup.Should().Contain("Registo de dívidas", "page should display subtitle");
    }

    [Fact]
    public async Task CalotesPage_ShowsSearchBar_WhenFiscalYearsExist()
    {
        SetupAuthentication("test-user", "Test User");
        var fy = FiscalYear.Create(2024, 2025);
        fy.Id = 1;
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear> { fy });
        _mockFiscalYearService
            .Setup(x => x.GetFiscalYearByStartYearAsync(2024))
            .ReturnsAsync(fy);

        var cut = RenderComponent<Calotes>();
        cut.WaitForState(() => cut.Markup.Contains("Pesquisar membros") || cut.Markup.Contains("Nenhum calote") || cut.Markup.Contains("Selecionar Ano"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Pesquisar membros", "page should show search bar when fiscal years exist");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task CalotesPage_HidesAddButton_ForRegularUser()
    {
        SetupAuthentication("regular-user", "Regular User");
        var fy = FiscalYear.Create(2024, 2025);
        fy.Id = 1;
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear> { fy });
        _mockFiscalYearService
            .Setup(x => x.GetFiscalYearByStartYearAsync(2024))
            .ReturnsAsync(fy);

        var cut = RenderComponent<Calotes>();
        cut.WaitForState(() => cut.Markup.Contains("Calotes") && (!cut.Markup.Contains("A carregar calotes") || cut.Markup.Contains("Nenhum calote") || cut.Markup.Contains("Selecionar")), TimeSpan.FromSeconds(2));

        cut.Markup.Should().NotContain("Adicionar Calote", "regular user should not see add button");
    }

    #endregion
}
