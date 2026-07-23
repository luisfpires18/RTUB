using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Web.Tests.Pages.Base;
using ReportEntity = RTUB.Core.Entities.Report;
using ReportPage = RTUB.Pages.Member.Report;

namespace RTUB.Web.Tests.Pages.Management;

/// <summary>
/// Component tests for Report.razor page (/finance/report/{ReportId}).
/// Tests page rendering, loading state, and authorization per Phase 0.5.
/// </summary>
public class ReportPageTests : PageTestBase
{
    private readonly Mock<IReportService> _mockReportService;
    private readonly Mock<IActivityService> _mockActivityService;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IMemberDebtService> _mockMemberDebtService;
    private readonly Mock<IFiscalYearService> _mockFiscalYearService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;

    public ReportPageTests()
    {
        _mockReportService = SetupService<IReportService>();
        _mockActivityService = SetupService<IActivityService>();
        _mockTransactionService = SetupService<ITransactionService>();
        _mockMemberDebtService = SetupService<IMemberDebtService>();
        _mockFiscalYearService = SetupService<IFiscalYearService>();
        _mockUserManager = SetupUserManager();

        _mockActivityService
            .Setup(x => x.GetActivitiesByReportIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Activity>());

        _mockTransactionService
            .Setup(x => x.GetTransactionsByActivityIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Transaction>());

        _mockMemberDebtService
            .Setup(x => x.GetDebtsForFiscalYearAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<MemberDebt>());

        _mockFiscalYearService
            .Setup(x => x.GetFiscalYearByStartYearAsync(It.IsAny<int>()))
            .ReturnsAsync((FiscalYear?)null);

        _mockUserManager
            .Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);
    }

    #region Page Rendering Tests

    [Fact]
    public async Task ReportPage_ShowsLoadingState_Initially()
    {
        SetupAuthentication("test-user", "Test User");
        var report = CreateTestReport(1, "Test Report 2024", 2024);
        var tcs = new TaskCompletionSource<ReportEntity?>();
        _mockReportService
            .Setup(x => x.GetReportByIdAsync(1))
            .Returns(tcs.Task);

        var cut = Render<ReportPage>(p => p.Add(p => p.ReportId, 1));

        // Assert - Check loading state before async operations complete
        cut.Markup.Should().Contain("A carregar", "page should show loading initially");

        // Complete the delayed task to allow test cleanup
        tcs.SetResult(report);
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ReportPage_RendersReportTitle_WhenReportLoaded()
    {
        SetupAuthentication("test-user", "Test User");
        var report = CreateTestReport(1, "Relatório 2023-2024", 2023);
        _mockReportService
            .Setup(x => x.GetReportByIdAsync(1))
            .ReturnsAsync(report);

        var cut = Render<ReportPage>(p => p.Add(p => p.ReportId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Relatório 2023-2024") || !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Relatório 2023-2024", "page should display report title");
        cut.Markup.Should().Contain("Ano Letivo", "page should display year label");
    }

    [Fact]
    public async Task ReportPage_ShowsSummaryCards_WhenReportLoaded()
    {
        SetupAuthentication("test-user", "Test User");
        var report = CreateTestReport(1, "Test Report", 2024);
        _mockReportService
            .Setup(x => x.GetReportByIdAsync(1))
            .ReturnsAsync(report);

        var cut = Render<ReportPage>(p => p.Add(p => p.ReportId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Dinheiro Total") || !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Dinheiro Total", "page should display summary card");
    }

    [Fact]
    public async Task ReportPage_ShowsPublishedWarning_WhenReportIsPublished()
    {
        SetupAuthentication("test-user", "Test User");
        var report = CreateTestReport(1, "Published Report", 2024);
        report.IsPublished = true;
        _mockReportService
            .Setup(x => x.GetReportByIdAsync(1))
            .ReturnsAsync(report);

        var cut = Render<ReportPage>(p => p.Add(p => p.ReportId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Relatório Publicado") || cut.Markup.Contains("Published Report"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Relatório Publicado", "published report should show read-only warning");
        cut.Markup.Should().Contain("Somente Leitura", "published report should show read-only message");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task ReportPage_ShowsTransactionHistoryButton_ForAdminUser()
    {
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var report = CreateTestReport(1, "Test Report", 2024);
        _mockReportService
            .Setup(x => x.GetReportByIdAsync(1))
            .ReturnsAsync(report);

        var cut = Render<ReportPage>(p => p.Add(p => p.ReportId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Histórico") || cut.Markup.Contains("Ver Calotes") || !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Histórico de Transações", "admin should see transaction history button");
    }

    [Fact]
    public async Task ReportPage_ShowsVerCalotesButton_ForAnyAuthenticatedUser()
    {
        SetupAuthentication("test-user", "Test User");
        var report = CreateTestReport(1, "Test Report", 2024);
        _mockReportService
            .Setup(x => x.GetReportByIdAsync(1))
            .ReturnsAsync(report);

        var cut = Render<ReportPage>(p => p.Add(p => p.ReportId, 1));
        cut.WaitForState(() => cut.Markup.Contains("Ver Calotes") || !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        cut.Markup.Should().Contain("Ver Calotes", "authenticated user should see Calotes button");
    }

    #endregion

    #region Helper Methods

    private static ReportEntity CreateTestReport(int id, string title, int year)
    {
        return new ReportEntity
        {
            Id = id,
            Title = title,
            Year = year,
            Summary = null,
            IsPublished = false
        };
    }

    #endregion
}
