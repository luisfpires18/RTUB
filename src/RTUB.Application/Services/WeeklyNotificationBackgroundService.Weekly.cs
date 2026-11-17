using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Weekly notification: the weekly summary push.

public partial class WeeklyNotificationBackgroundService
{
    private async Task CheckAndSendWeeklyNotificationsAsync(CancellationToken cancellationToken)
    {
        var today = PortugalTime.Today(_time);

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
        await using var context = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
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
                .Where(r => r.Date >= weekStart && r.Date <= weekEnd && !r.IsCanceled)
                .ToListAsync(cancellationToken);

            // Get all meetings for the current week
            var meetings = await context.Meetings
                .AsNoTracking()
                .Where(m => m.Date >= weekStart && m.Date <= weekEnd && !m.IsCancelled)
                .ToListAsync(cancellationToken);

            var eventCount = events.Count;
            var rehearsalCount = rehearsals.Count;
            var totalMeetingCount = meetings.Count;

            _logger.LogInformation(
                "Found {EventCount} events, {RehearsalCount} rehearsals, {MeetingCount} meetings for the week",
                eventCount, rehearsalCount, totalMeetingCount);

            // Get all users with their categories and positions for filtering
            var allUsers = await context.Users
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Pre-load Admin user IDs for Direção meeting visibility check
            var adminRoleId = await context.Roles
                .Where(r => r.Name == "Admin")
                .Select(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var adminUserIds = adminRoleId != null
                ? (await context.UserRoles
                    .Where(ur => ur.RoleId == adminRoleId)
                    .Select(ur => ur.UserId)
                    .ToListAsync(cancellationToken))
                    .ToHashSet()
                : new HashSet<string>();

            var sentCount = 0;

            foreach (var user in allUsers)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Calculate meeting count visible to this user based on their role/positions
                    var userIsAdmin = adminUserIds.Contains(user.Id);
                    var userMeetingCount = GetVisibleMeetingCount(meetings, user, userIsAdmin);

                    // Create personalized notification with user-specific meeting count
                    var notification = pushNotificationFactory.CreateWeeklySummaryNotification(
                        eventCount, rehearsalCount, userMeetingCount);

                    await pushNotificationService.SendToUserAsync(user.Id, notification);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send weekly notification to user {UserName}", user.UserName ?? user.Id);
                }
            }

            _logger.LogInformation(
                "Weekly notifications sent successfully to {SentCount}/{TotalCount} users",
                sentCount, allUsers.Count);

            try
            {
                await NotifyNewLeaderAsync(context, scope.ServiceProvider.GetRequiredService<IRankingService>(),
                    pushNotificationService, pushNotificationFactory, allUsers, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check the leaderboard's first place");
            }

            _lastRunDate = today;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for weekly activities");
        }
    }
}
