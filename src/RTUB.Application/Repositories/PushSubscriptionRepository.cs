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
        using var context = CreateContext();
        return await context.Set<PushSubscription>()
            .Include(s => s.User)
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a subscription by endpoint URL
    /// </summary>
    public async Task<PushSubscription?> GetByEndpointAsync(string endpoint)
    {
        using var context = CreateContext();
        return await context.Set<PushSubscription>()
            .AsNoTracking()
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
        using var context = CreateContext();
        return await context.Set<PushSubscription>()
            .Include(s => s.User)
            .AsNoTracking()
            .ToListAsync();
    }
}
