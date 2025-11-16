using RTUB.Application.Interfaces;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace RTUB.Application.Services;

/// <summary>
/// Meeting request service implementation
/// Contains business logic for CV meeting request operations
/// </summary>
public class MeetingRequestService : IMeetingRequestService
{
    private readonly ApplicationDbContext _context;

    public MeetingRequestService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<MeetingRequest>> GetAllAsync(RequestStatus? status = null)
    {
        var query = _context.MeetingRequests
            .Include(mr => mr.Author)
            .AsQueryable();
        
        if (status.HasValue)
        {
            query = query.Where(mr => mr.Status == status.Value);
        }
        
        return await query
            .OrderByDescending(mr => mr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingRequest>> GetPagedAsync(int page, int pageSize, RequestStatus? status = null)
    {
        var query = _context.MeetingRequests
            .Include(mr => mr.Author)
            .AsQueryable();
        
        if (status.HasValue)
        {
            query = query.Where(mr => mr.Status == status.Value);
        }
        
        return await query
            .OrderByDescending(mr => mr.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(RequestStatus? status = null)
    {
        var query = _context.MeetingRequests.AsQueryable();
        
        if (status.HasValue)
        {
            query = query.Where(mr => mr.Status == status.Value);
        }
        
        return await query.CountAsync();
    }

    public async Task<MeetingRequest?> GetByIdAsync(int id)
    {
        return await _context.MeetingRequests
            .Include(mr => mr.Author)
            .FirstOrDefaultAsync(mr => mr.Id == id);
    }

    public async Task<MeetingRequest> CreateAsync(MeetingRequest request)
    {
        _context.MeetingRequests.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task UpdateStatusAsync(int id, RequestStatus status)
    {
        var request = await _context.MeetingRequests.FindAsync(id);
        if (request == null)
            throw new InvalidOperationException($"Meeting request with ID {id} not found");
        
        request.Status = status;
        _context.MeetingRequests.Update(request);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var request = await _context.MeetingRequests.FindAsync(id);
        if (request == null)
            throw new InvalidOperationException($"Meeting request with ID {id} not found");
        
        _context.MeetingRequests.Remove(request);
        await _context.SaveChangesAsync();
    }
}
