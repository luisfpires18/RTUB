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
            // No activity history
            result.HasMinimumHistory = false;
            result.IsRetired = false;
            return result;
        }

        result.HasMinimumHistory = true;
        result.FirstActivityDate = allActivities.First();
        result.LastActivityDate = allActivities.Last();

        var lastActivityDate = result.LastActivityDate.Value;

        // Calculate months since last activity (using Year/Month for calendar months)
        var monthsSinceLastActivity = ((now.Year - lastActivityDate.Year) * 12) + (now.Month - lastActivityDate.Month);
        result.MonthsSinceLastActivity = monthsSinceLastActivity;

        // Check if should be retired (6+ months without activity)
        if (monthsSinceLastActivity >= MonthsToRetire)
        {
            result.IsRetired = true;

            // Check if should return to active (3 consecutive months with activity)
            if (HasConsecutiveMonthsWithActivity(allActivities, now, ConsecutiveMonthsToReturn))
            {
                result.IsRetired = false;
            }
        }

        return result;
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

    /// <summary>
    /// Checks if there are N consecutive calendar months with at least one activity
    /// Used to determine if a retired member should return to active status
    /// </summary>
    private bool HasConsecutiveMonthsWithActivity(List<DateTime> activities, DateTime referenceDate, int consecutiveMonths)
    {
        if (activities.Count == 0 || consecutiveMonths <= 0)
        {
            return false;
        }

        // Get activities grouped by year-month
        var activitiesByMonth = activities
            .GroupBy(a => new { a.Year, a.Month })
            .Select(g => new DateTime(g.Key.Year, g.Key.Month, 1))
            .OrderBy(d => d)
            .ToList();

        if (activitiesByMonth.Count < consecutiveMonths)
        {
            return false;
        }

        // Check for consecutive months ending at or near the reference date
        // Look backwards from reference date to find consecutive months
        var currentMonth = new DateTime(referenceDate.Year, referenceDate.Month, 1);
        int consecutiveCount = 0;

        for (int i = 0; i < MaxMonthsToCheckForConsecutive; i++)
        {
            var checkMonth = currentMonth.AddMonths(-i);

            if (activitiesByMonth.Contains(checkMonth))
            {
                consecutiveCount++;
                if (consecutiveCount >= consecutiveMonths)
                {
                    return true;
                }
            }
            else
            {
                // Break in consecutive months
                consecutiveCount = 0;
            }
        }

        return false;
    }
}
