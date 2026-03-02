using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Meeting entity
/// Note: Veterano filtering logic remains in service layer as it requires user context
/// </summary>
public class MeetingRepository : Repository<Meeting>, IMeetingRepository
{
    public MeetingRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Meeting>> GetAllMeetingsAsync(string? searchTerm, int pageNumber, int pageSize, string userId)
    {
        // Note: This is a simplified version. Veterano filtering is handled in the service layer
        // because it requires ApplicationUser context which creates tight coupling in repository
        using var context = CreateContext();
        var query = context.Set<Meeting>()
            .AsNoTracking()
            .AsQueryable()
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
                m => m.Title.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase) ||
                     m.Statement.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase));

        // Order by date - upcoming first, then past
        var today = DateTime.UtcNow.Date;
        query = query.OrderBy(m => m.Date >= today ? 0 : 1)
                     .ThenBy(m => m.Date >= today ? m.Date : DateTime.MaxValue)
                     .ThenByDescending(m => m.Date < today ? m.Date : DateTime.MinValue);

        return await query
            .Include(m => m.Organizer)
            .Include(m => m.TunoRepresentative)
            .PaginateAsync(pageNumber, pageSize);
    }

    public async Task<Meeting?> GetMeetingByIdAsync(int id, string userId)
    {
        // Note: Veterano visibility check is handled in the service layer
        using var context = CreateContext();
        return await context.Set<Meeting>()
            .AsNoTracking()
            .Include(m => m.Organizer)
            .Include(m => m.TunoRepresentative)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<int> GetTotalCountAsync(string? searchTerm, string userId)
    {
        // Note: Veterano filtering is handled in the service layer
        using var context = CreateContext();
        var query = context.Set<Meeting>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(m =>
                m.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                m.Statement.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        return await query.CountAsync();
    }
}
