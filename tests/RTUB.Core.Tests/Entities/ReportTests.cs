using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Report entity
/// Tests financial calculations and business logic
/// </summary>
public partial class ReportTests
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
    public void ComputedFinancials_CalculatesFinalBalanceFromActivities()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var testDate = new DateTime(2023, 6, 15);

        // Add activities with transactions
        var activity1 = Activity.Create(1, "Activity 1", testDate);
        activity1.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Income", "Cat", 3000m, "Income", 1));
        activity1.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Expense", "Cat", 1000m, "Expense", 1));

        var activity2 = Activity.Create(1, "Activity 2", testDate.AddMonths(1));
        activity2.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Income", "Cat", 2000m, "Income", 1));
        activity2.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Expense", "Cat", 2000m, "Expense", 1));

        report.Activities.Add(activity1);
        report.Activities.Add(activity2);

        // Assert - computed properties calculate from transactions
        report.TotalIncome.Should().Be(5000m);
        report.TotalExpenses.Should().Be(3000m);
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
}
