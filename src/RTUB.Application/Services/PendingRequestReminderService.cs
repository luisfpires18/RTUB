using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that automatically sends push notification reminders for pending requests daily.
/// Runs once per day at a configured time to check for pending requests and notify appropriate users.
/// </summary>
public class PendingRequestReminderService : BackgroundService
{
    private readonly ILogger<PendingRequestReminderService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly PendingRequestReminderOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 15;
    private const string DefaultBaseUrl = "https://rtub.pt";

    public PendingRequestReminderService(
        ILogger<PendingRequestReminderService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<PendingRequestReminderOptions> options)
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
            "Pending request reminder service started. Will check for pending requests daily at {ScheduledTime} UTC. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate time until next run BEFORE checking/sending
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

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
        var today = DateTime.UtcNow.Date;

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

        var notification = pushNotificationFactory.CreatePendingPublicRequestsReminderNotification(pendingCount, DefaultBaseUrl);

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

    private async Task SendMeetingRequestRemindersAsync(
        IMeetingRequestRepository meetingRequestRepository,
        IPushNotificationService pushNotificationService,
        IPushNotificationFactory pushNotificationFactory,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var pendingRequests = (await meetingRequestRepository.GetPendingWithAuthorAsync()).ToList();

        if (!pendingRequests.Any())
        {
            _logger.LogInformation("No pending meeting requests found");
            return;
        }

        _logger.LogInformation("Found {Count} pending meeting requests", pendingRequests.Count);

        var today = DateTime.UtcNow.Date;

        // 1) Automatically expire pending requests whose proposed date has already passed.
        // These should move out of "Pendente" so they can no longer be accepted.
        var expiredRequests = pendingRequests
            .Where(r => r.ProposedDateTime.Date < today)
            .ToList();

        if (expiredRequests.Any())
        {
            foreach (var request in expiredRequests)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Use "Rejected" as the non-answer state to keep enum surface small.
                request.Status = RequestStatus.Rejected;
                await meetingRequestRepository.UpdateAsync(request);
                _logger.LogInformation(
                    "Auto-expired meeting request {RequestId} ('{Title}') because the proposed date {ProposedDate} has passed.",
                    request.Id,
                    request.Title,
                    request.ProposedDateTime);
            }

            // Remove expired ones from the list we will send reminders for
            pendingRequests = pendingRequests
                .Where(r => r.ProposedDateTime.Date >= today)
                .ToList();
        }

        if (!pendingRequests.Any())
        {
            _logger.LogInformation("All pending meeting requests are now expired; no reminders to send.");
            return;
        }

        // Load all users once for position-based filtering with AsNoTracking to prevent tracking issues
        var allUsers = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);

        // Get owner user IDs immediately to avoid tracking issues
        var ownerUsers = await userManager.GetUsersInRoleAsync("Owner");
        var ownerUserIds = ownerUsers.Select(u => u.Id).ToList();

        // Build a lookup for logging purposes
        var userLookup = allUsers.ToDictionary(u => u.Id, u => u.Nickname ?? u.UserName ?? u.Id);

        foreach (var request in pendingRequests)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var notification = pushNotificationFactory.CreatePendingMeetingRequestReminderNotification(request, DefaultBaseUrl);

            // Determine recipients based on meeting type
            IEnumerable<string> positionRecipientIds = Enumerable.Empty<string>();

            switch (request.RequestedMeetingType)
            {
                case MeetingType.ConselhoVeteranos:
                    positionRecipientIds = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteConselhoVeteranos))
                        .Select(u => u.Id);
                    break;

                case MeetingType.AssembleiaGeralOrdinaria:
                case MeetingType.AssembleiaGeralExtraordinaria:
                    positionRecipientIds = allUsers
                        .Where(u => u.Positions != null &&
                                    u.Positions.Contains(Position.PresidenteMesaAssembleia))
                        .Select(u => u.Id);
                    break;

                case MeetingType.ReuniaoDirecao:
                    positionRecipientIds = allUsers
                        .Where(u => u.Positions != null &&
                                    (u.Positions.Contains(Position.Magister) ||
                                     u.Positions.Contains(Position.ViceMagister)))
                        .Select(u => u.Id);
                    break;

                default:
                    break;
            }

            // Union Owners + position-based recipients
            var recipientUserIds = ownerUserIds
                .Concat(positionRecipientIds)
                .Distinct()
                .ToList();

            foreach (var userId in recipientUserIds)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await pushNotificationService.SendToUserAsync(userId, notification);
                _logger.LogInformation(
                    "Push notification sent to {Username} about meeting request '{MeetingTitle}'",
                    userLookup.GetValueOrDefault(userId, userId),
                    request.Title);
            }

            _logger.LogInformation(
                "Sent pending meeting request reminder for '{Title}' to {Count} recipients",
                request.Title,
                recipientUserIds.Count);
        }
    }

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var scheduledTime = _options.ScheduledTime;

        // Parse the scheduled time (format: "HH:mm")
        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid scheduled time format: {ScheduledTime}. Using default 10:00", scheduledTime);
            timeOfDay = new TimeSpan(10, 0, 0); // Default to 10 AM
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
