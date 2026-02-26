using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

public class GalleryMediaRepository : Repository<GalleryMedia>, IGalleryMediaRepository
{
    public GalleryMediaRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<GalleryMedia>> GetAllWithDetailsAsync(int? year = null, string? personId = null, bool? isAuthenticated = null)
    {
        using var context = CreateContext();
        var query = context.GalleryMedia
            .Include(m => m.Uploader)
            .Include(m => m.PeopleInMedia)
                .ThenInclude(p => p.User)
            .AsQueryable();

        // Filter by privacy: if user is not authenticated, only show public media
        if (isAuthenticated.HasValue && !isAuthenticated.Value)
        {
            query = query.Where(m => !m.IsPrivate);
        }

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
        using var context = CreateContext();
        return await context.GalleryMedia
            .Include(m => m.Uploader)
            .Include(m => m.PeopleInMedia)
                .ThenInclude(p => p.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<int>> GetAvailableYearsAsync(bool? isAuthenticated = null)
    {
        using var context = CreateContext();
        var query = context.GalleryMedia.AsQueryable();

        // Filter by privacy: if user is not authenticated, only show public media
        if (isAuthenticated.HasValue && !isAuthenticated.Value)
        {
            query = query.Where(m => !m.IsPrivate);
        }

        return await query
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
        string? personId = null,
        bool? isAuthenticated = null,
        string? titleSearch = null)
    {
        using var context = CreateContext();
        var query = context.GalleryMedia
            .Include(m => m.Uploader)
            .Include(m => m.PeopleInMedia)
                .ThenInclude(p => p.User)
            .AsQueryable();

        // Filter by privacy: if user is not authenticated, only show public media
        if (isAuthenticated.HasValue && !isAuthenticated.Value)
        {
            query = query.Where(m => !m.IsPrivate);
        }

        if (year.HasValue)
        {
            query = query.Where(m => m.Year == year.Value);
        }

        if (!string.IsNullOrEmpty(personId))
        {
            query = query.Where(m => m.PeopleInMedia.Any(p => p.UserId == personId));
        }

        if (!string.IsNullOrEmpty(titleSearch))
        {
            query = query.Where(m => EF.Functions.Like(m.Title, $"%{titleSearch}%"));
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
