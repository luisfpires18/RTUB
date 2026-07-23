using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Pages.Management;
using RTUB.Web.Tests.Pages.Base;
using RTUB.Web.Tests.TestData;

namespace RTUB.Web.Tests.Pages.Management;

/// <summary>
/// Component tests for Requests.razor page (/requests).
/// Tests page rendering, loading state, empty state, pending/answered sections, and authorization (Phase 0.5).
/// </summary>
public class RequestsPageTests : PageTestBase
{
    private readonly Mock<IRequestService> _mockRequestService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public RequestsPageTests()
    {
        _mockRequestService = SetupService<IRequestService>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockUserManager = SetupUserManager();

        _mockRequestService
            .Setup(x => x.GetAllRequestsAsync())
            .ReturnsAsync(new List<Request>());

        var fiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(2025, 2026),
            FiscalYear.Create(2024, 2025)
        };
        _mockFiscalYearService
            .Setup(x => x.GetAllFiscalYearsAsync())
            .ReturnsAsync(fiscalYears);

        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        SetupAuthentication("test-user", "Test User", "Admin");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task RequestsPage_RendersPageTitle()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");

        var cut = Render<Requests>();
        cut.WaitForState(() => cut.Markup.Contains("Gestão de Pedidos") || cut.Markup.Contains("Pedidos Pendentes") || cut.Markup.Contains("Nenhum pedido"), TimeSpan.FromSeconds(3));

        cut.Markup.Should().Contain("Gestão de Pedidos", "page should display title");
        cut.Markup.Should().Contain("Gerir todos os pedidos", "page should display subtitle");
    }

    [Fact]
    public async Task RequestsPage_ShowsPendingAndAnsweredSections()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");

        var cut = Render<Requests>();
        cut.WaitForState(() => cut.Markup.Contains("Pedidos Pendentes") && cut.Markup.Contains("Pedidos Respondidos"), TimeSpan.FromSeconds(3));

        cut.Markup.Should().Contain("Pedidos Pendentes", "page should show pending section");
        cut.Markup.Should().Contain("Pedidos Respondidos", "page should show answered section");
    }

    [Fact]
    public async Task RequestsPage_ShowsEmptyStates_WhenNoRequests()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");

        var cut = Render<Requests>();
        cut.WaitForState(() => cut.Markup.Contains("Nenhum pedido pendente") || cut.Markup.Contains("Nenhum pedido respondido"), TimeSpan.FromSeconds(3));

        cut.Markup.Should().Contain("Nenhum pedido pendente encontrado", "should show empty state for pending");
        cut.Markup.Should().Contain("Nenhum pedido respondido encontrado", "should show empty state for answered");
    }

    [Fact]
    public async Task RequestsPage_DisplaysPendingRequests_WhenExist()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var pending = PageTestDataBuilders.CreateRequest(1, "Alice", "alice@test.com", "Atuacao", DateTime.UtcNow.AddDays(7), "Porto", RequestStatus.Pending);
        _mockRequestService
            .Setup(x => x.GetAllRequestsAsync())
            .ReturnsAsync(new List<Request> { pending });

        var cut = Render<Requests>();
        cut.WaitForState(() => cut.Markup.Contains("Alice") || cut.Markup.Contains("Atuacao"), TimeSpan.FromSeconds(3));

        cut.Markup.Should().Contain("Alice", "page should display pending request name");
    }

    [Fact]
    public async Task RequestsPage_DisplaysAnsweredRequests_WhenExist()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var answered = PageTestDataBuilders.CreateRequest(1, "Bob", "bob@test.com", "Concerto", DateTime.UtcNow.AddDays(14), "Lisboa", RequestStatus.Confirmed);
        _mockRequestService
            .Setup(x => x.GetAllRequestsAsync())
            .ReturnsAsync(new List<Request> { answered });

        var cut = Render<Requests>();
        cut.WaitForState(() => cut.Markup.Contains("Bob") || cut.Markup.Contains("Concerto"), TimeSpan.FromSeconds(3));

        cut.Markup.Should().Contain("Bob", "page should display answered request name");
    }

    #endregion
}
