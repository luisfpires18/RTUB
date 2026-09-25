using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Exceptions;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

/// <summary>
/// Archive + finish + activate as one SQLite write transaction (<see cref="AfterHoursWriteTransaction"/>):
/// concurrent calls serialize on the write lock and the later ones find the archive and replay it; the
/// unique indexes on the archive are the backstop. Anything that fails before commit leaves the source
/// cycle Active and writes nothing. No player state, family treasury, contract or objective row is
/// copied, changed or deleted: the new cycle creates its own lazily, like any new cycle.
/// </summary>
public class AfterHoursRolloverService(IDbContextFactory<ApplicationDbContext> contextFactory, TimeProvider clock) : IAfterHoursRolloverService
{
    private sealed record Target(int FiscalYearId, DateTime StartUtc, DateTime EndUtc);

    public Task<RolloverResult> RolloverLiveAsync(int sourceCycleId) =>
        TransitionAsync(sourceCycleId, GameCycleKind.Live, _ => true, async (context, source, now) =>
        {
            if (now < source.EndUtc)
                throw new InvalidOperationException($"Cycle {source.Id} is playable until {source.EndUtc:u}; annual rollover waits for its end.");

            var (next, error) = await NextLiveAsync(context, source);
            return next is null ? throw new InvalidOperationException(error) : new Target(next.FiscalYearId, next.StartUtc, next.EndUtc);
        });

    /// <summary>
    /// The Live cycle an annual rollover of <paramref name="source"/> would start: the one existing RTUB fiscal
    /// year whose StartYear is the source fiscal year's EndYear, 1 September to 1 September in Lisbon. Never
    /// creates or guesses a fiscal year; returns the reason instead. Also used by the admin preview.
    /// </summary>
    internal static async Task<(NextLiveCycle? Next, string? Error)> NextLiveAsync(ApplicationDbContext context, GameCycle source)
    {
        var endYear = source.FiscalYear?.EndYear
            ?? await context.FiscalYears.Where(f => f.Id == source.FiscalYearId).Select(f => f.EndYear).SingleAsync();
        var next = await context.FiscalYears.AsNoTracking().Where(f => f.StartYear == endYear).ToListAsync();
        if (next.Count != 1)
            return (null, next.Count == 0
                ? $"No RTUB fiscal year starts in {endYear}. Create it before rolling over."
                : $"{next.Count} RTUB fiscal years start in {endYear}; the next one is ambiguous.");
        var year = next[0];
        return (new NextLiveCycle(year.Id, year.FiscalYearString, RolloverRules.SeptemberStartUtc(year.StartYear), RolloverRules.SeptemberStartUtc(year.EndYear)), null);
    }

    public Task<RolloverResult> TransitionPilotToLiveAsync(int pilotCycleId, int targetFiscalYearId, DateTime targetStartUtc, DateTime targetEndUtc)
    {
        // Same validation as any cycle: UTC instants, end after start. Throws before anything is read.
        GameCycle.Create(targetFiscalYearId, GameCycleKind.Live, targetStartUtc, targetEndUtc);

        return TransitionAsync(pilotCycleId, GameCycleKind.Pilot,
            done => done.FiscalYearId == targetFiscalYearId && done.StartUtc == targetStartUtc && done.EndUtc == targetEndUtc,
            async (context, _, _) =>
            {
                // A Pilot may end early: no EndUtc check, on purpose (pilot length is administrative).
                if (!await context.FiscalYears.AnyAsync(f => f.Id == targetFiscalYearId))
                    throw new InvalidOperationException($"Fiscal year {targetFiscalYearId} does not exist.");
                return new Target(targetFiscalYearId, targetStartUtc, targetEndUtc);
            });
    }

    private Task<RolloverResult> TransitionAsync(
        int sourceCycleId,
        GameCycleKind sourceKind,
        Func<GameCycle, bool> isSameTarget,
        Func<ApplicationDbContext, GameCycle, DateTime, Task<Target>> resolveTarget) =>
        AfterHoursWriteTransaction.RunAsync(contextFactory, async (context, transaction) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var source = await context.AfterHoursGameCycles.Include(c => c.FiscalYear).SingleOrDefaultAsync(c => c.Id == sourceCycleId)
                ?? throw new EntityNotFoundException(nameof(GameCycle), sourceCycleId);
            if (source.Kind != sourceKind)
                throw new InvalidOperationException($"Cycle {source.Id} is a {source.Kind} cycle; this transition needs a {sourceKind} cycle.");

            // Already done: the same call again returns the stored result; a different destination is refused.
            var archive = await context.AfterHoursCycleArchives.AsNoTracking().SingleOrDefaultAsync(a => a.GameCycleId == source.Id);
            if (archive is not null)
            {
                var done = await context.AfterHoursGameCycles.AsNoTracking().SingleAsync(c => c.Id == archive.NextGameCycleId);
                if (!isSameTarget(done))
                    throw new InvalidOperationException($"Cycle {source.Id} was already rolled over into cycle {done.Id}.");
                return new RolloverResult(archive.Id, source.Id, done.Id, Replayed: true);
            }

            if (source.Status != GameCycleStatus.Active)
                throw new InvalidOperationException($"Cycle {source.Id} is {source.Status}; only an Active cycle can be rolled over.");

            var target = await resolveTarget(context, source, now);
            var next = GameCycle.Create(target.FiscalYearId, GameCycleKind.Live, target.StartUtc, target.EndUtc);
            if (next.EndUtc <= now)
                throw new InvalidOperationException($"The new cycle would already be over ({next.EndUtc:u}).");

            archive = await SnapshotAsync(context, source, now);
            context.AfterHoursCycleArchives.Add(archive);
            source.Status = GameCycleStatus.Finished;
            // Frees the single Active slot (filtered unique index) before the next cycle takes it.
            await context.SaveChangesAsync();

            next.Status = GameCycleStatus.Active;
            context.AfterHoursGameCycles.Add(next);
            await context.SaveChangesAsync();

            archive.NextGameCycleId = next.Id;
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new RolloverResult(archive.Id, source.Id, next.Id, Replayed: false);
        });

    /// <summary>
    /// The yearbook rows for <paramref name="source"/>: standings from the same code as the live
    /// leaderboards, final power stats, and rosters from membership history at the cycle's end.
    /// </summary>
    private static async Task<CycleArchive> SnapshotAsync(ApplicationDbContext context, GameCycle source, DateTime now)
    {
        var official = source.Kind == GameCycleKind.Live;
        var endedAt = now < source.EndUtc ? now : source.EndUtc; // a Pilot may finish early

        var (players, families) = await ObjectiveService.StandingsAsync(context, source.Id, 0, null);
        var states = await context.AfterHoursPlayerCycleStates.AsNoTracking()
            .Where(s => s.GameCycleId == source.Id).ToDictionaryAsync(s => s.Id);

        // Who was in which family when the cycle ended, from the persistent history rather than today's roster.
        var memberships = await context.AfterHoursFamilyMemberships.AsNoTracking()
            .Where(m => m.JoinedAtUtc <= endedAt && (m.LeftAtUtc == null || m.LeftAtUtc > endedAt))
            .OrderBy(m => m.Role).ThenBy(m => m.JoinedAtUtc)
            .ToListAsync();
        var familyOf = memberships.ToDictionary(m => m.UserId, m => m.FamilyId);
        var familyIds = memberships.Select(m => m.FamilyId).Distinct().ToList();
        var familyNames = await context.AfterHoursFamilies.AsNoTracking()
            .Where(f => familyIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name);
        var rosterIds = memberships.Select(m => m.UserId).ToList();
        var names = await context.Users.AsNoTracking()
            .Where(u => rosterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.Nickname) ? u.UserName ?? "Unknown" : u.Nickname);

        return new CycleArchive
        {
            GameCycleId = source.Id,
            Kind = source.Kind,
            FiscalYearId = source.FiscalYearId,
            FiscalYearLabel = source.FiscalYear!.FiscalYearString,
            StartUtc = source.StartUtc,
            EndUtc = source.EndUtc,
            ArchivedAtUtc = now,
            Official = official,
            Players = players.Select(p =>
            {
                var state = states[p.Row.Id];
                int? familyId = familyOf.TryGetValue(p.UserId, out var f) ? f : null;
                return new YearbookPlayerEntry
                {
                    UserId = p.UserId,
                    DisplayName = p.Row.Name,
                    Level = state.Level,
                    XP = state.XP,
                    Toughness = state.Toughness,
                    Stealth = state.Stealth,
                    Smarts = state.Smarts,
                    Charisma = state.Charisma,
                    AnnualScore = p.Row.AnnualScore,
                    Rank = p.Row.Rank,
                    ScoringWeeks = p.Row.ScoringWeeks,
                    FamilyId = familyId,
                    FamilyName = familyId is { } id ? familyNames[id] : null,
                    IsChampion = RolloverRules.IsChampion(official, p.Row.Rank, p.Row.AnnualScore)
                };
            }).ToList(),
            Families = families.Select(f => new YearbookFamilyEntry
            {
                FamilyId = f.Id,
                FamilyName = f.Name,
                AnnualScore = f.AnnualScore,
                Rank = f.Rank,
                ScoringWeeks = f.ScoringWeeks,
                IsChampion = RolloverRules.IsChampion(official, f.Rank, f.AnnualScore),
                Members = memberships.Where(m => m.FamilyId == f.Id).Select(m => new YearbookFamilyMember
                {
                    UserId = m.UserId,
                    DisplayName = names.GetValueOrDefault(m.UserId, "Unknown"),
                    Role = m.Role
                }).ToList()
            }).ToList()
        };
    }
}

public class YearbookService(IDbContextFactory<ApplicationDbContext> contextFactory) : IYearbookService
{
    public async Task<IReadOnlyList<AfterHoursCosmeticAward>> GetActiveAwardsAsync(string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.AfterHoursCosmeticAwards.AsNoTracking()
            .Where(a => a.UserId == userId && a.RevokedAtUtc == null)
            .OrderByDescending(a => a.GrantedAtUtc).ToListAsync();
    }

    public async Task<IReadOnlyList<CycleArchive>> GetArchivesAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.AfterHoursCycleArchives.AsNoTracking()
            .Include(a => a.Players)
            .Include(a => a.Families).ThenInclude(f => f.Members)
            .AsSplitQuery()
            .OrderByDescending(a => a.ArchivedAtUtc).ThenByDescending(a => a.Id)
            .ToListAsync();
    }
}
