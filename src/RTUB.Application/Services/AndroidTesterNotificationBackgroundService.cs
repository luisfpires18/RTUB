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
/// Background service that sends reminder notifications to Android testers throughout the day.
/// Notifications are sent at configured times (default: 9am, 12pm, 3pm, 6pm, 9pm) if the user hasn't logged in today.
/// Only runs between StartDate and EndDate when enabled.
/// </summary>
public class AndroidTesterNotificationBackgroundService : BackgroundService
{
    private readonly ILogger<AndroidTesterNotificationBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AndroidTesterNotificationOptions _options;
    private readonly HashSet<string> _notificationsSentToday = new();
    private DateTime _lastCheckDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 20;

    public AndroidTesterNotificationBackgroundService(
        ILogger<AndroidTesterNotificationBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AndroidTesterNotificationOptions> options)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;

        // Validate notification times at startup
        ValidateNotificationTimes();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait before starting to allow the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken);

        _logger.LogInformation(
            "Android tester notification service started. Enabled: {Enabled}, Times: {Times}",
            _options.Enabled,
            string.Join(", ", _options.NotificationTimes));

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogDebug("Next Android tester notification check scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                if (_options.Enabled)
                {
                    await CheckAndSendNotificationsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Android tester notification service");
            }
        }
    }

    private async Task CheckAndSendNotificationsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        // Reset daily tracking if it's a new day
        if (_lastCheckDate != today)
        {
            _notificationsSentToday.Clear();
            _lastCheckDate = today;
            _logger.LogDebug("Reset daily notification tracking for Android testers");
        }

        // Check if campaign is active
        if (!IsCampaignActive(today))
        {
            _logger.LogDebug("Android tester campaign is not active today ({Today})", today.ToString("yyyy-MM-dd"));
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var pushNotificationFactory = scope.ServiceProvider.GetRequiredService<IPushNotificationFactory>();

        try
        {
            var baseUrl = "/";

            // Get all Android testers
            var androidTesters = await context.Users
                .AsNoTracking()
                .Where(u => u.IsAndroidTester)
                .ToListAsync(cancellationToken);

            if (!androidTesters.Any())
            {
                _logger.LogDebug("No Android testers found");
                return;
            }

            _logger.LogDebug("Found {Count} Android testers", androidTesters.Count);

            // Filter testers who haven't logged in today and haven't been notified yet
            var usersToNotify = androidTesters
                .Where(u =>
                {
                    // Skip if already notified today
                    if (_notificationsSentToday.Contains(u.Id))
                    {
                        return false;
                    }

                    // Skip if user has logged in today
                    if (u.LastLoginDate.HasValue && u.LastLoginDate.Value.Date >= today)
                    {
                        _logger.LogDebug(
                            "Android tester {UserId} has already logged in today ({LastLogin}), skipping remaining notifications",
                            u.Id,
                            u.LastLoginDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));

                        // Mark as notified to avoid checking again today
                        _notificationsSentToday.Add(u.Id);
                        return false;
                    }

                    return true;
                })
                .ToList();

            if (!usersToNotify.Any())
            {
                _logger.LogDebug("No Android testers need notifications at this time");
                return;
            }

            // Send notifications
            var notification = pushNotificationFactory.CreateAndroidTesterReminderNotification(baseUrl);
            var userIds = usersToNotify.Select(u => u.Id).ToList();

            await pushNotificationService.SendToSelectedUsersAsync(userIds, notification);

            // Mark users as notified
            foreach (var userId in userIds)
            {
                _notificationsSentToday.Add(userId);
            }

            _logger.LogInformation(
                "Sent Android tester reminder to {Count} users at {Time} UTC",
                userIds.Count,
                now.ToString("HH:mm"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking and sending Android tester notifications");
        }
    }

    private bool IsCampaignActive(DateTime today)
    {
        // Check if we're within the campaign date range
        if (_options.StartDate.HasValue && today < _options.StartDate.Value.Date)
        {
            return false;
        }

        if (_options.EndDate.HasValue && today > _options.EndDate.Value.Date)
        {
            return false;
        }

        return true;
    }

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        // Parse all configured notification times
        var notificationTimes = new List<TimeSpan>();
        foreach (var timeString in _options.NotificationTimes)
        {
            if (TimeSpan.TryParse(timeString, out var timeOfDay))
            {
                notificationTimes.Add(timeOfDay);
            }
            else
            {
                _logger.LogWarning("Invalid notification time format: {TimeString}", timeString);
            }
        }

        // If no valid times, use default
        if (!notificationTimes.Any())
        {
            _logger.LogWarning("No valid notification times configured, using default times");
            notificationTimes = new List<TimeSpan>
            {
                new(9, 0, 0),   // 09:00
                new(12, 0, 0),  // 12:00
                new(15, 0, 0),  // 15:00
                new(18, 0, 0),  // 18:00
                new(21, 0, 0)   // 21:00
            };
        }

        // Sort times
        notificationTimes.Sort();

        // Find the next notification time today
        foreach (var time in notificationTimes)
        {
            var nextRun = today.Add(time);
            if (nextRun > now)
            {
                return nextRun;
            }
        }

        // All times have passed today, schedule for first time tomorrow
        return today.AddDays(1).Add(notificationTimes.First());
    }

    private void ValidateNotificationTimes()
    {
        if (_options.NotificationTimes == null || !_options.NotificationTimes.Any())
        {
            throw new InvalidOperationException(
                "AndroidTesterNotificationOptions.NotificationTimes must contain at least one time value.");
        }

        var invalidTimes = new List<string>();
        foreach (var timeString in _options.NotificationTimes)
        {
            if (!TimeSpan.TryParse(timeString, out _))
            {
                invalidTimes.Add(timeString);
            }
        }

        if (invalidTimes.Any())
        {
            throw new InvalidOperationException(
                $"AndroidTesterNotificationOptions.NotificationTimes contains invalid time format(s): {string.Join(", ", invalidTimes)}. " +
                "Expected format: HH:mm (e.g., '09:00', '15:30')");
        }

        _logger.LogInformation(
            "Validated {Count} notification time(s): {Times}",
            _options.NotificationTimes.Count,
            string.Join(", ", _options.NotificationTimes));
    }
}
