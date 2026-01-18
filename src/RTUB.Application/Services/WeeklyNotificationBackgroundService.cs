using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically sends weekly push notifications on Monday at configured time.
/// Sends a summary of all events, rehearsals, and meetings for the current week (Monday-Sunday).
/// </summary>
public class WeeklyNotificationBackgroundService : BackgroundService
{
    private readonly ILogger<WeeklyNotificationBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WeeklyNotificationOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 20;

    public WeeklyNotificationBackgroundService(
        ILogger<WeeklyNotificationBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<WeeklyNotificationOptions> options)
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
            "Weekly notification service started. Will check every Monday at {ScheduledTime} UTC. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

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

    private async Task CheckAndSendWeeklyNotificationsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Only run once per day (even though we only schedule for Mondays)
        if (_lastRunDate == today)
        {
            _logger.LogDebug("Weekly notifications already sent today, skipping");
            return;
        }

        // Double-check it's Monday
        if (today.DayOfWeek != DayOfWeek.Monday)
        {
            _logger.LogWarning("Weekly notification triggered on {DayOfWeek} instead of Monday, skipping", today.DayOfWeek);
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var pushNotificationFactory = scope.ServiceProvider.GetRequiredService<IPushNotificationFactory>();

        try
        {
            // Calculate current week range (Monday to Sunday)
            var weekStart = today; // Today is Monday
            var weekEnd = today.AddDays(6); // Sunday

            _logger.LogInformation(
                "Checking for events, rehearsals, and meetings for week {WeekStart} to {WeekEnd}",
                weekStart.ToString("yyyy-MM-dd"),
                weekEnd.ToString("yyyy-MM-dd"));

            // Get all events for the current week
            var events = await context.Events
                .AsNoTracking()
                .Where(e => e.Date >= weekStart && e.Date <= weekEnd && !e.IsCancelled)
                .ToListAsync(cancellationToken);

            // Get all rehearsals for the current week
            var rehearsals = await context.Rehearsals
                .AsNoTracking()
                .Where(r => r.Date >= weekStart && r.Date <= weekEnd && !r.IsCancelled)
                .ToListAsync(cancellationToken);

            // Get all meetings for the current week
            var meetings = await context.Meetings
                .AsNoTracking()
                .Where(m => m.Date >= weekStart && m.Date <= weekEnd && !m.IsCancelled)
                .ToListAsync(cancellationToken);

            var eventCount = events.Count;
            var rehearsalCount = rehearsals.Count;
            var meetingCount = meetings.Count;

            _logger.LogInformation(
                "Found {EventCount} events, {RehearsalCount} rehearsals, {MeetingCount} meetings for the week",
                eventCount, rehearsalCount, meetingCount);

            // Create notification with summary
            var baseUrl = "https://rtub.pt"; // Fallback URL since we don't have HttpContext
            var notification = pushNotificationFactory.CreateWeeklySummaryNotification(
                eventCount, rehearsalCount, meetingCount, baseUrl);

            // Send to all users
            var allUsers = await context.Users
                .AsNoTracking()
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var sentCount = 0;
            foreach (var userId in allUsers)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await pushNotificationService.SendToUserAsync(userId, notification);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send weekly notification to user {UserId}", userId);
                }
            }

            _logger.LogInformation(
                "Weekly notifications sent successfully to {SentCount}/{TotalCount} users",
                sentCount, allUsers.Count);

            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for weekly activities");
        }
    }

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var scheduledTime = _options.ScheduledTime;

        // Parse the scheduled time (format: "HH:mm")
        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid scheduled time format: {ScheduledTime}. Using default 09:00", scheduledTime);
            timeOfDay = new TimeSpan(9, 0, 0); // Default to 9 AM
        }

        // Calculate next Monday
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        
        // If it's Monday and the time hasn't passed yet, schedule for today
        if (daysUntilMonday == 0)
        {
            var todayAtScheduledTime = now.Date.Add(timeOfDay);
            if (todayAtScheduledTime > now)
            {
                return todayAtScheduledTime;
            }
            // Time has passed, schedule for next Monday
            daysUntilMonday = 7;
        }

        var nextMonday = now.Date.AddDays(daysUntilMonday);
        var nextRun = nextMonday.Add(timeOfDay);

        return nextRun;
    }
}
