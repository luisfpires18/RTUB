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
}
