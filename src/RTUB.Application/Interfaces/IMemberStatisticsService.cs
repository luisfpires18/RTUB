using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for member statistics operations
/// Provides aggregated data for leaderboard and member analytics
/// Extracts complex queries from Blazor pages following SOLID principles
/// </summary>
public interface IMemberStatisticsService
{
    /// <summary>
    /// Gets rehearsal attendance counts per user for attended rehearsals before a specific date
    /// </summary>
    /// <param name="beforeDate">Only count rehearsals before this date</param>
    /// <returns>Dictionary mapping UserId to attendance count</returns>
    Task<Dictionary<string, int>> GetRehearsalAttendanceCountsByUserAsync(DateTime beforeDate);

    /// <summary>
    /// Gets enrollments with event types for users who attended events before a specific date
    /// </summary>
    /// <param name="beforeDate">Only include events before this date</param>
    /// <returns>List of user enrollments with event types</returns>
    Task<List<UserEnrollmentWithEventType>> GetEnrollmentsByUserWithEventTypeAsync(DateTime beforeDate);
}
