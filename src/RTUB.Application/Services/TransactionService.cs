using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// Transaction service implementation
/// Contains business logic for transaction operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly ApplicationDbContext _context;

    public TransactionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int id)
    {
        return await _context.Transactions.FindAsync(id);
    }

    public async Task<IEnumerable<Transaction>> GetAllTransactionsAsync()
    {
        return await _context.Transactions.ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByActivityIdAsync(int activityId)
    {
        return await _context.Transactions
            .Where(t => t.ActivityId == activityId)
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(string type)
    {
        var allTransactions = await _context.Transactions.ToListAsync();
        return allTransactions.Where(t => t.Type == type);
    }

    public async Task<Transaction> CreateTransactionAsync(DateTime date, string description, string category, decimal amount, string type, int? activityId = null)
    {
        var transaction = Transaction.Create(date, description, category, amount, type, activityId);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task UpdateTransactionAsync(int id, DateTime date, string description, string category, decimal amount, string type)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if (transaction == null)
            throw new InvalidOperationException($"Transaction with ID {id} not found");

        transaction.UpdateDetails(date, description, category, amount, type);
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task SetTransactionReceiptAsync(int id, byte[]? receiptData, string? contentType)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if (transaction == null)
            throw new InvalidOperationException($"Transaction with ID {id} not found");

        transaction.SetReceipt(receiptData, contentType);
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteTransactionAsync(int id)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if (transaction == null)
            throw new InvalidOperationException($"Transaction with ID {id} not found");

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();
    }
}
