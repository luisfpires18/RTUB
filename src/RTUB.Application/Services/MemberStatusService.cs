using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Member status service implementation
/// Provides comprehensive member status including retirement state and last activity tracking
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class MemberStatusService : IMemberStatusService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MemberStatusService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    /// <summary>
    /// Gets the comprehensive status of a member including retirement state and last activity dates
    /// CRITICAL BUG FIX: Only includes PAST activities (before DateTime.UtcNow)
    /// Uses same predicates as XP/Leaderboard logic to ensure consistency
    /// </summary>
    /// <param name="userId">The user ID to get status for</param>
    /// <returns>A result containing retirement status, last activity dates, and activity flags</returns>
    public async Task<MemberStatusResult> GetMemberStatusAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        var now = DateTime.UtcNow;

        // Get last rehearsal date where member was present and rehearsal is not canceled
        // CRITICAL: Only include PAST rehearsals (Date < now)
        // Uses same predicate as MemberStatisticsService for XP calculation
        var lastRehearsalDate = await _context.RehearsalAttendances
            .Include(ra => ra.Rehearsal)
            .Where(ra => ra.UserId == userId
                && ra.Attended
                && ra.Rehearsal != null
                && !ra.Rehearsal.IsCanceled
                && ra.Rehearsal.Date < now) // BUG FIX: Exclude future rehearsals
            .OrderByDescending(ra => ra.Rehearsal!.Date)
            .Select(ra => ra.Rehearsal!.Date)
            .FirstOrDefaultAsync();

        // Get last event date where member is enrolled (WillAttend) and event is not canceled
        // CRITICAL: Only include PAST events (Date < now)
        // Uses same predicate as MemberStatisticsService for XP calculation
        var lastEventDate = await _context.Enrollments
            .Include(e => e.Event)
            .Where(e => e.UserId == userId
                && e.WillAttend
                && e.Event != null
                && !e.Event.IsCancelled
                && e.Event.Date < now) // BUG FIX: Exclude future events
            .OrderByDescending(e => e.Event!.Date)
            .Select(e => e.Event!.Date)
            .FirstOrDefaultAsync();

        // Calculate last activity date as the maximum of the two
        DateTime? lastActivityDate = null;
        bool hasLastRehearsal = lastRehearsalDate != default(DateTime);
        bool hasLastEvent = lastEventDate != default(DateTime);

        if (hasLastRehearsal && hasLastEvent)
        {
            lastActivityDate = lastRehearsalDate > lastEventDate
                ? lastRehearsalDate
                : lastEventDate;
        }
        else if (hasLastRehearsal)
        {
            lastActivityDate = lastRehearsalDate;
        }
        else if (hasLastEvent)
        {
            lastActivityDate = lastEventDate;
        }

        // Determine if user has any past activity
        bool hasAnyActivity = lastActivityDate.HasValue;

        // Get current user to check and update retirement status
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        bool isRetired = user.IsRetired;

        // Apply retirement rules only if user has activity history
        if (hasAnyActivity)
        {
            // Rule 1: User is retired if last activity > 6 months ago
            var sixMonthsAgo = now.AddMonths(-6);
            if (lastActivityDate!.Value < sixMonthsAgo)
            {
                isRetired = true;
            }
            // Rule 2: Retired user returns to active if 3 consecutive months with activity
            else if (user.IsRetired)
            {
                // Check if user has activity in last 3 consecutive months
                var threeMonthsAgo = now.AddMonths(-3);
                var hasConsecutiveActivity = await HasConsecutiveMonthlyActivityAsync(userId, threeMonthsAgo, now);

                if (hasConsecutiveActivity)
                {
                    isRetired = false;
                }
            }

            // Persist retirement status if changed
            if (user.IsRetired != isRetired)
            {
                user.IsRetired = isRetired;
                await _userManager.UpdateAsync(user);
            }
        }

        return new MemberStatusResult
        {
            IsRetired = isRetired,
            LastRehearsalDate = lastRehearsalDate == default ? null : lastRehearsalDate,
            LastEventDate = lastEventDate == default ? null : lastEventDate,
            LastActivityDate = lastActivityDate,
            HasAnyActivity = hasAnyActivity
        };
    }

    /// <summary>
    /// Checks if user has at least one approved activity in each of the last 3 consecutive months
    /// Used to determine if a retired member should return to active status
    /// </summary>
    private async Task<bool> HasConsecutiveMonthlyActivityAsync(string userId, DateTime startDate, DateTime endDate)
    {
        // Get all activities in the period
        var rehearsalDates = await _context.RehearsalAttendances
            .Include(ra => ra.Rehearsal)
            .Where(ra => ra.UserId == userId
                && ra.Attended
                && ra.Rehearsal != null
                && !ra.Rehearsal.IsCanceled
                && ra.Rehearsal.Date >= startDate
                && ra.Rehearsal.Date < endDate)
            .Select(ra => ra.Rehearsal!.Date)
            .ToListAsync();

        var eventDates = await _context.Enrollments
            .Include(e => e.Event)
            .Where(e => e.UserId == userId
                && e.WillAttend
                && e.Event != null
                && !e.Event.IsCancelled
                && e.Event.Date >= startDate
                && e.Event.Date < endDate)
            .Select(e => e.Event!.Date)
            .ToListAsync();

        // Combine all activity dates
        var allActivityDates = rehearsalDates.Concat(eventDates).ToList();

        if (!allActivityDates.Any())
            return false;

        // Check if there's at least one activity in each of the last 3 months
        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            var monthStart = now.AddMonths(-(i + 1));
            var monthEnd = now.AddMonths(-i);

            var hasActivityInMonth = allActivityDates.Any(d => d >= monthStart && d < monthEnd);
            if (!hasActivityInMonth)
            {
                return false;
            }
        }

        return true;
    }
}
