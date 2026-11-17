using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Activity reminders: events, rehearsals and meetings.

public partial class ActivityReminderBackgroundService
{
    private async Task SendEventRemindersAsync(
        ApplicationDbContext context,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        List<ApplicationUser> activeUsers,
        DateTime today,
        CancellationToken cancellationToken)
    {
        // Load all events in a wider window to filter for Festival-specific reminders
        var startDate = today;
        var endDate = today.AddDays(16); // Up to 15 days ahead for Festival reminders

        var allUpcomingEvents = await context.Events
            .AsNoTracking()
            .Where(e => !e.IsCancelled && e.Date.Date >= startDate && e.Date.Date <= endDate)
            .ToListAsync(cancellationToken);

        if (!allUpcomingEvents.Any())
        {
            _logger.LogInformation(
                "No events found that require reminders between {StartDate} and {EndDate}",
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"));
            return;
        }

        // Filter events: only those matching today/tomorrow for non-Festival, or special Festival dates
        var eventsToRemind = allUpcomingEvents.Where(@event =>
        {
            var daysUntilEvent = (@event.Date.Date - today).Days;

            if (@event.Type == EventType.Festival)
            {
                // Festival: remind on days 15, 7, 1 (day-before), and 0 (day-of)
                return daysUntilEvent == 15 || daysUntilEvent == 7 || daysUntilEvent == 1 || daysUntilEvent == 0;
            }
            else
            {
                // Non-Festival: remind on day-before (1) and day-of (0)
                return daysUntilEvent == 1 || daysUntilEvent == 0;
            }
        }).ToList();

        if (!eventsToRemind.Any())
        {
            _logger.LogInformation("No events found that require reminders for today {Today}", today.ToString("yyyy-MM-dd"));
            return;
        }

        foreach (var @event in eventsToRemind)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            SendPushNotificationDto notification;
            try
            {
                notification = pushNotificationFactory.CreateEventNotification(@event, isReminder: true);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Skipping event {EventId} for reminder due to validation error", @event.Id);
                continue;
            }

            var userIds = activeUsers.Select(u => u.Id).ToList();
            await pushNotificationService.SendToSelectedUsersAsync(userIds, notification);

            _logger.LogInformation(
                "Sent event reminder for event {EventId} ('{Name}') to {RecipientCount} active users",
                @event.Id,
                @event.Name,
                userIds.Count);
        }
    }

    private async Task SendRehearsalRemindersAsync(
        ApplicationDbContext context,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        List<ApplicationUser> activeUsers,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var targetDates = new[] { today };

        var upcomingRehearsals = await context.Rehearsals
            .AsNoTracking()
            .Where(r => !r.IsCanceled && targetDates.Contains(r.Date.Date))
            .ToListAsync(cancellationToken);

        if (!upcomingRehearsals.Any())
        {
            _logger.LogInformation("No rehearsals found that require same-day reminders on {Today}", today.ToString("yyyy-MM-dd"));
            return;
        }

        foreach (var rehearsal in upcomingRehearsals)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            SendPushNotificationDto notification;
            try
            {
                notification = pushNotificationFactory.CreateRehearsalReminderNotification(rehearsal);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Skipping rehearsal {RehearsalId} for reminder due to validation error", rehearsal.Id);
                continue;
            }

            var userIds = activeUsers.Select(u => u.Id).ToList();
            await pushNotificationService.SendToSelectedUsersAsync(userIds, notification);

            _logger.LogInformation(
                "Sent rehearsal reminder for rehearsal {RehearsalId} on {Date} to {RecipientCount} active users",
                rehearsal.Id,
                rehearsal.Date.ToString("yyyy-MM-dd"),
                userIds.Count);
        }
    }

    private async Task SendMeetingRemindersAsync(
        ApplicationDbContext context,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        List<ApplicationUser> activeUsers,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var targetDates = new[] { today, today.AddDays(1) };

        var upcomingMeetings = await context.Meetings
            .AsNoTracking()
            .Where(m => !m.IsCancelled && targetDates.Contains(m.Date.Date))
            .ToListAsync(cancellationToken);

        if (!upcomingMeetings.Any())
        {
            _logger.LogInformation("No meetings found that require day-before/day-of reminders on {Today}", today.ToString("yyyy-MM-dd"));
            return;
        }

        // Pre-load Admin user IDs for Direção meeting visibility check
        var adminRoleId = await context.Roles
            .Where(r => r.Name == "Admin")
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var adminUserIds = adminRoleId != null
            ? (await context.UserRoles
                .Where(ur => ur.RoleId == adminRoleId)
                .Select(ur => ur.UserId)
                .ToListAsync(cancellationToken))
                .ToHashSet()
            : new HashSet<string>();

        foreach (var meeting in upcomingMeetings)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            SendPushNotificationDto notification;
            try
            {
                notification = pushNotificationFactory.CreateMeetingNotification(meeting, isReminder: true);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Skipping meeting {MeetingId} for reminder due to validation error", meeting.Id);
                continue;
            }

            // Use WeeklyNotificationBackgroundService visibility logic to determine eligibility
            var eligibleRecipientIds = activeUsers
                .Where(u => WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, u, adminUserIds.Contains(u.Id)))
                .Select(u => u.Id)
                .ToList();

            if (!eligibleRecipientIds.Any())
            {
                _logger.LogInformation(
                    "No eligible recipients for meeting {MeetingId} on {Date}; skipping reminder",
                    meeting.Id,
                    meeting.Date.ToString("yyyy-MM-dd"));
                continue;
            }

            await pushNotificationService.SendToSelectedUsersAsync(eligibleRecipientIds, notification);

            _logger.LogInformation(
                "Sent meeting reminder for meeting {MeetingId} ('{Title}') to {RecipientCount} eligible active users",
                meeting.Id,
                meeting.Title,
                eligibleRecipientIds.Count);
        }
    }
}
