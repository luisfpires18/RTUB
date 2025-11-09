using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Report entity
/// Tests financial calculations and business logic
/// </summary>
public class ReportTests
{
    [Fact]
    public void Create_WithValidData_CreatesReport()
    {
        // Arrange
        var title = "Financial Report 2023-2024";
        var year = 2023;
        var summary = "Annual financial summary";

        // Act
        var result = Report.Create(title, year, summary);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.Year.Should().Be(year);
        result.Summary.Should().Be(summary);
        result.IsPublished.Should().BeFalse();
        result.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var title = "";
        var year = 2023;

        // Act & Assert
        var act = () => Report.Create(title, year);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*título do relatório*");
    }

    [Fact]
    public void UpdateFinancials_CalculatesFinalBalance()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var totalIncome = 5000m;
        var totalExpenses = 3000m;

        // Act
        report.UpdateFinancials(totalIncome, totalExpenses);

        // Assert
        report.TotalIncome.Should().Be(totalIncome);
        report.TotalExpenses.Should().Be(totalExpenses);
        report.FinalBalance.Should().Be(2000m);
    }

    [Fact]
    public void UpdateFinancials_WithNegativeBalance_CalculatesCorrectly()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var totalIncome = 2000m;
        var totalExpenses = 3000m;

        // Act
        report.UpdateFinancials(totalIncome, totalExpenses);

        // Assert
        report.FinalBalance.Should().Be(-1000m);
    }

    [Fact]
    public void UpdateSummary_UpdatesCorrectly()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var newSummary = "Updated summary";

        // Act
        report.UpdateSummary(newSummary);

        // Assert
        report.Summary.Should().Be(newSummary);
    }

    [Fact]
    public void SetPdfData_WithValidData_SetsCorrectly()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var pdfData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        report.SetPdfData(pdfData);

        // Assert
        report.PdfData.Should().BeEquivalentTo(pdfData);
    }

    [Fact]
    public void SetPdfData_WithNullData_ThrowsArgumentNullException()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);

        // Act & Assert
        var act = () => report.SetPdfData(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Publish_WhenNotPublished_PublishesReport()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);

        // Act
        report.Publish();

        // Assert
        report.IsPublished.Should().BeTrue();
        report.PublishedAt.Should().NotBeNull();
        report.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_ThrowsInvalidOperationException()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.Publish();

        // Act & Assert
        var act = () => report.Publish();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already published*");
    }

    [Fact]
    public void Unpublish_WhenPublished_UnpublishesReport()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.Publish();

        // Act
        report.Unpublish();

        // Assert
        report.IsPublished.Should().BeFalse();
        report.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void IsCurrentFiscalYear_WhenReportIsCurrentYear_ReturnsTrue()
    {
        // Arrange - Get current fiscal year start year
        var today = DateTime.Today;
        var currentFiscalYearStartYear = today.Month >= 9 ? today.Year : today.Year - 1;
        var report = Report.Create("Current Year Report", currentFiscalYearStartYear);

        // Act
        var result = report.IsCurrentFiscalYear();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCurrentFiscalYear_WhenReportIsNotCurrentYear_ReturnsFalse()
    {
        // Arrange - Get a year that's definitely not current
        var today = DateTime.Today;
        var currentFiscalYearStartYear = today.Month >= 9 ? today.Year : today.Year - 1;
        var pastYear = currentFiscalYearStartYear - 2;
        var report = Report.Create("Past Year Report", pastYear);

        // Act
        var result = report.IsCurrentFiscalYear();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentFiscalYear_WhenReportIsLastYear_ReturnsFalse()
    {
        // Arrange
        var today = DateTime.Today;
        var currentFiscalYearStartYear = today.Month >= 9 ? today.Year : today.Year - 1;
        var lastYear = currentFiscalYearStartYear - 1;
        var report = Report.Create("Last Year Report", lastYear);

        // Act
        var result = report.IsCurrentFiscalYear();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentFiscalYear_WhenReportIsNextYear_ReturnsFalse()
    {
        // Arrange
        var today = DateTime.Today;
        var currentFiscalYearStartYear = today.Month >= 9 ? today.Year : today.Year - 1;
        var nextYear = currentFiscalYearStartYear + 1;
        var report = Report.Create("Next Year Report", nextYear);

        // Act
        var result = report.IsCurrentFiscalYear();

        // Assert
        result.Should().BeFalse();
    }
}
