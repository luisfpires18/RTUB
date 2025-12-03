using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

public class GalleryMediaRepository : Repository<GalleryMedia>, IGalleryMediaRepository
{
    public GalleryMediaRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<GalleryMedia>> GetAllWithDetailsAsync(int? year = null, string? personId = null)
    {
        var query = _context.GalleryMedia
            .Include(m => m.Uploader)
            .Include(m => m.PeopleInMedia)
                .ThenInclude(p => p.User)
            .AsQueryable();

        if (year.HasValue)
        {
            query = query.Where(m => m.Year == year.Value);
        }

        if (!string.IsNullOrEmpty(personId))
        {
            query = query.Where(m => m.PeopleInMedia.Any(p => p.UserId == personId));
        }

        return await query
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month ?? 0)
            .ThenByDescending(m => m.Day ?? 0)
            .ThenByDescending(m => m.TakenAt ?? m.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<GalleryMedia?> GetByIdWithDetailsAsync(int id)
    {
        return await _context.GalleryMedia
            .Include(m => m.Uploader)
            .Include(m => m.PeopleInMedia)
                .ThenInclude(p => p.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<int>> GetAvailableYearsAsync()
    {
        return await _context.GalleryMedia
            .AsNoTracking()
            .Select(m => m.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();
    }

    public async Task<(IEnumerable<GalleryMedia> Items, int TotalCount)> GetPaginatedWithDetailsAsync(
        int page,
        int pageSize,
        int? year = null,
        string? personId = null)
    {
        var query = _context.GalleryMedia
            .Include(m => m.Uploader)
            .Include(m => m.PeopleInMedia)
                .ThenInclude(p => p.User)
            .AsQueryable();

        if (year.HasValue)
        {
            query = query.Where(m => m.Year == year.Value);
        }

        if (!string.IsNullOrEmpty(personId))
        {
            query = query.Where(m => m.PeopleInMedia.Any(p => p.UserId == personId));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month ?? 0)
            .ThenByDescending(m => m.Day ?? 0)
            .ThenByDescending(m => m.TakenAt ?? m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }
}
