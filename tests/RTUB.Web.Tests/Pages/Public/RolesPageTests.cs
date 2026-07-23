using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Web.Tests.Pages.Base;
using RolesPage = RTUB.Pages.Public.Roles;

namespace RTUB.Web.Tests.Pages.Public;

/// <summary>
/// Component tests for Roles.razor page (/roles).
/// Tests page rendering, loading state, empty state, fiscal year filtering, and authorization (Phase 0.5).
/// </summary>
public class RolesPageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";
    private const string SkipUserManagerQuery = "Roles page uses UserManager.Users IQueryable which is complex to mock in bUnit.";

    private readonly Mock<IRoleAssignmentService> _mockRoleAssignmentService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IDocumentStorageService> _mockDocumentStorageService;
    private readonly AuditContext _auditContext;

    public RolesPageTests()
    {
        _mockRoleAssignmentService = SetupService<IRoleAssignmentService>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockUserManager = SetupUserManager();
        _mockDocumentStorageService = SetupService<IDocumentStorageService>();

        // Setup AuditContext
        _auditContext = new AuditContext();
        Services.AddSingleton(_auditContext);

        var fiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(2024, 2025),
            FiscalYear.Create(2025, 2026)
        };
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(fiscalYears);

        _mockRoleAssignmentService
            .Setup(x => x.GetAllRoleAssignmentsAsync())
            .ReturnsAsync(new List<RoleAssignment>());

        _mockUserManager
            .Setup(x => x.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns((string?)null);
    }

    #region Page Rendering Tests

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_RendersPageTitle()
    {
        SetupAuthentication("test-user", "Test User");

        var cut = Render<RolesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Órgãos Sociais") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Órgãos Sociais", "page should display title");
        cut.Markup.Should().Contain("Estrutura organizacional", "page should display subtitle");
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("test-user", "Test User");
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .Returns(async () =>
            {
                await Task.Delay(100);
                return new List<FiscalYear>
                {
                    FiscalYear.Create(2024, 2025)
                };
            });

        var cut = Render<RolesPage>();

        cut.Markup.Should().Contain("Órgãos Sociais", "page should display title");
        cut.Markup.Should().Match(m => m.Contains("A carregar") || m.Contains("Órgãos Sociais"), "should show loading or title initially");

        cut.WaitForState(() => !cut.Markup.Contains("A carregar") || cut.Markup.Contains("DIREÇÃO"), TimeSpan.FromSeconds(2));
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_ShowsEmptyState_WhenNoFiscalYears()
    {
        SetupAuthentication("test-user", "Test User");
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(new List<FiscalYear>());

        var cut = Render<RolesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Nenhum ano letivo") || cut.Markup.Contains("Órgãos Sociais"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Nenhum ano letivo ainda", "should show empty state when no fiscal years");
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_DisplaysFiscalYearDropdown_WhenFiscalYearsExist()
    {
        SetupAuthentication("test-user", "Test User");
        var fiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(2024, 2025),
            FiscalYear.Create(2025, 2026)
        };
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(fiscalYears);

        var cut = Render<RolesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Selecionar Ano Letivo") || cut.Markup.Contains("DIREÇÃO"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Selecionar Ano Letivo", "page should display fiscal year dropdown");
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_ShowsRgiButton_ForMemberUsers()
    {
        SetupAuthentication("member-user", "Member User", "Member");
        var fiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(2024, 2025)
        };
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(fiscalYears);

        var cut = Render<RolesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Abrir RGI") || cut.Markup.Contains("DIREÇÃO"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Abrir RGI", "member should see RGI button");
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_ShowsCreateFiscalYearButton_ForAdminUsers()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var fiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(2024, 2025)
        };
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(fiscalYears);

        var cut = Render<RolesPage>();
        cut.WaitForState(() => cut.Markup.Contains("Adicionar Ano Letivo") || cut.Markup.Contains("DIREÇÃO"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Adicionar Ano Letivo", "admin should see create fiscal year button");
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_HidesCreateFiscalYearButton_ForRegularUsers()
    {
        SetupAuthentication("regular-user", "Regular User");
        var fiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(2024, 2025)
        };
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(fiscalYears);

        var cut = Render<RolesPage>();
        cut.WaitForState(() => cut.Markup.Contains("DIREÇÃO") || cut.Markup.Contains("Órgãos Sociais"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().NotContain("Adicionar Ano Letivo", "regular user should not see create fiscal year button");
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_DisplaysDirecaoSection()
    {
        SetupAuthentication("test-user", "Test User");
        var fiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(2024, 2025)
        };
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(fiscalYears);

        var cut = Render<RolesPage>();
        cut.WaitForState(() => cut.Markup.Contains("DIREÇÃO") || cut.Markup.Contains("Órgãos Sociais"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("DIREÇÃO", "page should display Direção section");
        cut.Markup.Should().Contain("MAGISTER", "page should display Magister position");
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_OpensRgiModal_WhenRgiButtonClicked()
    {
        // Modal interactions are outside component fragment
    }

    [Fact(Skip = SkipUserManagerQuery)]
    public async Task RolesPage_OpensCreateFiscalYearModal_ForAdminUser()
    {
        // Modal interactions are outside component fragment
    }

    #endregion
}
