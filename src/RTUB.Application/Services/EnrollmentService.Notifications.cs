// Enrollments: enrollment, cancellation and non-enrollment notifications.
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public partial class EnrollmentService
{
    private async Task NotifyEnrollmentAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        try
        {
            var detailedEnrollment = await _enrollmentRepository.QueryAsync(q => q
                .Include(e => e.Event)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == enrollment.Id, cancellationToken), cancellationToken);

            if (detailedEnrollment?.Event == null || detailedEnrollment.User == null)
            {
                return;
            }

            // Only send notifications for future events
            if (!IsFutureEvent(detailedEnrollment.Event.Date))
            {
                return;
            }

            var userDisplayName = detailedEnrollment.User.Nickname
                                  ?? detailedEnrollment.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateEventEnrollmentNotification(
                detailedEnrollment.Event,
                userDisplayName);

            var recipientIds = await _enrollmentRepository.QueryAsync(q => q
                .Where(e => e.EventId == detailedEnrollment.EventId
                            && e.WillAttend
                            && e.UserId != detailedEnrollment.UserId
                            && !string.IsNullOrEmpty(e.UserId))
                .Select(e => e.UserId)
                .Distinct()
                .ToListAsync(cancellationToken), cancellationToken);

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

    private async Task NotifyCancellationAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        try
        {
            var detailedEnrollment = await _enrollmentRepository.QueryAsync(q => q
                .Include(e => e.Event)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == enrollment.Id, cancellationToken), cancellationToken);

            if (detailedEnrollment?.Event == null || detailedEnrollment.User == null)
            {
                return;
            }

            // Only send notifications for future events
            if (!IsFutureEvent(detailedEnrollment.Event.Date))
            {
                return;
            }

            var userDisplayName = detailedEnrollment.User.Nickname
                                  ?? detailedEnrollment.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateEventCancellationNotification(
                detailedEnrollment.Event,
                userDisplayName);

            var recipientIds = await _enrollmentRepository.QueryAsync(q => q
                .Where(e => e.EventId == detailedEnrollment.EventId
                            && e.WillAttend
                            && e.UserId != detailedEnrollment.UserId
                            && !string.IsNullOrEmpty(e.UserId))
                .Select(e => e.UserId)
                .Distinct()
                .ToListAsync(cancellationToken), cancellationToken);

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

    private async Task NotifyNonEnrollmentAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        try
        {
            var detailedEnrollment = await _enrollmentRepository.QueryAsync(q => q
                .Include(e => e.Event)
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == enrollment.Id, cancellationToken), cancellationToken);

            if (detailedEnrollment?.Event == null || detailedEnrollment.User == null)
            {
                return;
            }

            // Only send notifications for future events
            if (!IsFutureEvent(detailedEnrollment.Event.Date))
            {
                return;
            }

            var userDisplayName = detailedEnrollment.User.Nickname
                                  ?? detailedEnrollment.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateEventNonEnrollmentNotification(
                detailedEnrollment.Event,
                userDisplayName);

            var recipientIds = await _enrollmentRepository.QueryAsync(q => q
                .Where(e => e.EventId == detailedEnrollment.EventId
                            && e.WillAttend
                            && e.UserId != detailedEnrollment.UserId
                            && !string.IsNullOrEmpty(e.UserId))
                .Select(e => e.UserId)
                .Distinct()
                .ToListAsync(cancellationToken), cancellationToken);

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

    /// <summary>
    /// Checks if the event date is in the future (today or later)
    /// </summary>
    private bool IsFutureEvent(DateTime eventDate)
    {
        return eventDate.Date >= PortugalTime.Today(_time);
    }


    private async Task<MemberCategory?> ResolveUserPrimaryCategoryAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user?.GetPrimaryCategory();
    }
}
