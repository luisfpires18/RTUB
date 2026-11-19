using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that sends scheduled reminders for events, rehearsals and meetings.
/// - Events: day-before and day-of reminders for non-retired users. Festival events also remind 15 days and 7 days before.
/// - Rehearsals: same-day reminders only, for non-retired users.
/// - Meetings: day-before and day-of reminders at 11:30 UTC, for eligible non-retired users only (CV, AG, Direção rules).
/// </summary>
public partial class ActivityReminderBackgroundService : BackgroundService
{
    private readonly TimeProvider _time;

    private readonly ILogger<ActivityReminderBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ActivityReminderOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 25;

    public ActivityReminderBackgroundService(
        ILogger<ActivityReminderBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ActivityReminderOptions> options,
        TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit before starting to allow the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken);

        _logger.LogInformation(
            "Activity reminder service started. Will check daily at {ScheduledTime} Portugal time. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - _time.GetUtcNow().UtcDateTime;

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
        var today = PortugalTime.Today(_time);

        // Only run once per day
        if (_lastRunDate == today)
        {
            _logger.LogDebug("Activity reminders already sent today, skipping");
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        await using var context = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var pushNotificationFactory = scope.ServiceProvider.GetRequiredService<IPushNotificationFactory>();

        try
        {
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

            await SendEventRemindersAsync(context, pushNotificationService, pushNotificationFactory, activeUsers, today, cancellationToken);
            await SendRehearsalRemindersAsync(context, pushNotificationService, pushNotificationFactory, activeUsers, today, cancellationToken);
            await SendMeetingRemindersAsync(context, pushNotificationService, pushNotificationFactory, activeUsers, today, cancellationToken);

            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for activity reminders");
        }
    }

    private DateTime CalculateNextRunTime()
    {
        var scheduledTime = _options.ScheduledTime;

        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid activity reminder scheduled time format: {ScheduledTime}. Using default 11:30", scheduledTime);
            timeOfDay = new TimeSpan(11, 30, 0);
        }

        // The configured time is Portugal's clock, summer and winter.
        return PortugalTime.NextUtc(_time, timeOfDay);
    }
}
