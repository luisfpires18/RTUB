// TransactionService: receipts.
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public partial class TransactionServiceTests
{
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
        var updated = await _context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == transaction.Id);
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
}
