using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically sends birthday emails daily
/// Runs once per day at a configured time to check for members with birthdays today
/// </summary>
public class BirthdayEmailSchedulerService : BackgroundService
{
    private readonly ILogger<BirthdayEmailSchedulerService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly BirthdayEmailSchedulerOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 10;
    private const int DelayBetweenEmailsSeconds = 5;

    public BirthdayEmailSchedulerService(
        ILogger<BirthdayEmailSchedulerService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BirthdayEmailSchedulerOptions> options)
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
            "Birthday email scheduler started. Will check for birthdays daily at {ScheduledTime} UTC. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate time until next run BEFORE checking/sending
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next birthday check scheduled for {NextRun} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            // Now it's time to check and send
            try
            {
                if (_options.Enabled)
                {
                    await CheckAndSendBirthdayEmailsAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in birthday email scheduler");
            }
        }
    }

    private async Task CheckAndSendBirthdayEmailsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Only run once per day
        if (_lastRunDate == today)
        {
            _logger.LogDebug("Birthday emails already sent today, skipping");
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailNotificationService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

        try
        {
            // Find all users with birthdays today
            // Note: This performs a full table scan. For better performance with large user tables,
            // consider adding computed columns with indexes for Month(DateOfBirth) and Day(DateOfBirth)
            var usersWithBirthdaysToday = await userManager.Users
                .Where(u => u.DateOfBirth.HasValue &&
                           u.DateOfBirth.Value.Month == today.Month &&
                           u.DateOfBirth.Value.Day == today.Day)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Exclude users who are ONLY Leitão (not yet official members)
            // Leitão who have also become Caloiro, Tuno, etc. are still included
            usersWithBirthdaysToday = usersWithBirthdaysToday
                .Where(u => !u.IsOnlyLeitao())
                .ToList();

            if (!usersWithBirthdaysToday.Any())
            {
                _logger.LogInformation("No birthdays today");
                _lastRunDate = today;
                return;
            }

            _logger.LogInformation("Found {Count} members with birthdays today", usersWithBirthdaysToday.Count);

            // Get all subscribed users to send emails to
            var subscribedUsers = await userManager.Users
                .Where(u => u.Subscribed && u.EmailConfirmed && !string.IsNullOrEmpty(u.Email))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (!subscribedUsers.Any())
            {
                _logger.LogWarning("No subscribed users to send birthday notifications to");
                _lastRunDate = today;
                return;
            }

            // Prepare recipient data once for all birthday emails
            var recipientEmails = subscribedUsers.Select(u => u.Email!).ToList();
            var recipientData = subscribedUsers.ToDictionary(
                u => u.Email!,
                u => (u.Nickname ?? u.FirstName ?? "", $"{u.FirstName} {u.LastName}")
            );

            // Send birthday email for each person with a birthday today
            foreach (var birthdayPerson in usersWithBirthdaysToday)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var (success, count, errorMessage) = await emailNotificationService.SendBirthdayNotificationAsync(
                        birthdayPerson.Id,
                        birthdayPerson.Nickname ?? birthdayPerson.FirstName ?? "",
                        $"{birthdayPerson.FirstName} {birthdayPerson.LastName}",
                        recipientEmails,
                        recipientData,
                        null // No progress reporting for background task
                    );

                    if (success)
                    {
                        _logger.LogInformation(
                            "Birthday email sent successfully for {Name} to {Count} recipients",
                            birthdayPerson.Nickname ?? birthdayPerson.GetDisplayName(),
                            count);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to send birthday email for {Name}: {Error}",
                            birthdayPerson.Nickname ?? birthdayPerson.GetDisplayName(),
                            errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error sending birthday email for {Name}",
                        birthdayPerson.Nickname ?? birthdayPerson.GetDisplayName());
                }

                // Small delay between emails to avoid overwhelming the SMTP server
                if (usersWithBirthdaysToday.Count > 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(DelayBetweenEmailsSeconds), cancellationToken);
                }
            }

            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for birthdays");
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
