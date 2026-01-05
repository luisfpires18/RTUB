using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Enrollment service implementation
/// Contains business logic for enrollment operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class EnrollmentService : IEnrollmentService
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IRetirementStatusService _retirementStatusService;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnrollmentService(
        IEnrollmentRepository enrollmentRepository,
        IRetirementStatusService retirementStatusService,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _enrollmentRepository = enrollmentRepository;
        _retirementStatusService = retirementStatusService;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Enrollment?> GetEnrollmentByIdAsync(int id)
    {
        return await _enrollmentRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Enrollment>> GetAllEnrollmentsAsync()
    {
        return await _enrollmentRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetEnrollmentsByEventIdAsync(int eventId)
    {
        return await _enrollmentRepository.GetByEventIdAsync(eventId);
    }

    public async Task<IEnumerable<Enrollment>> GetEnrollmentsByUserIdAsync(string userId)
    {
        return await _enrollmentRepository.GetByUserIdAsync(userId);
    }

    public async Task<Enrollment> CreateEnrollmentAsync(string userId, int eventId, InstrumentType? instrument = null, string? notes = null, bool willAttend = true, string? otherInstruments = null, bool skipNotification = false)
    {
        var enrollment = Enrollment.Create(userId, eventId);
        enrollment.Instrument = instrument;
        enrollment.Notes = notes;
        enrollment.WillAttend = willAttend;
        enrollment.OtherInstruments = otherInstruments;
        var createdEnrollment = await _enrollmentRepository.AddAsync(enrollment);

        if (willAttend && !skipNotification)
        {
            await NotifyEnrollmentAsync(createdEnrollment);
        }

        return createdEnrollment;
    }

    public async Task<Enrollment> UpdateEnrollmentAsync(
       int enrollmentId,
       bool willAttend,
       InstrumentType? instrument = null,
       string? notes = null,
       string? otherInstruments = null)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(enrollmentId);

        if (enrollment == null)
        {
            throw new EntityNotFoundException(nameof(Enrollment), enrollmentId);
        }

        var wasAttending = enrollment.WillAttend;

        // Update enrollment fields
        enrollment.WillAttend = willAttend;
        enrollment.Instrument = instrument;
        enrollment.Notes = notes;
        enrollment.OtherInstruments = otherInstruments;

        await _enrollmentRepository.UpdateAsync(enrollment);

        // Update retirement status if enrollment was confirmed
        if (willAttend)
        {
            await _retirementStatusService.UpdateUserRetirementStatusAsync(enrollment.UserId);
        }

        // Send notification if user is now attending (was not attending before)
        if (willAttend && !wasAttending)
        {
            await NotifyEnrollmentAsync(enrollment);
        }
        // Send notification if user is no longer attending (was attending before)
        else if (!willAttend && wasAttending)
        {
            await NotifyCancellationAsync(enrollment);
        }

        return enrollment;
    }

    public async Task DeleteEnrollmentAsync(int id)
    {
        var enrollment = await _enrollmentRepository.GetByIdAsync(id);
        if (enrollment == null)
            throw new EntityNotFoundException(nameof(Enrollment), id);

        // Send cancellation notification before deleting if user was attending
        if (enrollment.WillAttend)
        {
            await NotifyCancellationAsync(enrollment);
        }

        await _enrollmentRepository.DeleteAsync(id);
    }

    private async Task NotifyEnrollmentAsync(Enrollment enrollment)
    {
        try
        {
            var detailedEnrollment = await _enrollmentRepository.Query()
                .AsNoTracking()
                .Include(e => e.Event)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == enrollment.Id);

            if (detailedEnrollment?.Event == null || detailedEnrollment.User == null)
            {
                return;
            }

            var baseUrl = GetBaseUrl();
            var userDisplayName = detailedEnrollment.User.Nickname
                                  ?? detailedEnrollment.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateEventEnrollmentNotification(
                detailedEnrollment.Event,
                userDisplayName,
                baseUrl);

            var recipientIds = await _enrollmentRepository.Query()
                .AsNoTracking()
                .Where(e => e.EventId == detailedEnrollment.EventId
                            && e.WillAttend
                            && e.UserId != detailedEnrollment.UserId
                            && !string.IsNullOrEmpty(e.UserId))
                .Select(e => e.UserId)
                .Distinct()
                .ToListAsync();

            if (recipientIds.Count == 0)
            {
                return;
            }

            await _pushNotificationService.SendToSelectedUsersAsync(recipientIds, notification);
        }
        catch
        {
            // Notifications are non-critical; ignore failures
        }
    }

    private async Task NotifyCancellationAsync(Enrollment enrollment)
    {
        try
        {
            var detailedEnrollment = await _enrollmentRepository.Query()
                .AsNoTracking()
                .Include(e => e.Event)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == enrollment.Id);

            if (detailedEnrollment?.Event == null || detailedEnrollment.User == null)
            {
                return;
            }

            var baseUrl = GetBaseUrl();
            var userDisplayName = detailedEnrollment.User.Nickname
                                  ?? detailedEnrollment.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateEventCancellationNotification(
                detailedEnrollment.Event,
                userDisplayName,
                baseUrl);

            var recipientIds = await _enrollmentRepository.Query()
                .AsNoTracking()
                .Where(e => e.EventId == detailedEnrollment.EventId
                            && e.WillAttend
                            && e.UserId != detailedEnrollment.UserId
                            && !string.IsNullOrEmpty(e.UserId))
                .Select(e => e.UserId)
                .Distinct()
                .ToListAsync();

            if (recipientIds.Count == 0)
            {
                return;
            }

            await _pushNotificationService.SendToSelectedUsersAsync(recipientIds, notification);
        }
        catch
        {
            // Notifications are non-critical; ignore failures
        }
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "https://rtub.pt"; // Fallback
    }
}
