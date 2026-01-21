using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically sends push notification reminders for outstanding debts (calotes) daily.
/// Runs once per day at a configured time to check for users with debts and notify them.
/// </summary>
public class CalotesNotificationBackgroundService : BackgroundService
{
    private readonly ILogger<CalotesNotificationBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly CalotesNotificationOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 15;
    private const string DefaultBaseUrl = "https://rtub.pt";

    public CalotesNotificationBackgroundService(
        ILogger<CalotesNotificationBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CalotesNotificationOptions> options)
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
            "Calotes notification service started. Will check for debts daily at {ScheduledTime} UTC. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate time until next run BEFORE checking/sending
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next calotes notification check scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            // Now it's time to check and send
            try
            {
                if (_options.Enabled)
                {
                    await CheckAndSendCalotesNotificationsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in calotes notification service");
            }
        }
    }

    private async Task CheckAndSendCalotesNotificationsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Only run once per day
        if (_lastRunDate == today)
        {
            _logger.LogDebug("Calotes notifications already sent today, skipping");
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var fiscalYearRepository = scope.ServiceProvider.GetRequiredService<IFiscalYearRepository>();
        var memberDebtService = scope.ServiceProvider.GetRequiredService<IMemberDebtService>();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var pushNotificationFactory = scope.ServiceProvider.GetRequiredService<IPushNotificationFactory>();

        try
        {
            // 1. Get the current fiscal year
            var currentFiscalYearString = FiscalYearHelper.GetCurrentFiscalYearString();
            var fiscalYearStartYear = FiscalYearHelper.GetCurrentFiscalYearStartYear();

            var fiscalYear = await fiscalYearRepository.GetByStartYearAsync(fiscalYearStartYear);

            if (fiscalYear == null)
            {
                _logger.LogWarning("Current fiscal year {FiscalYear} not found in database, skipping calotes notifications", currentFiscalYearString);
                return;
            }

            // 2. Get all users with debts
            var usersWithDebts = await memberDebtService.GetUsersWithDebtsAsync(fiscalYear.Id);

            if (!usersWithDebts.Any())
            {
                _logger.LogInformation("No users with outstanding debts found for fiscal year {FiscalYear}", currentFiscalYearString);
                _lastRunDate = today;
                return;
            }

            _logger.LogInformation("Found {Count} users with outstanding debts for fiscal year {FiscalYear}", usersWithDebts.Count, currentFiscalYearString);

            // 3. Send notifications to each user with debt
            var notificationsSent = 0;
            foreach (var (userId, amount) in usersWithDebts)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Only send if amount is greater than 0
                if (amount <= 0)
                {
                    continue;
                }

                var notification = pushNotificationFactory.CreateCalotesReminderNotification(amount, DefaultBaseUrl);
                await pushNotificationService.SendToUserAsync(userId, notification);
                notificationsSent++;

                _logger.LogDebug("Sent calotes notification to user {UserId} for amount {Amount:N2}€", userId, amount);
            }

            _logger.LogInformation("Sent {Count} calotes notifications", notificationsSent);
            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking and sending calotes notifications");
        }
    }

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var scheduledTime = _options.ScheduledTime;

        // Parse the scheduled time (format: "HH:mm")
        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid scheduled time format: {ScheduledTime}. Using default 10:30", scheduledTime);
            timeOfDay = new TimeSpan(10, 30, 0); // Default to 10:30 AM
        }

        // Calculate next run time in UTC
        var nextRun = now.Date.Add(timeOfDay);

        // If the scheduled time has already passed today, schedule for tomorrow
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun;
    }
}
