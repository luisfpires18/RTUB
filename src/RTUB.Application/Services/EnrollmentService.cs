using Microsoft.AspNetCore.Identity;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Enrollment service implementation
/// Contains business logic for enrollment operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public partial class EnrollmentService : IEnrollmentService
{
    private readonly TimeProvider _time;

    private readonly IEnrollmentRepository _enrollmentRepository;

    private readonly IMemberStatusService _memberStatusService;

    private readonly IPushNotificationFactory _pushNotificationFactory;

    private readonly IPushNotificationService _pushNotificationService;

    private readonly UserManager<ApplicationUser> _userManager;

    public EnrollmentService(
        IEnrollmentRepository enrollmentRepository,
        IMemberStatusService memberStatusService,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _enrollmentRepository = enrollmentRepository;
        _memberStatusService = memberStatusService;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
    }

    public async Task<IEnumerable<Enrollment>> GetAllEnrollmentsAsync(CancellationToken cancellationToken = default)
    {
        return await _enrollmentRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetEnrollmentsByEventIdAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await _enrollmentRepository.GetByEventIdAsync(eventId);
    }

    public async Task<Enrollment?> GetEnrollmentByEventAndUserAsync(int eventId, string userId, CancellationToken cancellationToken = default)
    {
        return await _enrollmentRepository.GetByEventAndUserAsync(eventId, userId);
    }

    public async Task<Enrollment> CreateEnrollmentAsync(string userId, int eventId, InstrumentType? instrument = null, string? notes = null, bool willAttend = true, string? otherInstruments = null, bool skipNotification = false, CancellationToken cancellationToken = default)
    {
        var enrollment = Enrollment.Create(userId, eventId);
        enrollment.Instrument = instrument;
        enrollment.Notes = notes;
        enrollment.WillAttend = willAttend;
        enrollment.OtherInstruments = otherInstruments;
        enrollment.CategoryAtEvent = await ResolveUserPrimaryCategoryAsync(userId);
        var createdEnrollment = await _enrollmentRepository.AddAsync(enrollment);

        if (!skipNotification)
        {
            if (willAttend)
            {
                await NotifyEnrollmentAsync(createdEnrollment);
            }
            else
            {
                await NotifyNonEnrollmentAsync(createdEnrollment);
            }
        }

        return createdEnrollment;
    }

    public async Task<Enrollment> UpdateEnrollmentAsync(
       int enrollmentId,
       bool willAttend,
       InstrumentType? instrument = null,
       string? notes = null,
       string? otherInstruments = null,
       CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentRepository.GetByIdOrThrowAsync(enrollmentId);

        var wasAttending = enrollment.WillAttend;

        // Update enrollment fields
        if (enrollment.WillAttend != willAttend)
        {
            enrollment.WillAttend = willAttend;
            // Update enlist time only when attendance intent actually changes
            enrollment.EnrolledAt = DateTime.UtcNow;
        }
        enrollment.Instrument = instrument;
        enrollment.Notes = notes;
        enrollment.OtherInstruments = otherInstruments;

        await _enrollmentRepository.UpdateAsync(enrollment);

        // Update retirement status if enrollment was confirmed
        if (willAttend)
        {
            await _memberStatusService.ReconcileMemberStatusAsync(enrollment.UserId);
        }

        // Send notification if user is now attending (was not attending before)
        if (willAttend && !wasAttending)
        {
            await NotifyEnrollmentAsync(enrollment, cancellationToken);
        }
        // Send notification if user is no longer attending (was attending before)
        else if (!willAttend && wasAttending)
        {
            await NotifyCancellationAsync(enrollment, cancellationToken);
        }

        return enrollment;
    }

    public async Task DeleteEnrollmentAsync(int id, CancellationToken cancellationToken = default)
    {
        var enrollment = await _enrollmentRepository.GetByIdOrThrowAsync(id);

        // Send cancellation notification before deleting if user was attending
        if (enrollment.WillAttend)
        {
            await NotifyCancellationAsync(enrollment, cancellationToken);
        }

        await _enrollmentRepository.DeleteAsync(id);
    }
}
