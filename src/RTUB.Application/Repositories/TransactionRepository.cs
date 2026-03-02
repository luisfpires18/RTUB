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
    public TransactionRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByActivityIdAsync(int activityId)
    {
        using var context = CreateContext();
        return await context.Set<Transaction>()
            .AsNoTracking()
            .Include(t => t.Activity)
            .Where(t => t.ActivityId == activityId)
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByActivityIdsAsync(IEnumerable<int> activityIds)
    {
        var activityIdsList = activityIds.ToList();
        if (!activityIdsList.Any())
        {
            return Enumerable.Empty<Transaction>();
        }

        using var context = CreateContext();
        return await context.Set<Transaction>()
            .AsNoTracking()
            .Include(t => t.Activity)
            .Where(t => activityIdsList.Contains(t.ActivityId ?? 0))
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(string type)
    {
        using var context = CreateContext();
        return await context.Set<Transaction>()
            .AsNoTracking()
            .Where(t => t.Type == type)
            .OrderBy(t => t.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(string userId)
    {
        using var context = CreateContext();
        return await context.Set<Transaction>()
            .AsNoTracking()
            .Include(t => t.Activity)
            .Include(t => t.User)
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Date)
            .ToListAsync();
    }
}
