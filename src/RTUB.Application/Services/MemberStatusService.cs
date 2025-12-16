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
    /// Retirement status follows a state transition model:
    /// - New members: manual initial state (can be set as active or retired)
    /// - RETIRED → ACTIVE: requires 3 consecutive months with activity
    /// - ACTIVE → RETIRED: requires 6 months without any activity
    /// CRITICAL: Only includes PAST activities (before DateTime.UtcNow)
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
        // CRITICAL: Only include PAST events (EndDate or Date < now)
        // Uses same predicate as MemberStatisticsService for XP calculation
        var lastEventDate = await _context.Enrollments
            .Include(e => e.Event)
            .Where(e => e.UserId == userId
                && e.WillAttend
                && e.Event != null
                && !e.Event.IsCancelled
                && (e.Event.EndDate ?? e.Event.Date) < now) // BUG FIX: Exclude future events
            .OrderByDescending(e => e.Event!.EndDate ?? e.Event!.Date)
            .Select(e => e.Event!.EndDate ?? e.Event!.Date)
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

        bool isRetired;

        // Apply retirement rules only if user has activity history
        if (hasAnyActivity)
        {
            // Start with current retirement status from database
            isRetired = user.IsRetired;
            
            // State transition rules based on activity:
            // 1. RETIRED → ACTIVE: requires 3 consecutive months with activity
            // 2. ACTIVE → RETIRED: requires 6 months without any activity
            
            if (user.IsRetired)
            {
                // Currently retired - check if should return to active
                // Need 3 consecutive months of activity to become active
                var consecutiveMonths = await CountConsecutiveMonthsWithActivityAsync(userId, now);
                if (consecutiveMonths >= 3)
                {
                    isRetired = false; // Return to active
                }
            }
            else
            {
                // Currently active - check if should become retired
                // Need 6 months without activity to become retired
                var sixMonthsAgo = now.AddMonths(-6);
                if (lastActivityDate!.Value < sixMonthsAgo)
                {
                    isRetired = true; // Become retired
                }
            }

            // Persist retirement status if changed
            if (user.IsRetired != isRetired)
            {
                user.IsRetired = isRetired;
                await _userManager.UpdateAsync(user);
            }
        }
        else
        {
            // No activity history -> keep current status (allows manual setting for new members)
            isRetired = user.IsRetired;
        }

        // Calculate progress based on consecutive months of activity
        int? progressMonths = null;
        int? progressTotalMonths = null;
        string? progressDescription = null;

        if (hasAnyActivity && lastActivityDate.HasValue)
        {
            // Get consecutive months count (already calculated above if retired, need to recalculate if active)
            var consecutiveMonths = await CountConsecutiveMonthsWithActivityAsync(userId, now);
            
            if (isRetired)
            {
                // For retired members: show consecutive months toward reactivation (need 3 to become active)
                progressMonths = consecutiveMonths;
                progressTotalMonths = 3;
                progressDescription = $"{consecutiveMonths}/3 meses de atividade consecutiva";
            }
            else
            {
                // For active members: show months until retirement (6 months from last activity)
                var monthsSinceLastActivity = GetMonthsDifference(lastActivityDate.Value, now);
                var monthsUntilReform = 6 - monthsSinceLastActivity;
                if (monthsUntilReform < 0) monthsUntilReform = 0;
                
                progressMonths = monthsUntilReform;
                progressTotalMonths = 6;
                progressDescription = monthsUntilReform > 0 
                    ? $"{monthsUntilReform} {(monthsUntilReform == 1 ? "mês" : "meses")} até reforma"
                    : "Próximo da reforma";
            }
        }

        return new MemberStatusResult
        {
            IsRetired = isRetired,
            LastRehearsalDate = lastRehearsalDate == default ? null : lastRehearsalDate,
            LastEventDate = lastEventDate == default ? null : lastEventDate,
            LastActivityDate = lastActivityDate,
            HasAnyActivity = hasAnyActivity,
            ProgressMonths = progressMonths,
            ProgressTotalMonths = progressTotalMonths,
            ProgressDescription = progressDescription
        };
    }

    
    /// <summary>
    /// Counts the number of consecutive months (starting from most recent) that have at least one activity
    /// Used to determine if a retired member should return to active status
    /// Member needs 3 consecutive months to transition from RETIRED to ACTIVE
    /// Stops counting when a month without activity is found
    /// CRITICAL: Only counts PAST activities (before referenceDate)
    /// </summary>
    private async Task<int> CountConsecutiveMonthsWithActivityAsync(string userId, DateTime referenceDate)
    {
        int consecutiveMonths = 0;

        // Check up to 12 months back (reasonable limit)
        // Start from i=0 to include the CURRENT month
        for (int i = 0; i < 12; i++)
        {
            var targetDate = referenceDate.AddMonths(-i);
            var monthStart = new DateTime(targetDate.Year, targetDate.Month, 1); // First day of the month
            var monthEnd = monthStart.AddMonths(1); // First day of next month

            // Get activities in this month
            // CRITICAL: Exclude future activities (after referenceDate)
            // For consecutive months, we count the START date of activities (when they began)
            var hasRehearsalInMonth = await _context.RehearsalAttendances
                .Include(ra => ra.Rehearsal)
                .AnyAsync(ra => ra.UserId == userId
                    && ra.Attended
                    && ra.Rehearsal != null
                    && !ra.Rehearsal.IsCanceled
                    && ra.Rehearsal.Date >= monthStart
                    && ra.Rehearsal.Date < monthEnd
                    && ra.Rehearsal.Date < referenceDate); // Exclude future rehearsals

            var hasEventInMonth = await _context.Enrollments
                .Include(e => e.Event)
                .AnyAsync(e => e.UserId == userId
                    && e.WillAttend
                    && e.Event != null
                    && !e.Event.IsCancelled
                    && e.Event.Date >= monthStart  // Use start date for month counting
                    && e.Event.Date < monthEnd
                    && e.Event.Date < referenceDate); // Exclude future events

            if (hasRehearsalInMonth || hasEventInMonth)
            {
                consecutiveMonths++;
            }
            else
            {
                // Stop counting when we find a month without activity
                break;
            }
        }

        return consecutiveMonths;
    }
    
    /// <summary>
    /// Calculates the difference in months between two dates
    /// </summary>
    private int GetMonthsDifference(DateTime startDate, DateTime endDate)
    {
        return ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month;
    }
}
