using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Enrollment operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IEnrollmentService
{
    Task<Enrollment?> GetEnrollmentByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Enrollment>> GetAllEnrollmentsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Enrollment>> GetEnrollmentsByEventIdAsync(int eventId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Enrollment>> GetEnrollmentsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Enrollment?> GetEnrollmentByEventAndUserAsync(int eventId, string userId, CancellationToken cancellationToken = default);
    Task<Enrollment> CreateEnrollmentAsync(string userId, int eventId, InstrumentType? instrument = null, string? notes = null, bool willAttend = true, string? otherInstruments = null, bool skipNotification = false, CancellationToken cancellationToken = default);
    Task<Enrollment> UpdateEnrollmentAsync(int enrollmentId, bool willAttend, InstrumentType? instrument = null, string? notes = null, string? otherInstruments = null, CancellationToken cancellationToken = default);
    Task DeleteEnrollmentAsync(int id, CancellationToken cancellationToken = default);
}
