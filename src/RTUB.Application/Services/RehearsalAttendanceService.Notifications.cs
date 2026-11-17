// Rehearsal attendance: approval, rejection and attendance notifications.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Configuration;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public partial class RehearsalAttendanceService
{
    private async Task SendApprovalNotificationAsync(RehearsalAttendance attendance, string approverUserId)
    {
        try
        {
            // Load the rehearsal if not already loaded
            var rehearsal = attendance.Rehearsal ?? await _attendanceRepository.QueryAsync(q => q
                .Include(a => a.Rehearsal)
                .Where(a => a.Id == attendance.Id)
                .Select(a => a.Rehearsal)
                .FirstOrDefaultAsync());

            if (rehearsal == null)
                return;

            // Get the approver's name
            var approver = await _userManager.FindByIdAsync(approverUserId);
            if (approver == null)
                return;

            var approverName = approver.Nickname ?? approver.UserName ?? "Admin";
            var notification = _pushNotificationFactory.CreateRehearsalAttendanceApprovalNotification(rehearsal, approverName);

            await _pushNotificationService.SendToUserAsync(attendance.UserId, notification);
        }
        catch
        {
            // Log error but don't fail the operation
            // Notification is secondary to the main operation
        }
    }

    private async Task SendRejectionNotificationAsync(RehearsalAttendance attendance, string rejectorUserId)
    {
        try
        {
            // Load the rehearsal if not already loaded
            var rehearsal = attendance.Rehearsal ?? await _attendanceRepository.QueryAsync(q => q
                .Include(a => a.Rehearsal)
                .Where(a => a.Id == attendance.Id)
                .Select(a => a.Rehearsal)
                .FirstOrDefaultAsync());

            if (rehearsal == null)
                return;

            // Get the rejector's name
            var rejector = await _userManager.FindByIdAsync(rejectorUserId);
            if (rejector == null)
                return;

            var rejectorName = rejector.Nickname ?? rejector.UserName ?? "Admin";
            var notification = _pushNotificationFactory.CreateRehearsalAttendanceRejectionNotification(rehearsal, rejectorName);

            await _pushNotificationService.SendToUserAsync(attendance.UserId, notification);
        }
        catch
        {
            // Log error but don't fail the operation
            // Notification is secondary to the main operation
        }
    }

    private async Task NotifyAttendanceAsync(RehearsalAttendance attendance)
    {
        try
        {
            var detailedAttendance = await _attendanceRepository.QueryAsync(q => q
                .Include(a => a.Rehearsal)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == attendance.Id));

            if (detailedAttendance?.Rehearsal == null || detailedAttendance.User == null)
            {
                return;
            }

            // Only send notifications for future rehearsals
            if (!IsFutureRehearsal(detailedAttendance.Rehearsal.Date))
            {
                return;
            }

            var userDisplayName = detailedAttendance.User.Nickname
                                  ?? detailedAttendance.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateRehearsalAttendanceNotification(
                detailedAttendance.Rehearsal,
                userDisplayName);

            var recipientIds = await _attendanceRepository.QueryAsync(q => q
                .Where(a => a.RehearsalId == detailedAttendance.RehearsalId
                            && a.WillAttend
                            && a.UserId != detailedAttendance.UserId
                            && !string.IsNullOrEmpty(a.UserId))
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync());

            if (recipientIds.Count == 0)
            {
                return;
            }

            await _pushNotificationService.SendToSelectedUsersAsync(recipientIds, notification);
        }
        catch (Exception ex)
        {
            // Notifications are non-critical; log but don't fail the operation
            _logger.LogError(ex, "Failed to send rehearsal attendance notification");
        }
    }

    private async Task NotifyCancellationAsync(RehearsalAttendance attendance)
    {
        try
        {
            var detailedAttendance = await _attendanceRepository.QueryAsync(q => q
                .Include(a => a.Rehearsal)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == attendance.Id));

            if (detailedAttendance?.Rehearsal == null || detailedAttendance.User == null)
            {
                return;
            }

            // Only send notifications for future rehearsals
            if (!IsFutureRehearsal(detailedAttendance.Rehearsal.Date))
            {
                return;
            }

            var userDisplayName = detailedAttendance.User.Nickname
                                  ?? detailedAttendance.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateRehearsalCancellationNotification(
                detailedAttendance.Rehearsal,
                userDisplayName);

            var recipientIds = await _attendanceRepository.QueryAsync(q => q
                .Where(a => a.RehearsalId == detailedAttendance.RehearsalId
                            && a.WillAttend
                            && a.UserId != detailedAttendance.UserId
                            && !string.IsNullOrEmpty(a.UserId))
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync());

            if (recipientIds.Count == 0)
            {
                return;
            }

            await _pushNotificationService.SendToSelectedUsersAsync(recipientIds, notification);
        }
        catch (Exception ex)
        {
            // Notifications are non-critical; log but don't fail the operation
            _logger.LogError(ex, "Failed to send rehearsal cancellation notification");
        }
    }

    private async Task NotifyNonAttendanceAsync(RehearsalAttendance attendance)
    {
        try
        {
            var detailedAttendance = await _attendanceRepository.QueryAsync(q => q
                .Include(a => a.Rehearsal)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == attendance.Id));

            if (detailedAttendance?.Rehearsal == null || detailedAttendance.User == null)
            {
                return;
            }

            // Only send notifications for future rehearsals
            if (!IsFutureRehearsal(detailedAttendance.Rehearsal.Date))
            {
                return;
            }

            var userDisplayName = detailedAttendance.User.Nickname
                                  ?? detailedAttendance.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateRehearsalNonAttendanceNotification(
                detailedAttendance.Rehearsal,
                userDisplayName);

            var recipientIds = await _attendanceRepository.QueryAsync(q => q
                .Where(a => a.RehearsalId == detailedAttendance.RehearsalId
                            && a.WillAttend
                            && a.UserId != detailedAttendance.UserId
                            && !string.IsNullOrEmpty(a.UserId))
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync());

            if (recipientIds.Count == 0)
            {
                return;
            }

            await _pushNotificationService.SendToSelectedUsersAsync(recipientIds, notification);
        }
        catch (Exception ex)
        {
            // Notifications are non-critical; log but don't fail the operation
            _logger.LogError(ex, "Failed to send rehearsal non-attendance notification");
        }
    }
}
