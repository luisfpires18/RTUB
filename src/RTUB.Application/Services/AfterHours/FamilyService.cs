using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

public class FamilyService(IDbContextFactory<ApplicationDbContext> contextFactory, TimeProvider clock) : IFamilyService
{
    public async Task<FamilyOverview?> GetOverviewAsync(string userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var context = await contextFactory.CreateDbContextAsync();

        var cycle = await context.AfterHoursGameCycles.AsNoTracking().SingleOrDefaultAsync(c => c.Status == GameCycleStatus.Active);
        if (cycle is null || !cycle.IsPlayableAt(now))
            return null;

        var lastLeft = await context.AfterHoursFamilyMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.LeftAtUtc != null).MaxAsync(m => m.LeftAtUtc);
        var joinFrom = FamilyRules.InLeaveCooldown(lastLeft, now) ? FamilyRules.JoinAllowedFromUtc(lastLeft) : null;

        var mine = await context.AfterHoursFamilyMemberships.AsNoTracking().Include(m => m.Family)
            .SingleOrDefaultAsync(m => m.UserId == userId && m.LeftAtUtc == null);

        if (mine is null)
        {
            var incoming = await context.AfterHoursFamilyInvitations.AsNoTracking()
                .Where(i => i.InvitedUserId == userId && i.Status == FamilyInvitationStatus.Pending)
                .Join(context.AfterHoursFamilies, i => i.FamilyId, f => f.Id, (i, f) => new { i, f.Name })
                .OrderBy(x => x.i.CreatedAtUtc)
                .ToListAsync();
            var inviterNames = await NamesAsync(context, incoming.Select(x => x.i.InvitedByUserId));
            return new FamilyOverview(null, null, [], 0,
                incoming.Select(x => new FamilyInvitationView(x.i.Id, x.i.FamilyId, x.Name, inviterNames.GetValueOrDefault(x.i.InvitedByUserId, "Unknown"), x.i.CreatedAtUtc)).ToList(),
                [], [], joinFrom);
        }

        var memberships = await context.AfterHoursFamilyMemberships.AsNoTracking()
            .Where(m => m.FamilyId == mine.FamilyId && m.LeftAtUtc == null)
            .OrderBy(m => m.Role).ThenBy(m => m.JoinedAtUtc)
            .ToListAsync();
        var memberIds = memberships.Select(m => m.UserId).ToList();
        var levels = await context.AfterHoursPlayerCycleStates.AsNoTracking()
            .Where(s => s.GameCycleId == cycle.Id && memberIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, s => s.Level);
        var treasury = await context.AfterHoursFamilyCycleStates.AsNoTracking()
            .Where(s => s.FamilyId == mine.FamilyId && s.GameCycleId == cycle.Id)
            .Select(s => s.TreasuryCash).SingleOrDefaultAsync();

        var outgoing = await context.AfterHoursFamilyInvitations.AsNoTracking()
            .Where(i => i.FamilyId == mine.FamilyId && i.Status == FamilyInvitationStatus.Pending)
            .OrderBy(i => i.CreatedAtUtc).ToListAsync();

        var candidates = new List<FamilyCandidateView>();
        if (mine.Role == FamilyRole.Boss)
        {
            var busyUsers = context.AfterHoursFamilyMemberships.Where(m => m.LeftAtUtc == null).Select(m => m.UserId);
            var invited = outgoing.Select(i => i.InvitedUserId).ToList();
            var free = await context.AfterHoursPlayerCycleStates.AsNoTracking()
                .Where(s => s.GameCycleId == cycle.Id && !busyUsers.Contains(s.UserId) && !invited.Contains(s.UserId))
                .Select(s => new { s.Id, s.UserId, s.Level })
                .ToListAsync();
            var freeNames = await NamesAsync(context, free.Select(s => s.UserId));
            candidates = free.Select(s => new FamilyCandidateView(s.Id, freeNames.GetValueOrDefault(s.UserId, "Unknown"), s.Level))
                .OrderBy(c => c.DisplayName).ToList();
        }

        var names = await NamesAsync(context, memberIds.Concat(outgoing.Select(i => i.InvitedUserId)));
        var members = memberships
            .Select(m => new FamilyMemberView(m.Id, m.UserId, names.GetValueOrDefault(m.UserId, "Unknown"), m.Role, m.JoinedAtUtc, levels.TryGetValue(m.UserId, out var level) ? level : null))
            .ToList();

        return new FamilyOverview(
            mine.Family,
            members.Single(m => m.MembershipId == mine.Id),
            members,
            treasury,
            [],
            outgoing.Select(i => new FamilyInvitationView(i.Id, i.FamilyId, mine.Family!.Name, names.GetValueOrDefault(i.InvitedUserId, "Unknown"), i.CreatedAtUtc)).ToList(),
            candidates,
            joinFrom);
    }

    private static Task<Dictionary<string, string>> NamesAsync(ApplicationDbContext context, IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        return context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.Nickname) ? u.UserName ?? "Unknown" : u.Nickname);
    }
}
