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
    public PushSubscriptionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<PushSubscription>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync();
    }

    public async Task<PushSubscription?> GetByEndpointAsync(string endpoint)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint);
    }

    public async Task DeleteByEndpointAsync(string endpoint)
    {
        var subscription = await _dbSet
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        
        if (subscription != null)
        {
            _dbSet.Remove(subscription);
            await SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<PushSubscription>> GetAllActiveAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .ToListAsync();
    }
}
