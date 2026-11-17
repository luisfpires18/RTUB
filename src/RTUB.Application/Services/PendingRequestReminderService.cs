using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically sends push notification reminders for pending requests daily.
/// Runs once per day at a configured time to check for pending requests and notify appropriate users.
/// </summary>
public partial class PendingRequestReminderService : BackgroundService
{
    private readonly TimeProvider _time;

    private readonly ILogger<PendingRequestReminderService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly PendingRequestReminderOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 15;

    public PendingRequestReminderService(
        ILogger<PendingRequestReminderService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<PendingRequestReminderOptions> options,
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
            "Pending request reminder service started. Will check for pending requests daily at {ScheduledTime} Portugal time. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate time until next run BEFORE checking/sending
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - _time.GetUtcNow().UtcDateTime;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next pending request reminder check scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            // Now it's time to check and send
            try
            {
                if (_options.Enabled)
                {
                    await CheckAndSendRemindersAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pending request reminder service");
            }
        }
    }

    private async Task CheckAndSendRemindersAsync(CancellationToken cancellationToken)
    {
        var today = PortugalTime.Today(_time);

        // Only run once per day
        if (_lastRunDate == today)
        {
            _logger.LogDebug("Pending request reminders already sent today, skipping");
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var requestRepository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        var meetingRequestRepository = scope.ServiceProvider.GetRequiredService<IMeetingRequestRepository>();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var pushNotificationFactory = scope.ServiceProvider.GetRequiredService<IPushNotificationFactory>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        try
        {
            // 1. Send reminders for pending public requests to admins
            await SendPublicRequestRemindersAsync(
                requestRepository,
                pushNotificationService,
                pushNotificationFactory,
                userManager,
                cancellationToken);

            // 2. Send reminders for pending meeting requests
            await SendMeetingRequestRemindersAsync(
                meetingRequestRepository,
                pushNotificationService,
                pushNotificationFactory,
                userManager,
                cancellationToken);

            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for pending requests");
        }
    }

    private async Task SendPublicRequestRemindersAsync(
        IRequestRepository requestRepository,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var pendingCount = await requestRepository.GetPendingCountAsync();

        if (pendingCount == 0)
        {
            _logger.LogInformation("No pending public requests found");
            return;
        }

        _logger.LogInformation("Found {Count} pending public requests", pendingCount);

        var notification = pushNotificationFactory.CreatePendingPublicRequestsReminderNotification(pendingCount);

        // Get admin and owner user IDs
        var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
        var ownerUsers = await userManager.GetUsersInRoleAsync("Owner");
        var adminUserIds = adminUsers.Union(ownerUsers).Select(u => u.Id).Distinct().ToList();

        foreach (var userId in adminUserIds)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await pushNotificationService.SendToUserAsync(userId, notification);
        }

        _logger.LogInformation("Sent pending public request reminders to {Count} admins", adminUserIds.Count);
    }

    private DateTime CalculateNextRunTime()
    {
        var scheduledTime = _options.ScheduledTime;

        // Parse the scheduled time (format: "HH:mm")
        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid scheduled time format: {ScheduledTime}. Using default 10:00", scheduledTime);
            timeOfDay = new TimeSpan(10, 0, 0); // Default to 10 AM
        }

        // The configured time is Portugal's clock, summer and winter (#226 F02; was UTC).
        return PortugalTime.NextUtc(_time, timeOfDay);
    }
}
