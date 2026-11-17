using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically sends weekly push notifications on Monday at configured time.
/// Sends a summary of all events, rehearsals, and meetings for the current week (Monday-Sunday), then the
/// "Novo 1º Lugar no Ranking" push when the all-time leaderboard has a new leader since last week (task 058).
/// </summary>
public partial class WeeklyNotificationBackgroundService : BackgroundService
{
    private readonly TimeProvider _time;

    private readonly ILogger<WeeklyNotificationBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WeeklyNotificationOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 20;

    public WeeklyNotificationBackgroundService(
        ILogger<WeeklyNotificationBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<WeeklyNotificationOptions> options,
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
            "Weekly notification service started. Will check every Monday at {ScheduledTime} Portugal time. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - _time.GetUtcNow().UtcDateTime;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next weekly notification check scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                if (_options.Enabled)
                {
                    await CheckAndSendWeeklyNotificationsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in weekly notification service");
            }
        }
    }

    private DateTime CalculateNextRunTime()
    {
        var scheduledTime = _options.ScheduledTime;

        // Parse the scheduled time (format: "HH:mm")
        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid scheduled time format: {ScheduledTime}. Using default 09:00", scheduledTime);
            timeOfDay = new TimeSpan(9, 0, 0); // Default to 9 AM
        }

        // The configured time is Portugal's clock, summer and winter (#226 F02; was UTC).
        return PortugalTime.NextUtc(_time, timeOfDay, DayOfWeek.Monday);
    }
}
