using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for rehearsal attendance management
/// </summary>
public interface IRehearsalAttendanceService
{
    Task<RehearsalAttendance?> GetAttendanceByIdAsync(int id);
    Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdAsync(int rehearsalId);
    Task<IEnumerable<RehearsalAttendance>> GetAttendancesByUserIdAsync(string userId);
    
    /// <summary>
    /// Marks a user as attended at a rehearsal. Creates new attendance if not exists, updates if exists.
    /// Defaults to pending (Attended = false) for admin approval.
    /// </summary>
    Task<RehearsalAttendance> MarkAttendanceAsync(int rehearsalId, string userId, bool willAttend = true, InstrumentType? instrument = null, string? notes = null, string? otherInstruments = null);
    
    /// <summary>
    /// Creates attendance with immediate approval (Attended = true). Used by admin for past/approvable rehearsals.
    /// Avoids creating separate "Created" + "Modified" audit entries.
    /// </summary>
    Task<RehearsalAttendance> CreateAttendanceWithApprovalAsync(int rehearsalId, string userId, InstrumentType? instrument = null, string? notes = null, string? otherInstruments = null);
    
    Task UpdateAttendanceAsync(int id, bool attended, InstrumentType? instrument = null);
    
    /// <summary>
    /// Cancels an attendance by setting WillAttend to false. Used for past rehearsals.
    /// </summary>
    Task CancelAttendanceAsync(int id);
    
    Task DeleteAttendanceAsync(int id);
    
    // Statistics
    Task<int> GetUserAttendanceCountAsync(string userId, DateTime startDate, DateTime endDate);
    Task<Dictionary<string, int>> GetAttendanceStatsAsync(DateTime startDate, DateTime endDate);
}
