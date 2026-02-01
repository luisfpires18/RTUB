using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for TransactionService
/// Tests financial transaction operations (Income/Expense)
/// HIGH PRIORITY - Financial data handling
/// </summary>
public class TransactionServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly TransactionService _service;
    private readonly Mock<IReceiptStorageService> _mockReceiptStorageService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<IActivityService> _mockActivityService;

    public TransactionServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _mockReceiptStorageService = new Mock<IReceiptStorageService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockActivityService = new Mock<IActivityService>();
        _service = new TransactionService(
            new TransactionRepository(_context),
            _mockReceiptStorageService.Object,
            _mockAuditLogService.Object,
            _mockActivityService.Object);
    }

    #region Create Tests

    [Fact]
    public async Task CreateTransactionAsync_WithValidIncome_CreatesTransaction()
    {
        // Arrange
        var date = DateTime.Now;
        var description = "Membership fee payment";
        var category = "Membership";
        var amount = 50.00m;
        var type = "Income";

        // Act
        var result = await _service.CreateTransactionAsync(date, description, category, amount, type);

        // Assert
        result.Should().NotBeNull();
        result.Date.Should().Be(date);
        result.Description.Should().Be(description);
        result.Category.Should().Be(category);
        result.Amount.Should().Be(amount);
        result.Type.Should().Be(type);
        result.ActivityId.Should().BeNull();
    }

    [Fact]
    public async Task CreateTransactionAsync_WithValidExpense_CreatesTransaction()
    {
        // Arrange
        var date = DateTime.Now;
        var description = "Instrument repair";
        var category = "Equipment";
        var amount = 150.00m;
        var type = "Expense";

        // Act
        var result = await _service.CreateTransactionAsync(date, description, category, amount, type);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be("Expense");
        result.Amount.Should().Be(150.00m);
    }

    [Fact]
    public async Task CreateTransactionAsync_WithActivityId_AssociatesActivity()
    {
        // Arrange
        var date = DateTime.Now;
        var description = "Event revenue";
        var category = "Events";
        var amount = 500.00m;
        var type = "Income";
        var activityId = 1;

        // Act
        var result = await _service.CreateTransactionAsync(date, description, category, amount, type, activityId);

        // Assert
        result.ActivityId.Should().Be(activityId);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task GetTransactionByIdAsync_ExistingTransaction_ReturnsTransaction()
    {
        // Arrange
        var transaction = await _service.CreateTransactionAsync(
            DateTime.Now, "Test", "Category", 100m, "Income");

        // Act
        var result = await _service.GetTransactionByIdAsync(transaction.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(transaction.Id);
        result.Description.Should().Be("Test");
    }

    [Fact]
    public async Task GetTransactionByIdAsync_NonExistingTransaction_ReturnsNull()
    {
        // Act
        var result = await _service.GetTransactionByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllTransactionsAsync_WithMultipleTransactions_ReturnsAll()
    {
        // Arrange
        await _service.CreateTransactionAsync(DateTime.Now, "Transaction 1", "Cat1", 100m, "Income");
        await _service.CreateTransactionAsync(DateTime.Now, "Transaction 2", "Cat2", 200m, "Expense");
        await _service.CreateTransactionAsync(DateTime.Now, "Transaction 3", "Cat3", 300m, "Income");

        // Act
        var result = await _service.GetAllTransactionsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllTransactionsAsync_WithNoTransactions_ReturnsEmpty()
    {
        // Act
        var result = await _service.GetAllTransactionsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionsByActivityIdAsync_FiltersByActivity_OrdersByDate()
    {
        // Arrange
        var activityId = 5;
        await _service.CreateTransactionAsync(DateTime.Now.AddDays(-2), "Transaction 1", "Cat1", 100m, "Income", activityId);
        await _service.CreateTransactionAsync(DateTime.Now.AddDays(-1), "Transaction 2", "Cat2", 200m, "Expense", activityId);
        await _service.CreateTransactionAsync(DateTime.Now, "Transaction 3", "Cat3", 300m, "Income", 999); // Different activity

        // Act
        var result = (await _service.GetTransactionsByActivityIdAsync(activityId)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Description.Should().Be("Transaction 1"); // Oldest first
        result[1].Description.Should().Be("Transaction 2");
    }

    [Fact]
    public async Task GetTransactionsByTypeAsync_FiltersIncomeOnly()
    {
        // Arrange
        await _service.CreateTransactionAsync(DateTime.Now, "Income 1", "Cat1", 100m, "Income");
        await _service.CreateTransactionAsync(DateTime.Now, "Expense 1", "Cat2", 200m, "Expense");
        await _service.CreateTransactionAsync(DateTime.Now, "Income 2", "Cat3", 300m, "Income");

        // Act
        var result = await _service.GetTransactionsByTypeAsync("Income");

        // Assert
        result.Should().HaveCount(2);
        result.All(t => t.Type == "Income").Should().BeTrue();
    }

    [Fact]
    public async Task GetTransactionsByTypeAsync_FiltersExpenseOnly()
    {
        // Arrange
        await _service.CreateTransactionAsync(DateTime.Now, "Income 1", "Cat1", 100m, "Income");
        await _service.CreateTransactionAsync(DateTime.Now, "Expense 1", "Cat2", 200m, "Expense");
        await _service.CreateTransactionAsync(DateTime.Now, "Expense 2", "Cat3", 300m, "Expense");

        // Act
        var result = await _service.GetTransactionsByTypeAsync("Expense");

        // Assert
        result.Should().HaveCount(2);
        result.All(t => t.Type == "Expense").Should().BeTrue();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateTransactionAsync_WithValidData_UpdatesTransaction()
    {
        // Arrange
        var transaction = await _service.CreateTransactionAsync(
            DateTime.Now, "Original", "Cat1", 100m, "Income");

        var newDate = DateTime.Now.AddDays(1);
        var newDescription = "Updated";
        var newCategory = "Cat2";
        var newAmount = 200m;
        var newType = "Expense";

        // Act
        await _service.UpdateTransactionAsync(transaction.Id, newDate, newDescription, newCategory, newAmount, newType);

        // Assert
        var updated = await _service.GetTransactionByIdAsync(transaction.Id);
        updated!.Date.Should().Be(newDate);
        updated.Description.Should().Be(newDescription);
        updated.Category.Should().Be(newCategory);
        updated.Amount.Should().Be(newAmount);
        updated.Type.Should().Be(newType);
    }

    [Fact]
    public async Task UpdateTransactionAsync_NonExistingTransaction_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.UpdateTransactionAsync(
            999, DateTime.Now, "Test", "Cat", 100m, "Income");

        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteTransactionAsync_ExistingTransaction_DeletesTransaction()
    {
        // Arrange
        var transaction = await _service.CreateTransactionAsync(
            DateTime.Now, "Test", "Cat", 100m, "Income");

        // Act
        await _service.DeleteTransactionAsync(transaction.Id);

        // Assert
        var deleted = await _service.GetTransactionByIdAsync(transaction.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTransactionAsync_NonExistingTransaction_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.DeleteTransactionAsync(999);

        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region UploadReceiptAsync Tests

    [Fact]
    public async Task UploadReceiptAsync_ExistingTransaction_UploadsAndReturnsUrl()
    {
        // Arrange
        var transaction = await _service.CreateTransactionAsync(
            DateTime.Now, "Test", "Cat", 100m, "Income");
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        const string expectedUrl = "https://storage.example/receipts/1.pdf";
        _mockReceiptStorageService
            .Setup(x => x.UploadReceiptAsync(stream, "receipt.pdf", "application/pdf", transaction.Id))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _service.UploadReceiptAsync(transaction.Id, stream, "receipt.pdf", "application/pdf");

        // Assert
        result.Should().Be(expectedUrl);
        _mockReceiptStorageService.Verify(
            x => x.UploadReceiptAsync(stream, "receipt.pdf", "application/pdf", transaction.Id),
            Times.Once);
    }

    [Fact]
    public async Task UploadReceiptAsync_NonExistingTransaction_ThrowsException()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act & Assert
        var act = async () => await _service.UploadReceiptAsync(999, stream, "receipt.pdf", "application/pdf");

        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region DeleteReceiptAsync Tests

    [Fact]
    public async Task DeleteReceiptAsync_ExistingTransactionWithReceipt_DeletesReceiptAndClearsUrl()
    {
        // Arrange
        var transaction = await _service.CreateTransactionAsync(
            DateTime.Now, "Test", "Cat", 100m, "Income");
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        const string receiptUrl = "https://storage.example/receipts/1.pdf";
        _mockReceiptStorageService
            .Setup(x => x.UploadReceiptAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), transaction.Id))
            .ReturnsAsync(receiptUrl);
        await _service.UploadReceiptAsync(transaction.Id, stream, "receipt.pdf", "application/pdf");
        _mockReceiptStorageService
            .Setup(x => x.DeleteReceiptAsync(receiptUrl))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteReceiptAsync(transaction.Id);

        // Assert
        _mockReceiptStorageService.Verify(x => x.DeleteReceiptAsync(receiptUrl), Times.Once);
        var updated = await _service.GetTransactionByIdAsync(transaction.Id);
        updated!.ReceiptUrl.Should().BeNull();
    }

    [Fact]
    public async Task DeleteReceiptAsync_NonExistingTransaction_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.DeleteReceiptAsync(999);

        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    #endregion

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
        var testRepo = new TransactionRepository(_context);
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

    public void Dispose()
    {
        _context?.Dispose();
    }
}
