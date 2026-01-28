using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Transaction operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface ITransactionService
{
    Task<Transaction?> GetTransactionByIdAsync(int id);
    Task<IEnumerable<Transaction>> GetAllTransactionsAsync();
    Task<IEnumerable<Transaction>> GetTransactionsByActivityIdAsync(int activityId);
    Task<IEnumerable<Transaction>> GetTransactionsByActivityIdsAsync(IEnumerable<int> activityIds);
    Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(string type);
    Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(string userId);
    Task<Transaction> CreateTransactionAsync(DateTime date, string description, string category, decimal amount, string type, int? activityId = null, Stream? receiptStream = null, string? receiptFileName = null, string? receiptContentType = null, string? userId = null);
    Task UpdateTransactionAsync(int id, DateTime date, string description, string category, decimal amount, string type, Stream? receiptStream = null, string? receiptFileName = null, string? receiptContentType = null, bool deleteReceipt = false, string? userId = null);
    Task DeleteTransactionAsync(int id);
    Task<string?> UploadReceiptAsync(int transactionId, Stream fileStream, string fileName, string contentType);
    Task DeleteReceiptAsync(int transactionId);
    
    /// <summary>
    /// Gets transaction history for a report based on audit logs
    /// Returns all Created, Modified, and Deleted actions for transactions belonging to activities in the report
    /// </summary>
    /// <param name="reportId">The report ID</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Tuple containing the transaction history entries and total count</returns>
    Task<(IEnumerable<TransactionHistoryEntryDto> entries, int totalCount)> GetTransactionHistoryForReportAsync(int reportId, int page = 1, int pageSize = 10);
}
