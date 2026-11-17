// TransactionService: history changes.
using FluentAssertions;
using Moq;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public partial class TransactionServiceTests
{
    #region GetTransactionHistoryForReportAsync Tests

    [Fact]
    public async Task GetTransactionHistoryForReportAsync_ExtractActivityIdFromChanges_WithDirectValue_ReturnsId()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity = Activity.Create(report.Id, "Test Activity", DateTime.Now);
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        var transaction = Transaction.Create(
            DateTime.Now, "Test Transaction", "Category", 100m, "Income", activity.Id);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Create audit log with direct ActivityId value (Created/Deleted format)
        var auditLog = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = null, // Simulate old log with null EntityId
            Action = "Created",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", activity.Id }, // Direct value
                { "Description", "Test Transaction" }
            })
        };
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockActivityService.Setup(s => s.GetActivitiesByReportIdAsync(report.Id))
            .ReturnsAsync(new[] { activity });
        _mockAuditLogService.Setup(s => s.GetAllForExportAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(et => et == "Transaction"),
            It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new[] { auditLog });

        // Act
        var (entries, totalCount) = await _service.GetTransactionHistoryForReportAsync(report.Id);

        // Assert - Should extract ActivityId from Changes JSON
        entries.Should().HaveCount(1);
        entries.First().ActivityName.Should().Be("Test Activity");
    }

    [Fact]
    public async Task GetTransactionHistoryForReportAsync_ExtractActivityIdFromChanges_WithOldNewStructure_ReturnsNewId()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity1 = Activity.Create(report.Id, "Activity 1", DateTime.Now);
        var activity2 = Activity.Create(report.Id, "Activity 2", DateTime.Now);
        _context.Activities.AddRange(activity1, activity2);
        await _context.SaveChangesAsync();

        var transaction = Transaction.Create(
            DateTime.Now, "Test Transaction", "Category", 100m, "Income", activity2.Id);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Create audit log with Old/New structure (Modified format)
        var auditLog = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = transaction.Id,
            Action = "Modified",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", new Dictionary<string, object> { { "Old", activity1.Id }, { "New", activity2.Id } } },
                { "Description", "Test Transaction" }
            })
        };
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockActivityService.Setup(s => s.GetActivitiesByReportIdAsync(report.Id))
            .ReturnsAsync(new[] { activity1, activity2 });
        _mockAuditLogService.Setup(s => s.GetAllForExportAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(et => et == "Transaction"),
            It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new[] { auditLog });

        // Act
        var (entries, totalCount) = await _service.GetTransactionHistoryForReportAsync(report.Id);

        // Assert - Should extract New ActivityId from Old/New structure
        entries.Should().HaveCount(1);
        entries.First().ActivityName.Should().Be("Activity 2"); // Should use New value
    }

    [Fact]
    public async Task GetTransactionHistoryForReportAsync_ExtractDescriptionFromChanges_WithDirectValue_ReturnsDescription()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity = Activity.Create(report.Id, "Test Activity", DateTime.Now);
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Create audit log with direct Description value
        var auditLog = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = null, // Simulate deleted transaction
            Action = "Deleted",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", activity.Id },
                { "Description", "Deleted Transaction" } // Direct value
            })
        };
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockActivityService.Setup(s => s.GetActivitiesByReportIdAsync(report.Id))
            .ReturnsAsync(new[] { activity });
        _mockAuditLogService.Setup(s => s.GetAllForExportAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(et => et == "Transaction"),
            It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new[] { auditLog });

        // Act
        var (entries, totalCount) = await _service.GetTransactionHistoryForReportAsync(report.Id);

        // Assert - Should extract Description from Changes JSON
        entries.Should().HaveCount(1);
        entries.First().TransactionDescription.Should().Be("Deleted Transaction");
    }

    [Fact]
    public async Task GetTransactionHistoryForReportAsync_ExtractDescriptionFromChanges_WithOldNewStructure_ReturnsNewDescription()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity = Activity.Create(report.Id, "Test Activity", DateTime.Now);
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        var transaction = Transaction.Create(
            DateTime.Now, "Updated Description", "Category", 100m, "Income", activity.Id);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Create audit log with Old/New structure for Description
        var auditLog = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = transaction.Id,
            Action = "Modified",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", activity.Id },
                { "Description", new Dictionary<string, object> { { "Old", "Original Description" }, { "New", "Updated Description" } } }
            })
        };
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockActivityService.Setup(s => s.GetActivitiesByReportIdAsync(report.Id))
            .ReturnsAsync(new[] { activity });
        _mockAuditLogService.Setup(s => s.GetAllForExportAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(et => et == "Transaction"),
            It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new[] { auditLog });

        // Act
        var (entries, totalCount) = await _service.GetTransactionHistoryForReportAsync(report.Id);

        // Assert - Should extract New Description from Old/New structure
        entries.Should().HaveCount(1);
        entries.First().TransactionDescription.Should().Be("Updated Description");
    }

    #endregion
}
