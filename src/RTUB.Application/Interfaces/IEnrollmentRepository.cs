using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Enrollment entity
/// </summary>
public interface IEnrollmentRepository : IRepository<Enrollment>
{
    /// <summary>
    /// Get enrollments by event ID with User included
    /// </summary>
    Task<IEnumerable<Enrollment>> GetByEventIdAsync(int eventId);

    /// <summary>
    /// Get enrollments by user ID with Event included
    /// </summary>
    Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Get enrollment by event ID and user ID
    /// </summary>
    Task<Enrollment?> GetByEventAndUserAsync(int eventId, string userId);

    /// <summary>
    /// Get enrollments by attendance status
    /// </summary>
    Task<IEnumerable<Enrollment>> GetByAttendanceAsync(bool willAttend);

    /// <summary>
    /// Delete all enrollments for a specific event (batch operation)
    /// </summary>
    Task DeleteByEventIdAsync(int eventId);
}
