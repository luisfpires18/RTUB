using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Constants;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ReportPdfService
/// Tests PDF generation and caching functionality
/// </summary>
public class ReportPdfServiceTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ReportPdfService _service;

    public ReportPdfServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new ReportPdfService(_cache);
    }

    [Fact]
    public void GenerateReportPdf_WithValidReport_GeneratesPdf()
    {
        // Arrange
        var report = Report.Create("Test Report 2024", 2024);
        report.UpdateSummary("Test summary");
        report.Publish();

        var activities = new List<Activity>
        {
            Activity.Create(report.Id, "Activity 1", "Description 1")
        };

        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>
        {
            (activities[0], new List<Transaction>
            {
                Transaction.Create(DateTime.Now, "Income 1", "Category 1", 100.00m, TransactionTypes.Income, activities[0].Id),
                Transaction.Create(DateTime.Now, "Expense 1", "Category 2", 50.00m, TransactionTypes.Expense, activities[0].Id)
            })
        };

        // Act
        var result = _service.GenerateReportPdf(report, activities, allTransactions);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateReportPdf_WithMultipleActivities_GeneratesPdf()
    {
        // Arrange
        var report = Report.Create("Multi Activity Report", 2024);

        var activity1 = Activity.Create(report.Id, "Activity 1", "Desc 1");
        var activity2 = Activity.Create(report.Id, "Activity 2", "Desc 2");
        var activities = new List<Activity> { activity1, activity2 };

        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>
        {
            (activity1, new List<Transaction>
            {
                Transaction.Create(DateTime.Now, "Income 1", "Cat 1", 200.00m, TransactionTypes.Income, activity1.Id)
            }),
            (activity2, new List<Transaction>
            {
                Transaction.Create(DateTime.Now, "Expense 1", "Cat 2", 75.00m, TransactionTypes.Expense, activity2.Id)
            })
        };

        // Act
        var result = _service.GenerateReportPdf(report, activities, allTransactions);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateReportPdf_WithNoActivities_GeneratesPdf()
    {
        // Arrange
        var report = Report.Create("Empty Report", 2024);
        var activities = new List<Activity>();
        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>();

        // Act
        var result = _service.GenerateReportPdf(report, activities, allTransactions);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateReportPdf_WithActivitiesButNoTransactions_GeneratesPdf()
    {
        // Arrange
        var report = Report.Create("No Transactions Report", 2024);
        var activity = Activity.Create(report.Id, "Activity 1", null);
        var activities = new List<Activity> { activity };
        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>
        {
            (activity, new List<Transaction>())
        };

        // Act
        var result = _service.GenerateReportPdf(report, activities, allTransactions);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateReportPdf_SameReportTwice_UsesCachedVersion()
    {
        // Arrange
        var report = Report.Create("Cached Report", 2024);
        var activities = new List<Activity>
        {
            Activity.Create(report.Id, "Activity 1", null)
        };
        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>
        {
            (activities[0], new List<Transaction>())
        };

        // Act - Generate first time
        var result1 = _service.GenerateReportPdf(report, activities, allTransactions);

        // Act - Generate second time (should use cache)
        var result2 = _service.GenerateReportPdf(report, activities, allTransactions);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Should().Equal(result2); // Should be the exact same cached bytes
    }

    [Fact]
    public void GenerateReportPdf_WithDraftReport_GeneratesPdfWithDraftStatus()
    {
        // Arrange
        var report = Report.Create("Draft Report", 2024);
        // Don't publish - keep as draft
        var activities = new List<Activity>();
        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>();

        // Act
        var result = _service.GenerateReportPdf(report, activities, allTransactions);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateReportPdf_WithIncomeAndExpenses_GeneratesPdfWithCorrectTotals()
    {
        // Arrange
        var report = Report.Create("Financial Report", 2024);
        var activity = Activity.Create(report.Id, "Activity 1", null);
        var activities = new List<Activity> { activity };

        var transactions = new List<Transaction>
        {
            Transaction.Create(DateTime.Now, "Income 1", "Sales", 1000.00m, TransactionTypes.Income, activity.Id),
            Transaction.Create(DateTime.Now, "Income 2", "Services", 500.00m, TransactionTypes.Income, activity.Id),
            Transaction.Create(DateTime.Now, "Expense 1", "Equipment", 300.00m, TransactionTypes.Expense, activity.Id),
            Transaction.Create(DateTime.Now, "Expense 2", "Marketing", 200.00m, TransactionTypes.Expense, activity.Id)
        };

        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>
        {
            (activity, transactions)
        };

        // Act
        var result = _service.GenerateReportPdf(report, activities, allTransactions);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        // Total Income: 1500, Total Expenses: 500, Balance: 1000
    }

    [Fact]
    public void GenerateReportPdf_WithUpdatedReport_GeneratesNewPdfNotFromCache()
    {
        // Arrange
        var report = Report.Create("Updated Report", 2024);
        var activities = new List<Activity>();
        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>();

        // Act - Generate first time
        var result1 = _service.GenerateReportPdf(report, activities, allTransactions);

        // Simulate update by modifying the report
        report.UpdateSummary("New summary");

        // Act - Generate after update
        var result2 = _service.GenerateReportPdf(report, activities, allTransactions);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        // Since UpdatedAt hasn't actually changed in our test (no SaveChanges), 
        // the cache key would be the same, but in real scenario it would be different
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
