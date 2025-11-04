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
    Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(string type);
    Task<Transaction> CreateTransactionAsync(DateTime date, string description, string category, decimal amount, string type, int? activityId = null);
    Task UpdateTransactionAsync(int id, DateTime date, string description, string category, decimal amount, string type);
    Task SetTransactionReceiptAsync(int id, byte[]? receiptData, string? contentType);
    Task DeleteTransactionAsync(int id);
}
