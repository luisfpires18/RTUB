using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Enrollment entity
/// </summary>
public class EnrollmentRepository : Repository<Enrollment>, IEnrollmentRepository
{
    public EnrollmentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<Enrollment>> GetAllAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(e => e.Event)
            .ToListAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetByEventIdAsync(int eventId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Include(e => e.User)
            .ToListAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Include(e => e.Event)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<Enrollment?> GetByEventAndUserAsync(int eventId, string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    public async Task<IEnumerable<Enrollment>> GetByAttendanceAsync(bool willAttend)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => e.WillAttend == willAttend)
            .Include(e => e.User)
            .Include(e => e.Event)
            .ToListAsync();
    }
}
