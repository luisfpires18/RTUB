using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Background service that sends daily reminders to the Ensaiador role
/// when past rehearsals still have pending attendance approvals.
/// Uses the Positions collection on ApplicationUser to check for Position.Ensaiador.
/// </summary>
public class RehearsalApprovalReminderBackgroundService : BackgroundService
{
    private readonly ILogger<RehearsalApprovalReminderBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RehearsalApprovalReminderOptions _options;
    private DateTime _lastRunDate = DateTime.MinValue;

    private const int StartupDelaySeconds = 15;

    public RehearsalApprovalReminderBackgroundService(
        ILogger<RehearsalApprovalReminderBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RehearsalApprovalReminderOptions> options)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken);

        _logger.LogInformation(
            "Rehearsal approval reminder service started. Will check daily at {ScheduledTime} UTC. Enabled: {Enabled}",
            _options.ScheduledTime,
            _options.Enabled);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRunTime();
            var delay = nextRun - DateTime.UtcNow;

            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation("Next rehearsal approval reminder check scheduled for {NextRun} UTC", nextRun);
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
                _logger.LogError(ex, "Error in rehearsal approval reminder service");
            }
        }
    }

    private async Task CheckAndSendRemindersAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        if (_lastRunDate == today)
        {
            _logger.LogDebug("Rehearsal approval reminders already sent today, skipping");
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var pushNotificationFactory = scope.ServiceProvider.GetRequiredService<IPushNotificationFactory>();

        try
        {
            var pendingRehearsalCount = await GetPendingRehearsalIdsQuery(
                    context.RehearsalAttendances.AsNoTracking(),
                    today)
                .CountAsync(cancellationToken);

            if (pendingRehearsalCount == 0)
            {
                _logger.LogInformation("No past rehearsals with pending attendance approvals found.");
                _lastRunDate = today;
                return;
            }

            var ensaiador = await userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Positions.Contains(Position.Ensaiador));

            var userId = ensaiador?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("No users found with Position Ensaiador. Skipping rehearsal approval reminders.");
                _lastRunDate = today;
                return;
            }

            var notification = pushNotificationFactory.CreatePendingRehearsalApprovalsReminderNotification(
                pendingRehearsalCount,
                "/");

            await pushNotificationService.SendToUserAsync(userId, notification);

            _logger.LogInformation(
                "Sent rehearsal approval reminder for {Count} rehearsals to {UserName}",
                pendingRehearsalCount,
                ensaiador!.UserName);

            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking and sending rehearsal approval reminders");
        }
    }

    internal static IQueryable<int> GetPendingRehearsalIdsQuery(
        IQueryable<RehearsalAttendance> attendances,
        DateTime today)
    {
        return attendances
            .Where(attendance =>
                attendance.WillAttend &&
                !attendance.Attended &&
                attendance.Rehearsal != null &&
                !attendance.Rehearsal.IsCanceled &&
                attendance.Rehearsal.Date.Date < today)
            .Select(attendance => attendance.RehearsalId)
            .Distinct();
    }

    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var scheduledTime = _options.ScheduledTime;

        if (!TimeSpan.TryParse(scheduledTime, out var timeOfDay))
        {
            _logger.LogWarning("Invalid scheduled time format: {ScheduledTime}. Using default 15:00", scheduledTime);
            timeOfDay = new TimeSpan(15, 0, 0);
        }

        var nextRun = now.Date.Add(timeOfDay);

        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun;
    }
}
