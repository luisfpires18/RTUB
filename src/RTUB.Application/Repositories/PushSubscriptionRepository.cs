using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository for managing push subscriptions
/// </summary>
public class PushSubscriptionRepository : Repository<PushSubscription>, IPushSubscriptionRepository
{
    public PushSubscriptionRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<PushSubscription>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .Include(s => s.User)
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a subscription by endpoint URL
    /// Note: Returns a tracked entity to support update scenarios in SubscribeAsync
    /// </summary>
    public async Task<PushSubscription?> GetByEndpointAsync(string endpoint)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint);
    }

    public async Task DeleteByEndpointAsync(string endpoint)
    {
        using var context = CreateContext();
        var subscription = await context.Set<PushSubscription>()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint);

        if (subscription != null)
        {
            context.Set<PushSubscription>().Remove(subscription);
            await context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<PushSubscription>> GetAllActiveAsync()
    {
        return await _dbSet
            .Include(s => s.User)
            .AsNoTracking()
            .ToListAsync();
    }
}
