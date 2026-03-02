using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Discussion entity
/// </summary>
public class DiscussionRepository : Repository<Discussion>, IDiscussionRepository
{
    public DiscussionRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Discussion?> GetByIdWithEventAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Discussion>()
            .AsNoTracking()
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Discussion?> GetByEventIdAsync(int eventId)
    {
        using var context = CreateContext();
        return await context.Set<Discussion>()
            .AsNoTracking()
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.EventId == eventId);
    }
}
