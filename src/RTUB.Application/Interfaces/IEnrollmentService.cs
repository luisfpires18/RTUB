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
    Task<Enrollment> CreateEnrollmentAsync(string userId, int eventId, InstrumentType? instrument = null, string? notes = null, bool willAttend = true);
    Task DeleteEnrollmentAsync(int id);
}
