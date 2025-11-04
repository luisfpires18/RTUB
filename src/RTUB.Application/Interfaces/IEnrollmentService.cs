using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Enrollment operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IEnrollmentService
{
    Task<Enrollment?> GetEnrollmentByIdAsync(int id);
    Task<IEnumerable<Enrollment>> GetAllEnrollmentsAsync();
    Task<IEnumerable<Enrollment>> GetEnrollmentsByEventIdAsync(int eventId);
    Task<IEnumerable<Enrollment>> GetEnrollmentsByUserIdAsync(string userId);
    Task<Enrollment> CreateEnrollmentAsync(string userId, int eventId, bool attended = false, InstrumentType? instrument = null, string? notes = null);
    Task MarkEnrollmentAttendanceAsync(int id, bool attended);
    Task DeleteEnrollmentAsync(int id);
}
