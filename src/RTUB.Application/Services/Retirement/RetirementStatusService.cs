using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services.Retirement;

/// <summary>
/// Service for managing retirement status of active members
/// Implements business logic for evaluating CALOIRO/TUNO members based on activity history
/// </summary>
public class RetirementStatusService : IRetirementStatusService
{
    private readonly IRehearsalAttendanceRepository _rehearsalAttendanceRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IUserProfileRepository _userProfileRepository;

    // Business rule constants
    private const int MonthsToRetire = 6;
    private const int ConsecutiveMonthsToReturn = 3;
    private const int MaxMonthsToCheckForConsecutive = 12; // Look back up to 12 months

    public RetirementStatusService(
        IRehearsalAttendanceRepository rehearsalAttendanceRepository,
        IEnrollmentRepository enrollmentRepository,
        IUserProfileRepository userProfileRepository)
    {
        _rehearsalAttendanceRepository = rehearsalAttendanceRepository;
        _enrollmentRepository = enrollmentRepository;
        _userProfileRepository = userProfileRepository;
    }

    public async Task<RetirementStatusResult> EvaluateRetirementStatusAsync(string userId)
    {
        var result = new RetirementStatusResult
        {
            IsRetired = false,
            HasMinimumHistory = false,
            MonthsSinceLastActivity = 0
        };

        // Get user to check if they are CALOIRO/TUNO
        var user = await _userProfileRepository.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return result;
        }

        // Only evaluate CALOIRO/TUNO members
        var categories = user.Categories;
        if (!categories.Contains(MemberCategory.Caloiro) &&
            !categories.Contains(MemberCategory.Tuno) &&
            !categories.Contains(MemberCategory.Veterano) &&
            !categories.Contains(MemberCategory.Tunossauro))
        {
            return result;
        }

        // Get all activities (rehearsal attendances and event enrollments)
        var rehearsalAttendances = await _rehearsalAttendanceRepository.GetAttendancesByUserIdAsync(userId);
        var enrollments = await _enrollmentRepository.GetByUserIdAsync(userId);

        var now = DateTime.UtcNow;

        // Filter to only attended rehearsals from PAST, non-cancelled rehearsals
        // Use actual rehearsal date, not check-in timestamp
        var attendedRehearsals = rehearsalAttendances
            .Where(ra => ra.Attended
                && ra.Rehearsal != null
                && !ra.Rehearsal.IsCanceled
                && ra.Rehearsal.Date < now)
            .Select(ra => ra.Rehearsal!.Date)
            .ToList();

        // Filter to only confirmed enrollments for PAST, non-cancelled events
        // Use Event.EndDate (or Date) to determine if event is in the past,
        // but use Event.Date (start date) for month grouping to ensure
        // multi-day events are counted in the month they START, not END
        var confirmedEnrollments = enrollments
            .Where(e => e.WillAttend
                && e.Event != null
                && !e.Event.IsCancelled
                && (e.Event.EndDate ?? e.Event.Date) < now)  // Filter: event must have ended
            .Select(e => e.Event!.Date)  // Group by start date for consecutive month counting
            .ToList();

        // Combine all activities
        var allActivities = attendedRehearsals.Concat(confirmedEnrollments).OrderBy(d => d).ToList();

        if (!allActivities.Any())
        {
            // No activity history - preserve the user's current retirement status
            // This allows administrators to manually set initial status for new members
            result.HasMinimumHistory = false;
            result.IsRetired = user.IsRetired;
            return result;
        }

        result.HasMinimumHistory = true;
        result.FirstActivityDate = allActivities.First();
        result.LastActivityDate = allActivities.Last();

        var lastActivityDate = result.LastActivityDate.Value;

        // Calculate months since last activity (using Year/Month for calendar months)
        var monthsSinceLastActivity = ((now.Year - lastActivityDate.Year) * 12) + (now.Month - lastActivityDate.Month);
        result.MonthsSinceLastActivity = monthsSinceLastActivity;

        // Start with the user's CURRENT retirement status from the database
        // This is critical for proper state transition logic
        result.IsRetired = user.IsRetired;

        // State transition rules based on current status:
        // 1. RETIRED → ACTIVE: requires 3 consecutive months with activity (from most recent)
        // 2. ACTIVE → RETIRED: requires 6 consecutive months without any activity

        if (user.IsRetired)
        {
            // Currently retired - check if should return to active
            // Need 3 consecutive months WITH activity starting from most recent month
            var consecutiveMonthsWithActivity = CountConsecutiveMonthsWithActivity(allActivities, now);
            if (consecutiveMonthsWithActivity >= ConsecutiveMonthsToReturn)
            {
                result.IsRetired = false; // Return to active
            }
        }
        else
        {
            // Currently active - check if should become retired
            // Need 6 consecutive months WITHOUT activity
            if (monthsSinceLastActivity >= MonthsToRetire)
            {
                result.IsRetired = true; // Become retired
            }
        }

        return result;
    }

    /// <summary>
    /// Counts the number of consecutive months that have at least one activity
    /// starting from the most recent month and going backwards.
    /// Used to determine if a retired member should return to active status.
    /// Member needs 3 consecutive months to transition from RETIRED to ACTIVE.
    /// Stops counting when a month without activity is found.
    /// </summary>
    private int CountConsecutiveMonthsWithActivity(List<DateTime> activities, DateTime referenceDate)
    {
        if (activities.Count == 0)
        {
            return 0;
        }

        // Get activities grouped by year-month
        var activitiesByMonth = activities
            .GroupBy(a => new { a.Year, a.Month })
            .Select(g => new DateTime(g.Key.Year, g.Key.Month, 1))
            .ToHashSet();

        var currentMonth = new DateTime(referenceDate.Year, referenceDate.Month, 1);
        int consecutiveCount = 0;

        // Check from current month backwards
        for (int i = 0; i < MaxMonthsToCheckForConsecutive; i++)
        {
            var checkMonth = currentMonth.AddMonths(-i);

            if (activitiesByMonth.Contains(checkMonth))
            {
                consecutiveCount++;
            }
            else
            {
                // Stop counting when we find a month without activity (gap in consecutive months)
                break;
            }
        }

        return consecutiveCount;
    }

    public async Task<bool> UpdateUserRetirementStatusAsync(string userId)
    {
        var user = await _userProfileRepository.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        var evaluationResult = await EvaluateRetirementStatusAsync(userId);

        // Only update if value differs
        if (user.IsRetired != evaluationResult.IsRetired)
        {
            user.IsRetired = evaluationResult.IsRetired;
            await _userProfileRepository.UpdateAsync(user);
            return true;
        }

        return false;
    }

}
