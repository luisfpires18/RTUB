using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for TransactionService
/// Tests financial transaction operations (Income/Expense)
/// HIGH PRIORITY - Financial data handling
/// </summary>
public class TransactionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TransactionService _service;

    public TransactionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new TransactionService(_context);
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

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region Receipt Tests

    [Fact]
    public async Task SetTransactionReceiptAsync_WithValidData_SetsReceipt()
    {
        // Arrange
        var transaction = await _service.CreateTransactionAsync(
            DateTime.Now, "Test", "Cat", 100m, "Income");
        var receiptData = new byte[] { 1, 2, 3, 4 };
        var contentType = "image/jpeg";

        // Act
        await _service.SetTransactionReceiptAsync(transaction.Id, receiptData, contentType);

        // Assert
        var updated = await _service.GetTransactionByIdAsync(transaction.Id);
        updated!.ReceiptData.Should().Equal(receiptData);
        updated.ReceiptContentType.Should().Be(contentType);
    }

    [Fact]
    public async Task SetTransactionReceiptAsync_WithNullData_ClearsReceipt()
    {
        // Arrange
        var transaction = await _service.CreateTransactionAsync(
            DateTime.Now, "Test", "Cat", 100m, "Income");
        await _service.SetTransactionReceiptAsync(transaction.Id, new byte[] { 1, 2, 3 }, "image/jpeg");

        // Act
        await _service.SetTransactionReceiptAsync(transaction.Id, null, null);

        // Assert
        var updated = await _service.GetTransactionByIdAsync(transaction.Id);
        updated!.ReceiptData.Should().BeNull();
        updated.ReceiptContentType.Should().BeNull();
    }

    [Fact]
    public async Task SetTransactionReceiptAsync_NonExistingTransaction_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.SetTransactionReceiptAsync(
            999, new byte[] { 1, 2, 3 }, "image/jpeg");

        await act.Should().ThrowAsync<InvalidOperationException>()
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

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}
