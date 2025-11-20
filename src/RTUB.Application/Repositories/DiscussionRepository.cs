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
    public DiscussionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Discussion?> GetByIdWithEventAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Discussion?> GetByEventIdAsync(int eventId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(d => d.Event)
            .FirstOrDefaultAsync(d => d.EventId == eventId);
    }
}
