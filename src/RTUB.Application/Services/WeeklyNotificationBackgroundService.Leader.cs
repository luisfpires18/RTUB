using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

// Weekly notification: new leader push and meeting visibility.

public partial class WeeklyNotificationBackgroundService
{
    public const string LeaderAuditEntityType = "LeaderboardFirstPlace";

    /// <summary>
    /// "Novo 1º Lugar no Ranking", once a week: the all-time leaderboard's first row (level, then XP, descending; ties
    /// in the users table's order, as /leaderboard) against the leader recorded last time in the audit log. A new leader
    /// is broadcast and recorded; the very first run only records (no push for whoever already leads); nobody with XP
    /// yet means no leader. Internal for the tests.
    /// </summary>
    internal async Task NotifyNewLeaderAsync(ApplicationDbContext context, IRankingService ranking,
        IPushNotificationService push, IPushNotificationFactory factory, List<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var progress = await ranking.GetRankProgressBatchAsync(users.Select(u => u.Id), cancellationToken);
        var leader = users
            .Select(u => (User: u, Progress: progress.GetValueOrDefault(u.Id)))
            .Where(x => x.Progress is { CurrentXp: > 0 })
            .OrderByDescending(x => x.Progress!.CurrentLevel)
            .ThenByDescending(x => x.Progress!.CurrentXp)
            .FirstOrDefault();
        if (leader.User is null)
        {
            return;
        }

        var last = await context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == LeaderAuditEntityType)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => a.Changes)
            .FirstOrDefaultAsync(cancellationToken);
        var previousLeaderId = last is null ? null : JsonSerializer.Deserialize<LeaderRecord>(last)?.LeaderId;
        if (previousLeaderId == leader.User.Id)
        {
            return;
        }

        var nickname = leader.User.Nickname ?? leader.User.FirstName ?? "Utilizador";
        context.AuditLogs.Add(new AuditLog
        {
            EntityType = LeaderAuditEntityType,
            Action = previousLeaderId is null ? "Recorded" : "Changed",
            UserName = "System",
            EntityDisplayName = nickname,
            Timestamp = _time.GetUtcNow().UtcDateTime,
            Changes = JsonSerializer.Serialize(new LeaderRecord(leader.User.Id, leader.Progress!.CurrentLevel, leader.Progress.CurrentXp)),
        });
        await context.SaveChangesAsync(cancellationToken);

        if (previousLeaderId is not null)
        {
            await push.BroadcastAsync(factory.CreateLeaderboardFirstPlaceNotification(nickname, leader.Progress.CurrentLevel));
            _logger.LogInformation("New leaderboard first place: {Nickname} (level {Level})", nickname, leader.Progress.CurrentLevel);
        }
    }

    private sealed record LeaderRecord(string LeaderId, int Level, int Xp);

    /// <summary>
    /// Gets the count of meetings visible to a specific user based on their role and positions.
    /// - ConselhoVeteranos (CV): Only for VETERANO/TUNOSSAURO roles or Magister position
    /// - ReuniaoDirecao: Only for Direção members (Magister, ViceMagister, Secretario, PrimeiroTesoureiro, SegundoTesoureiro) + Admin with Tuno category
    /// - AssembleiaGeral (AG): Not for Leitão users (non-associated members)
    /// </summary>
    internal static int GetVisibleMeetingCount(List<Meeting> meetings, ApplicationUser user, bool isAdmin = false)
    {
        var count = 0;

        foreach (var meeting in meetings)
        {
            if (CanUserSeeMeeting(meeting, user, isAdmin))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Determines if a user can see a specific meeting based on meeting type and user role/positions.
    /// </summary>
    internal static bool CanUserSeeMeeting(Meeting meeting, ApplicationUser user, bool isAdmin = false)
    {
        switch (meeting.Type)
        {
            case MeetingType.ConselhoVeteranos:
                // CV meetings: Only for VETERANO/TUNOSSAURO roles or Magister position
                var role = user.CurrentRole;
                var hasMagisterPosition = user.Positions != null && user.Positions.Contains(Position.Magister);
                return role == "VETERANO" || role == "TUNOSSAURO" || hasMagisterPosition;

            case MeetingType.ReuniaoDirecao:
                // Direção meetings: Only for current fiscal year Direção members + Admin with Tuno category
                var hasDirecaoPosition = user.Positions != null &&
                       (user.Positions.Contains(Position.Magister) ||
                        user.Positions.Contains(Position.ViceMagister) ||
                        user.Positions.Contains(Position.Secretario) ||
                        user.Positions.Contains(Position.PrimeiroTesoureiro) ||
                        user.Positions.Contains(Position.SegundoTesoureiro));
                var isAdminTuno = isAdmin && user.IsTuno();
                return hasDirecaoPosition || isAdminTuno;

            case MeetingType.AssembleiaGeralOrdinaria:
            case MeetingType.AssembleiaGeralExtraordinaria:
                // AG meetings: Not for Leitão users
                return !user.IsLeitao();

            default:
                // Unknown meeting type - show to all users
                return true;
        }
    }
}
