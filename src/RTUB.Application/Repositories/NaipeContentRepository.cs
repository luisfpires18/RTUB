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
    public NaipeContentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<NaipeContent>> GetContentByInstrumentTypeAsync(InstrumentType instrumentType)
    {
        return await _dbSet
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
        return await _dbSet
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
        return await _dbSet
            .AsNoTracking() // Read-only operation - used for mapping to DTOs
            .Include(nc => nc.CreatedByUser)
            .Include(nc => nc.Comments.Where(c => c.DeletedAt == null))
                .ThenInclude(c => c.Author)
            .Include(nc => nc.PlayCounts)
            .FirstOrDefaultAsync(nc => nc.Id == id);
    }

    public async Task<int> GetPlayCountAsync(int contentId)
    {
        return await _context.NaipePlayCounts
            .Where(pc => pc.NaipeContentId == contentId)
            .CountAsync();
    }
}
