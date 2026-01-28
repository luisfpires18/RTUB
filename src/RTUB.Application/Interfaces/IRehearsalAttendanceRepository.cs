using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for RehearsalAttendance entity
/// </summary>
public interface IRehearsalAttendanceRepository : IRepository<RehearsalAttendance>
{
    /// <summary>
    /// Get all attendance records for a rehearsal
    /// </summary>
    Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdAsync(int rehearsalId);

    /// <summary>
    /// Get all attendance records for multiple rehearsals (batch operation to avoid N+1 queries)
    /// </summary>
    Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdsAsync(IEnumerable<int> rehearsalIds);

    /// <summary>
    /// Get all attendance records for a user
    /// </summary>
    Task<IEnumerable<RehearsalAttendance>> GetAttendancesByUserIdAsync(string userId);

    /// <summary>
    /// Get attendance record for a specific rehearsal and user
    /// </summary>
    Task<RehearsalAttendance?> GetAttendanceByRehearsalAndUserAsync(int rehearsalId, string userId);

    /// <summary>
    /// Get attendance statistics for a user
    /// </summary>
    Task<(int TotalRehearsals, int Attended)> GetAttendanceStatsAsync(string userId);

    /// <summary>
    /// Delete all attendance records for a specific rehearsal (batch operation)
    /// </summary>
    Task DeleteByRehearsalIdAsync(int rehearsalId);
}
