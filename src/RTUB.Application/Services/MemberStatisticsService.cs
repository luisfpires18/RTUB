using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
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
    private readonly IOptions<XpSettings> _xpSettings;

    public MemberStatisticsService(ApplicationDbContext context, IOptions<XpSettings> xpSettings)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _xpSettings = xpSettings ?? throw new ArgumentNullException(nameof(xpSettings));
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
    /// Gets rehearsal attendance counts per user for attended rehearsals within a date range
    /// Optimized to avoid unnecessary Include - navigation property not needed for aggregation
    /// Also excludes future rehearsals (after today) to match "all years" behavior
    /// </summary>
    public async Task<Dictionary<string, int>> GetRehearsalAttendanceCountsByUserAsync(DateTime startDate, DateTime endDate)
    {
        var startDateOnly = startDate.Date;
        var endDateOnly = endDate.Date;
        var nowDate = DateTime.UtcNow.Date;

        var attendanceCounts = await (
            from attendance in _context.RehearsalAttendances
            join rehearsal in _context.Rehearsals on attendance.RehearsalId equals rehearsal.Id
            where attendance.Attended && rehearsal.Date >= startDateOnly && rehearsal.Date <= endDateOnly && rehearsal.Date < nowDate
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

    /// <summary>
    /// Gets enrollments with event types for users who attended events within a date range
    /// Optimized to use Join for better performance
    /// Also excludes future events (after today) to match "all years" behavior
    /// </summary>
    public async Task<List<UserEnrollmentWithEventType>> GetEnrollmentsByUserWithEventTypeAsync(DateTime startDate, DateTime endDate)
    {
        var startDateOnly = startDate.Date;
        var endDateOnly = endDate.Date;
        var nowDate = DateTime.UtcNow.Date;

        var enrollmentsWithTypes = await (
            from enrollment in _context.Enrollments
            join evt in _context.Events on enrollment.EventId equals evt.Id
            let eventEndDate = (evt.EndDate ?? evt.Date).Date
            where enrollment.WillAttend && eventEndDate >= startDateOnly && eventEndDate <= endDateOnly && eventEndDate < nowDate
            select new UserEnrollmentWithEventType
            {
                UserId = enrollment.UserId,
                EventType = evt.Type
            }
        ).ToListAsync();

        return enrollmentsWithTypes;
    }

    /// <summary>
    /// Gets the XP breakdown for a specific user
    /// Shows XP earned from rehearsals and each event type with per-unit and total values
    /// XP values come from appsettings via XpSettings configuration
    /// </summary>
    public async Task<UserXpBreakdownDto> GetUserXpBreakdownAsync(string userId, DateTime beforeDate)
    {
        var beforeDateOnly = beforeDate.Date;
        var xpConfig = _xpSettings.Value;

        // Get rehearsal count
        var rehearsalCount = await (
            from attendance in _context.RehearsalAttendances
            join rehearsal in _context.Rehearsals on attendance.RehearsalId equals rehearsal.Id
            where attendance.UserId == userId && attendance.Attended && rehearsal.Date < beforeDateOnly
            select attendance
        ).CountAsync();

        // Get events by type
        var eventsByType = await (
            from enrollment in _context.Enrollments
            join evt in _context.Events on enrollment.EventId equals evt.Id
            let eventEndDate = (evt.EndDate ?? evt.Date).Date
            where enrollment.UserId == userId && enrollment.WillAttend && eventEndDate < beforeDateOnly
            group evt by evt.Type into g
            select new { EventType = g.Key, Count = g.Count() }
        ).ToListAsync();

        // Calculate XP breakdown by event type
        var eventTypeXpList = new List<EventTypeXpDto>();
        foreach (var eventGroup in eventsByType)
        {
            var eventTypeName = eventGroup.EventType.ToString();
            var xpPerUnit = xpConfig.GetXpForEventType(eventTypeName);

            eventTypeXpList.Add(new EventTypeXpDto
            {
                TypeName = eventTypeName,
                Count = eventGroup.Count,
                XpPerUnit = xpPerUnit,
                TotalXp = eventGroup.Count * xpPerUnit
            });
        }

        // Calculate totals
        var rehearsalXpTotal = rehearsalCount * xpConfig.XpPerRehearsal;
        var eventsXpTotal = eventTypeXpList.Sum(e => e.TotalXp);

        return new UserXpBreakdownDto
        {
            TotalXp = rehearsalXpTotal + eventsXpTotal,
            RehearsalCount = rehearsalCount,
            RehearsalXpPerUnit = xpConfig.XpPerRehearsal,
            RehearsalXpTotal = rehearsalXpTotal,
            EventsByType = eventTypeXpList.OrderByDescending(e => e.TotalXp).ToList()
        };
    }

    /// <summary>
    /// Gets all attended activities (events and rehearsals) for a user
    /// Used for displaying detailed attended events list in leaderboard modal
    /// Ordered by date descending (newest first)
    /// </summary>
    public async Task<List<AttendedActivityDto>> GetUserAttendedActivitiesAsync(string userId, DateTime beforeDate)
    {
        var beforeDateOnly = beforeDate.Date;
        var xpConfig = _xpSettings.Value;
        var activities = new List<AttendedActivityDto>();

        // Get attended rehearsals
        // Must match filters in MemberStatusService.HasActivityInPeriodAsync for consistency
        var rehearsals = await (
            from attendance in _context.RehearsalAttendances
            join rehearsal in _context.Rehearsals on attendance.RehearsalId equals rehearsal.Id
            where attendance.UserId == userId 
                && attendance.Attended  // Only approved/confirmed attendance
                && !rehearsal.IsCanceled  // Exclude canceled rehearsals
                && rehearsal.Date < beforeDateOnly  // Only past rehearsals
            select new AttendedActivityDto
            {
                Date = rehearsal.Date,
                Name = "Ensaio" + (string.IsNullOrEmpty(rehearsal.Theme) ? "" : $" - {rehearsal.Theme}"),
                Type = "Ensaio",
                XpEarned = xpConfig.XpPerRehearsal,
                IsRehearsal = true
            }
        ).ToListAsync();

        activities.AddRange(rehearsals);

        // Get attended events with event type information
        // Must match filters in MemberStatusService.HasActivityInPeriodAsync for consistency
        var eventData = await (
            from enrollment in _context.Enrollments
            join evt in _context.Events on enrollment.EventId equals evt.Id
            let eventEndDate = (evt.EndDate ?? evt.Date).Date
            where enrollment.UserId == userId 
                && enrollment.WillAttend  // Only enrolled attendees
                && !evt.IsCancelled  // Exclude canceled events
                && eventEndDate < beforeDateOnly  // Only past events
            select new
            {
                Date = evt.Date,
                Name = evt.Name,
                Type = evt.Type
            }
        ).ToListAsync();

        // Transform events to AttendedActivityDto with calculated XP
        var eventActivities = eventData.Select(evt => new AttendedActivityDto
        {
            Date = evt.Date,
            Name = evt.Name,
            Type = StatusHelper.GetEventTypeDisplay(evt.Type),
            XpEarned = xpConfig.GetXpForEventType(evt.Type.ToString()),
            IsRehearsal = false
        });

        activities.AddRange(eventActivities);

        // Sort by date descending (newest first)
        return activities.OrderByDescending(a => a.Date).ToList();
    }
}
