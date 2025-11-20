using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Member statistics service implementation
/// Provides aggregated data for leaderboard and member analytics
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class MemberStatisticsService : IMemberStatisticsService
{
    private readonly ApplicationDbContext _context;

    public MemberStatisticsService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets rehearsal attendance counts per user for attended rehearsals before a specific date
    /// Extracts the query logic from Leaderboard.razor (lines 456-461)
    /// </summary>
    public async Task<Dictionary<string, int>> GetRehearsalAttendanceCountsByUserAsync(DateTime beforeDate)
    {
        // Query: Include rehearsal navigation property, filter by attendance and date, group by user
        // Original query from Leaderboard.razor:
        // var allRehearsalAttendances = await DbContext.RehearsalAttendances
        //     .Include(ra => ra.Rehearsal)
        //     .Where(a => a.Attended && a.Rehearsal!.Date < now)
        //     .GroupBy(a => a.UserId)
        //     .Select(g => new { UserId = g.Key, Count = g.Count() })
        //     .ToDictionaryAsync(x => x.UserId, x => x.Count);
        
        var attendanceCounts = await _context.RehearsalAttendances
            .Include(ra => ra.Rehearsal)
            .Where(a => a.Attended && a.Rehearsal!.Date < beforeDate)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        return attendanceCounts;
    }

    /// <summary>
    /// Gets enrollments with event types for users who attended events before a specific date
    /// Extracts the query logic from Leaderboard.razor (lines 464-468)
    /// </summary>
    public async Task<List<UserEnrollmentWithEventType>> GetEnrollmentsByUserWithEventTypeAsync(DateTime beforeDate)
    {
        // Query: Include event navigation property, filter by attendance and date, select user and event type
        // Original query from Leaderboard.razor:
        // var allEnrollmentsWithTypes = await DbContext.Enrollments
        //     .Include(e => e.Event)
        //     .Where(e => e.WillAttend && e.Event != null && e.Event!.Date < now)
        //     .Select(e => new { e.UserId, EventType = e.Event!.Type })
        //     .ToListAsync();
        
        var enrollmentsWithTypes = await _context.Enrollments
            .Include(e => e.Event)
            .Where(e => e.WillAttend && e.Event != null && e.Event!.Date < beforeDate)
            .Select(e => new UserEnrollmentWithEventType
            {
                UserId = e.UserId,
                EventType = e.Event!.Type
            })
            .ToListAsync();

        return enrollmentsWithTypes;
    }
}
