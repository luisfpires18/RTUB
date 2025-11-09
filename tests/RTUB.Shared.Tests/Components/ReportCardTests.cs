using AutoFixture;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RTUB.Core.Entities;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components;

/// <summary>
/// Tests for the ReportCard component to ensure report cards display correctly
/// </summary>
public class ReportCardTests : TestContext
{
    private readonly Fixture _fixture;

    public ReportCardTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void ReportCard_RendersReportTitle()
    {
        // Arrange
        var report = Report.Create("Financial Report 2023-2024", 2023);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Financial Report 2023-2024", "card should display report title");
    }

    [Fact]
    public void ReportCard_RendersFiscalYear()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("2023-2024", "card should display fiscal year");
        cut.Markup.Should().Contain("Ano Fiscal", "card should have fiscal year label");
    }

    [Fact]
    public void ReportCard_RendersPublishedBadge_WhenPublished()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.Publish();

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Publicado", "card should display published badge");
        cut.Markup.Should().Contain("bg-success", "published badge should be green");
    }

    [Fact]
    public void ReportCard_RendersDraftBadge_WhenNotPublished()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Rascunho", "card should display draft badge");
        cut.Markup.Should().Contain("bg-warning", "draft badge should be yellow");
    }

    [Fact]
    public void ReportCard_RendersCurrentYearBadge_WhenReportIsCurrentFiscalYear()
    {
        // Arrange - Create report for current fiscal year
        var today = DateTime.Today;
        var currentFiscalYearStartYear = today.Month >= 9 ? today.Year : today.Year - 1;
        var report = Report.Create("Current Year Report", currentFiscalYearStartYear);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Atual Ano", "card should display current year badge");
        cut.Markup.Should().Contain("badge-position", "current year badge should be purple");
    }

    [Fact]
    public void ReportCard_DoesNotRenderCurrentYearBadge_WhenReportIsNotCurrentFiscalYear()
    {
        // Arrange - Create report for past year
        var today = DateTime.Today;
        var currentFiscalYearStartYear = today.Month >= 9 ? today.Year : today.Year - 1;
        var pastYear = currentFiscalYearStartYear - 2;
        var report = Report.Create("Past Year Report", pastYear);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().NotContain("Atual Ano", "card should not display current year badge for past reports");
    }

    [Fact]
    public void ReportCard_RendersFinancialStats()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.UpdateFinancials(5000m, 3000m);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("Receitas", "card should display income label");
        cut.Markup.Should().Contain("Despesas", "card should display expenses label");
        cut.Markup.Should().Contain("Saldo", "card should display balance label");
        cut.Markup.Should().Contain("€5,000.00", "card should display income value");
        cut.Markup.Should().Contain("€3,000.00", "card should display expenses value");
        cut.Markup.Should().Contain("€2,000.00", "card should display balance value");
    }

    [Fact]
    public void ReportCard_ShowsUpArrow_WhenBalanceIsPositive()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.UpdateFinancials(5000m, 3000m);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("bi-caret-up-fill", "card should show up arrow for positive balance");
    }

    [Fact]
    public void ReportCard_ShowsDownArrow_WhenBalanceIsNegative()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.UpdateFinancials(2000m, 3000m);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("bi-caret-down-fill", "card should show down arrow for negative balance");
    }

    [Fact]
    public void ReportCard_ShowsDownloadButton_WhenUserIsAdmin()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, true)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("bi-download", "card should show download button for admins");
    }

    [Fact]
    public void ReportCard_HidesDownloadButton_WhenUserIsNotAdmin()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().NotContain("bi-download", "card should hide download button for non-admins");
    }

    [Fact]
    public void ReportCard_ShowsPublishAndDeleteButtons_WhenUserIsOwnerAndReportIsNotPublished()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, true));

        // Assert
        cut.Markup.Should().Contain("bi-check-circle", "card should show publish button for owners");
        cut.Markup.Should().Contain("bi-trash", "card should show delete button for owners");
    }

    [Fact]
    public void ReportCard_HidesPublishAndDeleteButtons_WhenReportIsPublished()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.Publish();

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, true));

        // Assert
        cut.Markup.Should().NotContain("bi-check-circle", "card should hide publish button for published reports");
        cut.Markup.Should().NotContain("bi-trash", "card should hide delete button for published reports");
    }

    [Fact]
    public void ReportCard_HasAccessibilityLabels_ForStatTiles()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.UpdateFinancials(5000m, 3000m);

        // Act
        var cut = RenderComponent<ReportCard>(parameters => parameters
            .Add(p => p.Report, report)
            .Add(p => p.IsAdmin, false)
            .Add(p => p.IsOwner, false));

        // Assert
        cut.Markup.Should().Contain("aria-label=\"Receitas: €5,000.00\"", 
            "income tile should have aria-label");
        cut.Markup.Should().Contain("aria-label=\"Despesas: €3,000.00\"", 
            "expenses tile should have aria-label");
        cut.Markup.Should().Contain("aria-label=\"Saldo: €2,000.00\"", 
            "balance tile should have aria-label");
    }
}
