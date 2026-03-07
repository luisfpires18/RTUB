using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Rehearsal attendance service implementation
/// Contains business logic for attendance tracking
/// </summary>
public class RehearsalAttendanceService : IRehearsalAttendanceService
{
    private readonly IRehearsalAttendanceRepository _attendanceRepository;
    private readonly IRetirementStatusService _retirementStatusService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RehearsalAttendanceService> _logger;

    public RehearsalAttendanceService(
        IRehearsalAttendanceRepository attendanceRepository,
        IRetirementStatusService retirementStatusService,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager,
        ILogger<RehearsalAttendanceService> logger)
    {
        _attendanceRepository = attendanceRepository;
        _retirementStatusService = retirementStatusService;
        _pushNotificationService = pushNotificationService;
        _pushNotificationFactory = pushNotificationFactory;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<RehearsalAttendance?> GetAttendanceByIdAsync(int id)
    {
        var attendance = await _attendanceRepository.GetByIdAsync(id);
        if (attendance != null)
        {
            // Load the rehearsal navigation property
            attendance = await _attendanceRepository.QueryAsync(q => q
                .Include(a => a.Rehearsal)
                .FirstOrDefaultAsync(a => a.Id == id));
        }
        return attendance;
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdAsync(int rehearsalId)
    {
        return await _attendanceRepository.GetAttendancesByRehearsalIdAsync(rehearsalId);
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdsAsync(IEnumerable<int> rehearsalIds)
    {
        return await _attendanceRepository.GetAttendancesByRehearsalIdsAsync(rehearsalIds);
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByUserIdAsync(string userId)
    {
        return await _attendanceRepository.GetAttendancesByUserIdAsync(userId);
    }

    public async Task<RehearsalAttendance> MarkAttendanceAsync(int rehearsalId, string userId, bool willAttend = true, InstrumentType? instrument = null, string? notes = null, string? otherInstruments = null, bool skipNotification = false)
    {
        // Check if attendance already exists
        var existing = await _attendanceRepository.GetAttendanceByRehearsalAndUserAsync(rehearsalId, userId);

        if (existing != null)
        {
            // Track if this is a change from not attending to attending
            var wasNotAttending = !existing.WillAttend;
            var wasAttending = existing.WillAttend;

            // Update existing attendance
            if (existing.WillAttend != willAttend)
            {
                existing.WillAttend = willAttend;
                // Update enlist/check-in time only when attendance intent actually changes
                existing.CheckedInAt = DateTime.UtcNow;
            }
            // Always update instrument (including setting to null to clear it)
            existing.UpdateInstrument(instrument);
            // Always update notes, even if empty (allows clearing notes)
            existing.Notes = notes;
            // Update other instruments
            existing.OtherInstruments = otherInstruments;

            await _attendanceRepository.UpdateAsync(existing);

            // Send notification if user changed from not attending to attending
            if (willAttend && wasNotAttending && !skipNotification)
            {
                await NotifyAttendanceAsync(existing);
            }
            // Send notification if user changed from attending to not attending
            else if (!willAttend && wasAttending && !skipNotification)
            {
                await NotifyCancellationAsync(existing);
            }

            return existing;
        }

        // Create new attendance (defaults to pending - Attended = false)
        var attendance = RehearsalAttendance.Create(rehearsalId, userId, instrument);
        attendance.WillAttend = willAttend;
        // Always set notes, even if empty
        attendance.Notes = notes;
        // Set other instruments
        attendance.OtherInstruments = otherInstruments;

        var createdAttendance = await _attendanceRepository.AddAsync(attendance);

        // Send notification to other attendees if user is attending
        if (willAttend && !skipNotification)
        {
            await NotifyAttendanceAsync(createdAttendance);
        }
        // Send notification to other attendees if user is not attending
        else if (!willAttend && !skipNotification)
        {
            await NotifyNonAttendanceAsync(createdAttendance);
        }

        return createdAttendance;
    }

    public async Task<RehearsalAttendance> CreateAttendanceWithApprovalAsync(int rehearsalId, string userId, InstrumentType? instrument = null, string? notes = null, string? otherInstruments = null, bool skipNotification = true)
    {
        // Check if attendance already exists
        var existing = await _attendanceRepository.GetAttendanceByRehearsalAndUserAsync(rehearsalId, userId);

        if (existing != null)
        {
            // Update existing attendance to approved
            existing.WillAttend = true;
            existing.MarkAttendance(true); // Set Attended = true
            if (instrument.HasValue)
                existing.UpdateInstrument(instrument);
            existing.Notes = notes;
            existing.OtherInstruments = otherInstruments;

            await _attendanceRepository.UpdateAsync(existing);
            return existing;
        }

        // Create new attendance with immediate approval (Attended = true)
        var attendance = RehearsalAttendance.Create(rehearsalId, userId, instrument);
        attendance.WillAttend = true;
        attendance.MarkAttendance(true); // Set Attended = true immediately
        attendance.Notes = notes;
        attendance.OtherInstruments = otherInstruments;

        return await _attendanceRepository.AddAsync(attendance);
        // Note: This method is designed for admin use and inherently does not send notifications
        // to other attendees. The skipNotification parameter is included for API consistency with
        // other attendance/enrollment creation methods but does not affect this method's behavior
        // since it never sends notifications.
    }

    public async Task UpdateAttendanceAsync(int id, bool attended, InstrumentType? instrument = null, string? approverUserId = null)
    {
        var attendance = await _attendanceRepository.GetByIdOrThrowAsync(id);

        attendance.MarkAttendance(attended);
        if (instrument.HasValue)
            attendance.UpdateInstrument(instrument);

        await _attendanceRepository.UpdateAsync(attendance);

        // Update retirement status if attendance was marked as true
        if (attended)
        {
            await _retirementStatusService.UpdateUserRetirementStatusAsync(attendance.UserId);

            // Send approval notification if approverUserId is provided
            if (!string.IsNullOrEmpty(approverUserId))
            {
                await SendApprovalNotificationAsync(attendance, approverUserId);
            }
        }
    }

    public async Task CancelAttendanceAsync(int id, string? rejectorUserId = null)
    {
        var attendance = await _attendanceRepository.GetByIdOrThrowAsync(id);

        // Check if user was attending before canceling
        var wasAttending = attendance.WillAttend;

        // Set WillAttend to false to cancel the attendance
        attendance.WillAttend = false;

        await _attendanceRepository.UpdateAsync(attendance);

        // Send rejection notification if rejectorUserId is provided
        if (!string.IsNullOrEmpty(rejectorUserId))
        {
            await SendRejectionNotificationAsync(attendance, rejectorUserId);
        }
        // Send cancellation notification to other attendees if user was attending
        else if (wasAttending)
        {
            await NotifyCancellationAsync(attendance);
        }
    }

    public async Task DeleteAttendanceAsync(int id)
    {
        var attendance = await _attendanceRepository.GetByIdOrThrowAsync(id);

        // Send cancellation notification before deleting if user was attending
        if (attendance.WillAttend)
        {
            await NotifyCancellationAsync(attendance);
        }

        await _attendanceRepository.DeleteAsync(id);
    }

    public async Task<int> GetUserAttendanceCountAsync(string userId, DateTime startDate, DateTime endDate)
    {
        return await _attendanceRepository.QueryAsync(q => q
            .Include(a => a.Rehearsal)
            .Where(a => a.UserId == userId &&
                       a.Attended &&
                       a.Rehearsal!.Date >= startDate.Date &&
                       a.Rehearsal.Date <= endDate.Date)
            .CountAsync());
    }

    public async Task<Dictionary<string, int>> GetAttendanceStatsAsync(DateTime startDate, DateTime endDate)
    {
        var attendances = await _attendanceRepository.QueryAsync(q => q
            .Include(a => a.Rehearsal)
            .Include(a => a.User)
            .Where(a => a.Attended &&
                       a.Rehearsal!.Date >= startDate.Date &&
                       a.Rehearsal.Date <= endDate.Date)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync());

        return attendances.ToDictionary(a => a.UserId, a => a.Count);
    }

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
            var baseUrl = GetBaseUrl();
            var notification = _pushNotificationFactory.CreateRehearsalAttendanceApprovalNotification(rehearsal, approverName, baseUrl);

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
            var baseUrl = GetBaseUrl();
            var notification = _pushNotificationFactory.CreateRehearsalAttendanceRejectionNotification(rehearsal, rejectorName, baseUrl);

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

            var baseUrl = GetBaseUrl();
            var userDisplayName = detailedAttendance.User.Nickname
                                  ?? detailedAttendance.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateRehearsalAttendanceNotification(
                detailedAttendance.Rehearsal,
                userDisplayName,
                baseUrl);

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

            var baseUrl = GetBaseUrl();
            var userDisplayName = detailedAttendance.User.Nickname
                                  ?? detailedAttendance.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateRehearsalCancellationNotification(
                detailedAttendance.Rehearsal,
                userDisplayName,
                baseUrl);

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

            var baseUrl = GetBaseUrl();
            var userDisplayName = detailedAttendance.User.Nickname
                                  ?? detailedAttendance.User.FirstName
                                  ?? "Utilizador";

            var notification = _pushNotificationFactory.CreateRehearsalNonAttendanceNotification(
                detailedAttendance.Rehearsal,
                userDisplayName,
                baseUrl);

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

    /// <summary>
    /// Checks if the rehearsal date is strictly in the future (tomorrow or later).
    /// Same-day rehearsals are excluded because attendance needs admin approval,
    /// so broadcasting "vai ao ensaio" notifications is premature.
    /// </summary>
    private static bool IsFutureRehearsal(DateTime rehearsalDate)
    {
        return rehearsalDate.Date > DateTime.Today;
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
