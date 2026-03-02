using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for MeetingRequest entity
/// </summary>
public class MeetingRequestRepository : Repository<MeetingRequest>, IMeetingRequestRepository
{
    public MeetingRequestRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<MeetingRequest>> GetAllWithAuthorAsync(RequestStatus? status = null)
    {
        using var context = CreateContext();
        return await context.Set<MeetingRequest>()
            .AsNoTracking()
            .Include(mr => mr.Author)
            .AsQueryable()
            .WhereIf(status.HasValue, mr => mr.Status == status!.Value)
            .OrderByDescending(mr => mr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingRequest>> GetPagedWithAuthorAsync(int page, int pageSize, RequestStatus? status = null)
    {
        using var context = CreateContext();
        return await context.Set<MeetingRequest>()
            .AsNoTracking()
            .Include(mr => mr.Author)
            .AsQueryable()
            .WhereIf(status.HasValue, mr => mr.Status == status!.Value)
            .OrderByDescending(mr => mr.CreatedAt)
            .PaginateAsync(page, pageSize);
    }

    public async Task<MeetingRequest?> GetByIdWithAuthorAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<MeetingRequest>()
            .AsNoTracking()
            .Include(mr => mr.Author)
            .FirstOrDefaultAsync(mr => mr.Id == id);
    }

    public async Task<int> GetCountAsync(RequestStatus? status = null)
    {
        using var context = CreateContext();
        return await context.Set<MeetingRequest>()
            .AsNoTracking()
            .AsQueryable()
            .WhereIf(status.HasValue, mr => mr.Status == status!.Value)
            .CountAsync();
    }

    public async Task<IEnumerable<MeetingRequest>> GetPendingWithAuthorAsync()
    {
        using var context = CreateContext();
        return await context.Set<MeetingRequest>()
            .AsNoTracking()
            .Include(r => r.Author)
            .Where(r => r.Status == RequestStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }
}
