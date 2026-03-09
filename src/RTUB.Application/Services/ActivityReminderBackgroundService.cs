using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that sends scheduled reminders for events, rehearsals and meetings.
/// - Events: day-before and day-of reminders for non-retired users. Festival events also remind 15 days and 7 days before.
/// - Rehearsals: same-day reminders only, for non-retired users.
/// - Meetings: day-before and day-of reminders at 11:30 UTC, for eligible non-retired users only (CV, AG, Direção rules).
/// </summary>
public class ActivityReminderBackgroundService : BackgroundService
{
    private readonly ILogger<ActivityReminderBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ActivityReminderOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 25;

    public ActivityReminderBackgroundService(
        ILogger<ActivityReminderBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ActivityReminderOptions> options)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit before starting to allow the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken);

        _logger.LogInformation(
            "Activity reminder service started. Will check daily at {ScheduledTime} UTC. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next activity reminder check scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                if (_options.Enabled)
                {
                    await CheckAndSendRemindersAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in activity reminder service");
            }
        }
    }

    private async Task CheckAndSendRemindersAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Only run once per day
        if (_lastRunDate == today)
        {
            _logger.LogDebug("Activity reminders already sent today, skipping");
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var pushNotificationFactory = scope.ServiceProvider.GetRequiredService<IPushNotificationFactory>();

        try
        {
            var baseUrl = "/";

            // Load non-retired users once
            var activeUsers = await context.Users
                .AsNoTracking()
                .Where(u => !u.IsRetired)
                .ToListAsync(cancellationToken);

            if (!activeUsers.Any())
            {
                _logger.LogInformation("No active (non-retired) users found for activity reminders.");
                _lastRunDate = today;
                return;
            }

            await SendEventRemindersAsync(context, pushNotificationService, pushNotificationFactory, activeUsers, baseUrl, today, cancellationToken);
            await SendRehearsalRemindersAsync(context, pushNotificationService, pushNotificationFactory, activeUsers, baseUrl, today, cancellationToken);
            await SendMeetingRemindersAsync(context, pushNotificationService, pushNotificationFactory, activeUsers, baseUrl, today, cancellationToken);

            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for activity reminders");
        }
    }

    private async Task SendEventRemindersAsync(
        ApplicationDbContext context,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        List<ApplicationUser> activeUsers,
        string baseUrl,
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
                notification = pushNotificationFactory.CreateEventNotification(@event, isReminder: true, baseUrl);
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
        string baseUrl,
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
                notification = pushNotificationFactory.CreateRehearsalReminderNotification(rehearsal, baseUrl);
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
        string baseUrl,
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

        foreach (var meeting in upcomingMeetings)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            SendPushNotificationDto notification;
            try
            {
                notification = pushNotificationFactory.CreateMeetingNotification(meeting, isReminder: true, baseUrl);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Skipping meeting {MeetingId} for reminder due to validation error", meeting.Id);
                continue;
            }

            // Use WeeklyNotificationBackgroundService visibility logic to determine eligibility
            var eligibleRecipientIds = activeUsers
                .Where(u => WeeklyNotificationBackgroundService.CanUserSeeMeeting(meeting, u))
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

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var scheduledTime = _options.ScheduledTime;

        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid activity reminder scheduled time format: {ScheduledTime}. Using default 11:30", scheduledTime);
            timeOfDay = new TimeSpan(11, 30, 0);
        }

        var nextRun = now.Date.Add(timeOfDay);
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun;
    }
}
