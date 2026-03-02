using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for NaipeContent entity
/// </summary>
public class NaipeContentRepository : Repository<NaipeContent>, INaipeContentRepository
{
    public NaipeContentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<NaipeContent>> GetContentByInstrumentTypeAsync(InstrumentType instrumentType)
    {
        using var context = CreateContext();
        return await context.Set<NaipeContent>()
            .AsNoTracking() // Read-only operation - used for mapping to DTOs
            .Include(nc => nc.CreatedByUser)
            .Include(nc => nc.Comments.Where(c => c.DeletedAt == null))
            .Include(nc => nc.PlayCounts)
            .Where(nc => nc.InstrumentType == instrumentType)
            .OrderBy(nc => nc.SortOrder)
            .ThenBy(nc => nc.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<NaipeContent>> GetAllContentWithDetailsAsync()
    {
        using var context = CreateContext();
        return await context.Set<NaipeContent>()
            .AsNoTracking() // Read-only operation - used for mapping to DTOs
            .Include(nc => nc.CreatedByUser)
            .Include(nc => nc.Comments.Where(c => c.DeletedAt == null))
            .Include(nc => nc.PlayCounts)
            .OrderBy(nc => nc.InstrumentType)
            .ThenBy(nc => nc.SortOrder)
            .ThenBy(nc => nc.CreatedAt)
            .ToListAsync();
    }

    public async Task<NaipeContent?> GetByIdWithDetailsAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<NaipeContent>()
            .AsNoTracking() // Read-only operation - used for mapping to DTOs
            .Include(nc => nc.CreatedByUser)
            .Include(nc => nc.Comments.Where(c => c.DeletedAt == null))
                .ThenInclude(c => c.Author)
            .Include(nc => nc.PlayCounts)
            .FirstOrDefaultAsync(nc => nc.Id == id);
    }

    public async Task<int> GetPlayCountAsync(int contentId)
    {
        using var context = CreateContext();
        return await context.NaipePlayCounts
            .Where(pc => pc.NaipeContentId == contentId)
            .CountAsync();
    }
}
