using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Transaction entity
/// </summary>
public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByActivityIdAsync(int activityId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(t => t.Activity)
            .Where(t => t.ActivityId == activityId)
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(string type)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.Type == type)
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(t => t.Activity)
            .Include(t => t.User)
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Date)
            .ToListAsync();
    }
}
