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
        return await _dbSet
            .Include(mr => mr.Author)
            .AsQueryable()
            .WhereIf(status.HasValue, mr => mr.Status == status!.Value)
            .OrderByDescending(mr => mr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingRequest>> GetPagedWithAuthorAsync(int page, int pageSize, RequestStatus? status = null)
    {
        return await _dbSet
            .Include(mr => mr.Author)
            .AsQueryable()
            .WhereIf(status.HasValue, mr => mr.Status == status!.Value)
            .OrderByDescending(mr => mr.CreatedAt)
            .PaginateAsync(page, pageSize);
    }

    public async Task<MeetingRequest?> GetByIdWithAuthorAsync(int id)
    {
        return await _dbSet
            .Include(mr => mr.Author)
            .FirstOrDefaultAsync(mr => mr.Id == id);
    }

    public async Task<int> GetCountAsync(RequestStatus? status = null)
    {
        return await _dbSet
            .AsQueryable()
            .WhereIf(status.HasValue, mr => mr.Status == status!.Value)
            .CountAsync();
    }

    public async Task<IEnumerable<MeetingRequest>> GetPendingWithAuthorAsync()
    {
        return await _dbSet
            .Include(r => r.Author)
            .Where(r => r.Status == RequestStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }
}
