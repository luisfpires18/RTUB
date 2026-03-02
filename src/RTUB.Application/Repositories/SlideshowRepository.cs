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
        using var context = CreateContext();
        return await context.Set<Slideshow>()
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<IEnumerable<Slideshow>> GetActivePublicSlideshowsAsync()
    {
        using var context = CreateContext();
        return await context.Set<Slideshow>()
            .AsNoTracking()
            .Where(s => s.IsActive && !s.IsExclusive)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }
}
