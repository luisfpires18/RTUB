using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;


namespace RTUB.Application.Services;

/// <summary>
/// Transaction service implementation using Repository pattern
/// Contains business logic for transaction operations
/// Now depends on ITransactionRepository abstraction instead of concrete DbContext
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;

    public TransactionService(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
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

    public async Task<Transaction> CreateTransactionAsync(DateTime date, string description, string category, decimal amount, string type, int? activityId = null)
    {
        var transaction = Transaction.Create(date, description, category, amount, type, activityId);
        return await _transactionRepository.AddAsync(transaction);
    }

    public async Task UpdateTransactionAsync(int id, DateTime date, string description, string category, decimal amount, string type)
    {
        var transaction = await _transactionRepository.GetByIdOrThrowAsync(id);

        transaction.UpdateDetails(date, description, category, amount, type);
        await _transactionRepository.UpdateAsync(transaction);
    }

    public async Task DeleteTransactionAsync(int id)
    {
        var transaction = await _transactionRepository.GetByIdOrThrowAsync(id);

        await _transactionRepository.DeleteAsync(transaction);
    }
}
