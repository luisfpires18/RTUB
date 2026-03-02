using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Request entity
/// </summary>
public class RequestRepository : Repository<Request>, IRequestRepository
{
    public RequestRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Request>> GetByStatusAsync(RequestStatus status)
    {
        using var context = CreateContext();
        return await context.Set<Request>()
            .AsNoTracking()
            .Where(r => r.Status == status)
            .OrderBy(r => r.PreferredDate)
            .ToListAsync();
    }

    public async Task<int> GetPendingCountAsync()
    {
        using var context = CreateContext();
        return await context.Set<Request>()
            .AsNoTracking()
            .CountAsync(r => r.Status == RequestStatus.Pending);
    }
}
