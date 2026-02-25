using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Slideshow entity
/// </summary>
public class SlideshowRepository : Repository<Slideshow>, ISlideshowRepository
{
    public SlideshowRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Slideshow>> GetActiveSlideshowsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<IEnumerable<Slideshow>> GetActivePublicSlideshowsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(s => s.IsActive && !s.IsExclusive)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }
}
