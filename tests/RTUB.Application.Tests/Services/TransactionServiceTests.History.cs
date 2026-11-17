// TransactionService: history.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public partial class TransactionServiceTests
{
    #region GetTransactionHistoryForReportAsync Tests

    [Fact]
    public async Task GetTransactionHistoryForReportAsync_WithValidReport_ReturnsHistory()
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

        // Create audit log for transaction creation
        var auditLog = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = transaction.Id,
            Action = "Created",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", activity.Id },
                { "Description", "Test Transaction" }
            })
        };
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // Verify transaction is accessible via repository (same context used by service)
        var testRepo = new TransactionRepository(_fixture.CreateContextFactory());
        var testTransaction = await testRepo.GetByIdAsync(transaction.Id);
        testTransaction.Should().NotBeNull("transaction should be accessible via repository");
        testTransaction!.ActivityId.Should().Be(activity.Id, "transaction should have correct ActivityId");

        // Setup mocks
        // Note: GetAllForExportAsync is called with entityType: "Transaction" (named parameter, 3rd position)
        _mockActivityService.Setup(s => s.GetActivitiesByReportIdAsync(report.Id))
            .ReturnsAsync(new[] { activity });
        _mockAuditLogService.Setup(s => s.GetAllForExportAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(et => et == "Transaction"),
            It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new[] { auditLog });

        // Act
        var (entries, totalCount) = await _service.GetTransactionHistoryForReportAsync(report.Id);

        // Assert
        entries.Should().NotBeNull();
        // The transaction should be found via EntityId and matched to the activity
        totalCount.Should().Be(1, "transaction should be found and matched to report activity");
        var entry = entries.First();
        entry.TransactionId.Should().Be(transaction.Id);
        entry.ActivityName.Should().Be("Test Activity");
        entry.TransactionDescription.Should().Be("Test Transaction");
        entry.Action.Should().Be("Created");
    }

    [Fact]
    public async Task GetTransactionHistoryForReportAsync_FiltersByActivity()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity1 = Activity.Create(report.Id, "Activity 1", DateTime.Now);
        var activity2 = Activity.Create(report.Id, "Activity 2", DateTime.Now);
        _context.Activities.AddRange(activity1, activity2);
        await _context.SaveChangesAsync();

        var transaction1 = Transaction.Create(
            DateTime.Now, "Transaction 1", "Category", 100m, "Income", activity1.Id);
        var transaction2 = Transaction.Create(
            DateTime.Now, "Transaction 2", "Category", 200m, "Income", activity2.Id);
        var transaction3 = Transaction.Create(
            DateTime.Now, "Transaction 3", "Category", 300m, "Income", null); // No activity
        _context.Transactions.AddRange(transaction1, transaction2, transaction3);
        await _context.SaveChangesAsync();

        // Create audit logs
        var auditLog1 = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = transaction1.Id,
            Action = "Created",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", activity1.Id },
                { "Description", "Transaction 1" }
            })
        };
        var auditLog2 = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = transaction2.Id,
            Action = "Created",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", activity2.Id },
                { "Description", "Transaction 2" }
            })
        };
        var auditLog3 = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = transaction3.Id,
            Action = "Created",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "Description", "Transaction 3" }
            })
        };
        _context.AuditLogs.AddRange(auditLog1, auditLog2, auditLog3);
        await _context.SaveChangesAsync();

        // Setup mocks - only return activities for this report
        _mockActivityService.Setup(s => s.GetActivitiesByReportIdAsync(report.Id))
            .ReturnsAsync(new[] { activity1, activity2 });
        _mockAuditLogService.Setup(s => s.GetAllForExportAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(et => et == "Transaction"),
            It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new[] { auditLog1, auditLog2, auditLog3 });

        // Act
        var (entries, totalCount) = await _service.GetTransactionHistoryForReportAsync(report.Id);

        // Assert - Should only include transactions for activities in the report
        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.TransactionId == transaction1.Id);
        entries.Should().Contain(e => e.TransactionId == transaction2.Id);
        entries.Should().NotContain(e => e.TransactionId == transaction3.Id);
    }

    [Fact]
    public async Task GetTransactionHistoryForReportAsync_FiltersDinheiroActivities()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var dinheiroActivity = Activity.Create(report.Id, "DINHEIRO EM CAIXA", DateTime.Now);
        _context.Activities.Add(dinheiroActivity);
        await _context.SaveChangesAsync();

        var transaction = Transaction.Create(
            DateTime.Now, "Test Transaction", "Category", 100m, "Income", dinheiroActivity.Id);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Create audit logs - Created and Modified
        var createdLog = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = transaction.Id,
            Action = "Created",
            Timestamp = DateTime.UtcNow.AddHours(-1),
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", dinheiroActivity.Id },
                { "Description", "Test Transaction" }
            })
        };
        var modifiedLog = new AuditLog
        {
            EntityType = "Transaction",
            EntityId = transaction.Id,
            Action = "Modified",
            Timestamp = DateTime.UtcNow,
            UserName = "TestUser",
            Changes = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "ActivityId", dinheiroActivity.Id },
                { "Description", "Updated Transaction" }
            })
        };
        _context.AuditLogs.AddRange(createdLog, modifiedLog);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockActivityService.Setup(s => s.GetActivitiesByReportIdAsync(report.Id))
            .ReturnsAsync(new[] { dinheiroActivity });
        _mockAuditLogService.Setup(s => s.GetAllForExportAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(et => et == "Transaction"),
            It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new[] { createdLog, modifiedLog });

        // Act
        var (entries, totalCount) = await _service.GetTransactionHistoryForReportAsync(report.Id);

        // Assert - DINHEIRO activities should only show Modified, not Created
        entries.Should().HaveCount(1);
        entries.First().Action.Should().Be("Modified");
        entries.Should().NotContain(e => e.Action == "Created");
    }

    #endregion
}
