using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
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
    /// Optimized to avoid unnecessary Include - navigation property not needed for aggregation
    /// </summary>
    public async Task<Dictionary<string, int>> GetRehearsalAttendanceCountsByUserAsync(DateTime beforeDate)
    {
        // Optimized query: Use Join instead of Include for better performance
        // Only fetch the data we need (Date and UserId) without loading entire Rehearsal entities
        var attendanceCounts = await (
            from attendance in _context.RehearsalAttendances
            join rehearsal in _context.Rehearsals on attendance.RehearsalId equals rehearsal.Id
            where attendance.Attended && rehearsal.Date < beforeDate
            group attendance by attendance.UserId into g
            select new { UserId = g.Key, Count = g.Count() }
        ).ToDictionaryAsync(x => x.UserId, x => x.Count);

        return attendanceCounts;
    }

    /// <summary>
    /// Gets enrollments with event types for users who attended events before a specific date
    /// Optimized to use Join for better performance
    /// </summary>
    public async Task<List<UserEnrollmentWithEventType>> GetEnrollmentsByUserWithEventTypeAsync(DateTime beforeDate)
    {
        // Optimized query: Use Join instead of Include for better performance
        // EF Core will optimize this to avoid loading full Event entities
        var beforeDateOnly = beforeDate.Date;

        var enrollmentsWithTypes = await (
            from enrollment in _context.Enrollments
            join evt in _context.Events on enrollment.EventId equals evt.Id
            let eventEndDate = (evt.EndDate ?? evt.Date).Date
            where enrollment.WillAttend && eventEndDate < beforeDateOnly
            select new UserEnrollmentWithEventType
            {
                UserId = enrollment.UserId,
                EventType = evt.Type
            }
        ).ToListAsync();

        return enrollmentsWithTypes;
    }
}
