using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;


namespace RTUB.Application.Services;

/// <summary>
/// Transaction service implementation using Repository pattern
/// Contains business logic for transaction operations
/// Now depends on ITransactionRepository abstraction instead of concrete DbContext
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IReceiptStorageService _receiptStorageService;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IReceiptStorageService receiptStorageService)
    {
        _transactionRepository = transactionRepository;
        _receiptStorageService = receiptStorageService;
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int id)
    {
        return await _transactionRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Transaction>> GetAllTransactionsAsync()
    {
        return await _transactionRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByActivityIdAsync(int activityId)
    {
        return await _transactionRepository.GetTransactionsByActivityIdAsync(activityId);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(string type)
    {
        return await _transactionRepository.GetTransactionsByTypeAsync(type);
    }

    public async Task<Transaction> CreateTransactionAsync(DateTime date, string description, string category, decimal amount, string type, int? activityId = null, Stream? receiptStream = null, string? receiptFileName = null, string? receiptContentType = null)
    {
        var transaction = Transaction.Create(date, description, category, amount, type, activityId);
        var createdTransaction = await _transactionRepository.AddAsync(transaction);

        // Upload receipt if provided
        if (receiptStream != null && !string.IsNullOrEmpty(receiptFileName) && !string.IsNullOrEmpty(receiptContentType))
        {
            var receiptUrl = await _receiptStorageService.UploadReceiptAsync(receiptStream, receiptFileName, receiptContentType, createdTransaction.Id);
            createdTransaction.SetReceiptUrl(receiptUrl);
            await _transactionRepository.UpdateAsync(createdTransaction);
        }

        return createdTransaction;
    }

    public async Task UpdateTransactionAsync(int id, DateTime date, string description, string category, decimal amount, string type, Stream? receiptStream = null, string? receiptFileName = null, string? receiptContentType = null, bool deleteReceipt = false)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id);
        if (transaction == null)
            throw new EntityNotFoundException(nameof(Transaction), id);

        transaction.UpdateDetails(date, description, category, amount, type);

        // Handle receipt deletion
        if (deleteReceipt && !string.IsNullOrEmpty(transaction.ReceiptUrl))
        {
            await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
            transaction.SetReceiptUrl(null);
        }
        // Handle receipt replacement
        else if (receiptStream != null && !string.IsNullOrEmpty(receiptFileName) && !string.IsNullOrEmpty(receiptContentType))
        {
            // Delete old receipt if exists
            if (!string.IsNullOrEmpty(transaction.ReceiptUrl))
            {
                await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
            }

            // Upload new receipt
            var receiptUrl = await _receiptStorageService.UploadReceiptAsync(receiptStream, receiptFileName, receiptContentType, id);
            transaction.SetReceiptUrl(receiptUrl);
        }

        await _transactionRepository.UpdateAsync(transaction);
    }

    public async Task DeleteTransactionAsync(int id)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id);
        if (transaction == null)
            throw new EntityNotFoundException(nameof(Transaction), id);

        // Delete receipt from storage if exists
        if (!string.IsNullOrEmpty(transaction.ReceiptUrl))
        {
            await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
        }

        await _transactionRepository.DeleteAsync(transaction);
    }

    public async Task<string?> UploadReceiptAsync(int transactionId, Stream fileStream, string fileName, string contentType)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId);
        if (transaction == null)
            throw new EntityNotFoundException(nameof(Transaction), transactionId);

        // Delete old receipt if exists
        if (!string.IsNullOrEmpty(transaction.ReceiptUrl))
        {
            await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
        }

        // Upload new receipt
        var receiptUrl = await _receiptStorageService.UploadReceiptAsync(fileStream, fileName, contentType, transactionId);
        transaction.SetReceiptUrl(receiptUrl);
        await _transactionRepository.UpdateAsync(transaction);

        return receiptUrl;
    }

    public async Task DeleteReceiptAsync(int transactionId)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId);
        if (transaction == null)
            throw new EntityNotFoundException(nameof(Transaction), transactionId);

        if (!string.IsNullOrEmpty(transaction.ReceiptUrl))
        {
            await _receiptStorageService.DeleteReceiptAsync(transaction.ReceiptUrl);
            transaction.SetReceiptUrl(null);
            await _transactionRepository.UpdateAsync(transaction);
        }
    }
}
