using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

public class AfterHoursAdminService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IAfterHoursRolloverService rollover,
    IOptionsMonitor<AfterHoursOptions> options,
    IHostEnvironment environment,
    TimeProvider clock) : IAfterHoursAdminService
{
    /// <summary>The narrowest RTUB role already used for dangerous operational pages (audit log, database viewer, roles).</summary>
    public const string OwnerRole = "Owner";

    // AH-010 defaults for the Pilot form and the PvP review flags.
    public const int PilotMinDays = 7;
    public const int PilotMaxDays = 14;
    public const int PilotDefaultDays = 10;
    public const int PairRepeatFlag = 3;
    public const decimal LowLootMultiplierFlag = 0.25m;

    // ------------------------------------------------------------------ status and readiness

    public async Task<AfterHoursAdminStatus> GetStatusAsync(string actorUserId)
    {
        await EnsureOwnerAsync(actorUserId);
        var now = Now;
        await using var context = await contextFactory.CreateDbContextAsync();

        var cycles = await CycleViewsAsync(context, now);
        var active = cycles.SingleOrDefault(c => c.Status == GameCycleStatus.Active);
        var rows = await context.AfterHoursTuningSettings.AsNoTracking().ToListAsync();
        var tuning = AfterHoursTuning.From(rows.Select(r => KeyValuePair.Create(r.Key, r.Value)), out var bad);
        var invalid = bad.Select(b => new InvalidTuningRow(b.Key, rows.First(r => r.Key == b.Key).Value, b.Error)).ToList();
        var fiscalYears = (await context.FiscalYears.AsNoTracking().OrderByDescending(f => f.StartYear).ToListAsync())
            .Select(f => new FiscalYearOption(f.Id, f.FiscalYearString, f.StartYear)).ToList();

        LiveRolloverPreview? preview = null;
        string? expectedNext = null;
        var nextExists = false;
        if (active is not null)
        {
            var endYear = await context.FiscalYears.Where(f => f.Id == active.FiscalYearId).Select(f => f.EndYear).SingleAsync();
            expectedNext = $"{endYear}-{endYear + 1}";
            var source = await context.AfterHoursGameCycles.AsNoTracking().Include(c => c.FiscalYear).SingleAsync(c => c.Id == active.Id);
            var (next, error) = await AfterHoursRolloverService.NextLiveAsync(context, source);
            nextExists = next is not null;
            if (active.Kind == GameCycleKind.Live)
            {
                var blocker = error ?? (now < active.EndUtc ? $"Available from {active.EndUtc:u} (the cycle's end)." : null);
                preview = new LiveRolloverPreview(active.Id, active.EndUtc, next, blocker, blocker is null);
            }
        }

        var warnings = new List<string>();
        if (active is null) warnings.Add("No active cycle: nobody can play.");
        else if (!active.PlayableNow) warnings.Add($"Cycle {active.Id} is Active but outside its playable window ({active.StartUtc:u} to {active.EndUtc:u}).");
        foreach (var orphan in cycles.Where(c => c.Status == GameCycleStatus.Finished && c.ArchiveId is null))
            warnings.Add($"Cycle {orphan.Id} ({orphan.Kind}, {orphan.FiscalYearLabel}) is Finished without a yearbook archive. It cannot be archived automatically.");
        if (active is not null && !nextExists)
            warnings.Add($"Next fiscal year {expectedNext} does not exist: an annual Live rollover cannot resolve it. Create it in Finance (Owner).");
        foreach (var row in invalid)
            warnings.Add($"Tuning row {row.Key} = \"{row.Value}\" is ignored: {row.Error}");
        if (environment.IsProduction() && options.CurrentValue.Enabled)
            warnings.Add("After Hours is ENABLED in Production.");
        if (!tuning.PvpEnabled) warnings.Add("PvP attacks are paused.");

        var readiness = await ReadinessAsync(context, active, tuning, invalid, nextExists, expectedNext, cycles);
        return new AfterHoursAdminStatus(options.CurrentValue.Enabled, environment.EnvironmentName, active, cycles, tuning, rows.Count, invalid,
            preview, expectedNext, nextExists, fiscalYears, warnings, readiness);
    }

    private async Task<IReadOnlyList<ReadinessCheck>> ReadinessAsync(
        ApplicationDbContext context, AdminCycleView? active, AfterHoursTuning tuning, IReadOnlyList<InvalidTuningRow> invalid,
        bool nextExists, string? expectedNext, IReadOnlyList<AdminCycleView> cycles)
    {
        var checks = new List<ReadinessCheck>
        {
            new("After Hours enabled here", options.CurrentValue.Enabled, options.CurrentValue.Enabled ? "AfterHours:Enabled is true." : "AfterHours:Enabled is off."),
            await ProbeAsync("Database schema", "After Hours tables up to AH-010 answer.", async () =>
            {
                await context.AfterHoursTuningSettings.AnyAsync();
                await context.AfterHoursCosmeticAwards.AnyAsync();
                await context.AfterHoursCycleArchives.AnyAsync();
            }),
            new("Active cycle", active is not null, active is null ? "None." : $"Cycle {active.Id}, {active.Kind}, {active.FiscalYearLabel}."),
            new("Playable now", active?.PlayableNow == true, active is null ? "No active cycle." : active.PlayableNow ? "Yes." : "Outside its window."),
            new("Cycle boundaries", active is null || active.EndUtc > active.StartUtc, active is null ? "No active cycle." : $"{active.StartUtc:u} to {active.EndUtc:u}."),
            new("PvP", true, tuning.PvpEnabled ? "Enabled." : "Explicitly paused by an override.", Blocking: false),
            new("Tuning overrides valid", invalid.Count == 0, invalid.Count == 0 ? "All overrides valid." : $"{invalid.Count} invalid row(s) ignored."),
            new("Family cap is structural 4", FamilyRules.MaxActiveMembers == 4, $"{FamilyRules.MaxActiveMembers}."),
            new("Level cap is structural 20", AfterHoursLevels.MaxLevel == 20, $"{AfterHoursLevels.MaxLevel}."),
            new("Bank fee resolves", invalid.All(i => i.Key != "BankDepositFeePercent"), $"{tuning.BankDepositFeePercent}% (default {AfterHoursTuning.Default.BankDepositFeePercent}%)."),
            new("Next fiscal year", nextExists, active is null ? "No active cycle." : nextExists ? $"{expectedNext} exists." : $"{expectedNext} missing.", Blocking: false),
            await ProbeAsync("Rollover service", "Resolves and reads archives.", async () =>
            {
                ArgumentNullException.ThrowIfNull(rollover);
                if (active is not null)
                    await AfterHoursRolloverService.NextLiveAsync(context, await context.AfterHoursGameCycles.AsNoTracking().SingleAsync(c => c.Id == active.Id));
            }),
            new("No unarchived finished cycle", cycles.All(c => c.Status != GameCycleStatus.Finished || c.ArchiveId is not null),
                "Finished cycles without an archive are history gaps, not a blocker.", Blocking: false),
            await ProbeAsync("Objectives and standings", "Catalogue and standings query answer.", async () =>
            {
                if (ObjectiveCatalogue.WeeklyFor(1).Count == 0 || ObjectiveCatalogue.FamilyFor(1).Count == 0)
                    throw new InvalidOperationException("Empty objective catalogue.");
                if (active is not null) await ObjectiveService.StandingsAsync(context, active.Id, 1, null);
            }),
            await ProbeAsync("Yearbook", "Archive query answers.", () => context.AfterHoursCycleArchives.AsNoTracking().Include(a => a.Players).Take(1).ToListAsync()),
        };
        return checks;
    }

    private static async Task<ReadinessCheck> ProbeAsync(string name, string okDetail, Func<Task> probe)
    {
        try
        {
            await probe();
            return new ReadinessCheck(name, true, okDetail);
        }
        catch (Exception ex)
        {
            return new ReadinessCheck(name, false, $"Failed: {ex.GetType().Name}.");
        }
    }

    // ------------------------------------------------------------------ tuning

    public async Task<string> SetTuningAsync(string actorUserId, string key, string value)
    {
        await EnsureOwnerAsync(actorUserId);
        var setting = AfterHoursTuning.Find(key) ?? throw new ArgumentException($"Unknown setting {key}.", nameof(key));
        if (setting.Normalize(value ?? string.Empty, out var normalized) is { } error)
            throw new ArgumentException($"{setting.Label}: {error}", nameof(value));

        // Under the write lock: an edit racing another updates the same row (unique Key), never adds one.
        await AfterHoursWriteTransaction.RunAsync(contextFactory, async (context, transaction) =>
        {
            var row = await context.AfterHoursTuningSettings.SingleOrDefaultAsync(r => r.Key == key);
            if (row is null)
            {
                row = new AfterHoursTuningSetting { Key = key };
                context.AfterHoursTuningSettings.Add(row);
            }
            row.Value = normalized;
            row.UpdatedAtUtc = Now;
            row.UpdatedByUserId = actorUserId;
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return 0;
        });
        return normalized;
    }

    public async Task ResetTuningAsync(string actorUserId, string key)
    {
        await EnsureOwnerAsync(actorUserId);
        await AfterHoursWriteTransaction.RunAsync(contextFactory, async (context, transaction) =>
        {
            // Removed through SaveChanges, so the audit log records who reset it.
            var row = await context.AfterHoursTuningSettings.SingleOrDefaultAsync(r => r.Key == key);
            if (row is not null)
            {
                context.AfterHoursTuningSettings.Remove(row);
                await context.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            return 0;
        });
    }

    public Task SetPvpEnabledAsync(string actorUserId, bool enabled) =>
        SetTuningAsync(actorUserId, nameof(AfterHoursTuning.PvpEnabled), enabled ? "true" : "false");

    // ------------------------------------------------------------------ cycles

    public async Task<AdminCycleView> CreatePilotAsync(string actorUserId, int fiscalYearId, DateTime startUtc, DateTime endUtc, bool acknowledgeUnusualLength)
    {
        await EnsureOwnerAsync(actorUserId);
        var pilot = GameCycle.Create(fiscalYearId, GameCycleKind.Pilot, startUtc, endUtc); // UTC, end after start
        var days = (endUtc - startUtc).TotalDays;
        if ((days < PilotMinDays || days > PilotMaxDays) && !acknowledgeUnusualLength)
            throw new ArgumentException($"A pilot of {days:0.#} days is outside the recommended {PilotMinDays}–{PilotMaxDays}. Confirm the unusual length to continue.");
        if (endUtc <= Now)
            throw new ArgumentException("The pilot would already be over.");

        var id = await AfterHoursWriteTransaction.RunAsync(contextFactory, async (context, transaction) =>
        {
            if (!await context.FiscalYears.AnyAsync(f => f.Id == fiscalYearId))
                throw new InvalidOperationException($"Fiscal year {fiscalYearId} does not exist.");
            if (await context.AfterHoursGameCycles.AnyAsync(c => c.Status == GameCycleStatus.Active))
                throw new InvalidOperationException("Another cycle is Active. Roll it over before starting a pilot.");

            var created = GameCycle.Create(pilot.FiscalYearId, pilot.Kind, pilot.StartUtc, pilot.EndUtc);
            created.Status = GameCycleStatus.Active; // the single-Active index is the backstop
            context.AfterHoursGameCycles.Add(created);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return created.Id;
        });

        await using var read = await contextFactory.CreateDbContextAsync();
        return (await CycleViewsAsync(read, Now)).Single(c => c.Id == id);
    }

    public async Task<RolloverResult> TransitionPilotToLiveAsync(string actorUserId, int pilotCycleId, int fiscalYearId, DateTime startUtc, DateTime endUtc)
    {
        await EnsureOwnerAsync(actorUserId);
        return await rollover.TransitionPilotToLiveAsync(pilotCycleId, fiscalYearId, startUtc, endUtc);
    }

    public async Task<RolloverResult> RolloverLiveAsync(string actorUserId, int sourceCycleId)
    {
        await EnsureOwnerAsync(actorUserId);
        return await rollover.RolloverLiveAsync(sourceCycleId);
    }

    private static async Task<IReadOnlyList<AdminCycleView>> CycleViewsAsync(ApplicationDbContext context, DateTime now)
    {
        var cycles = await context.AfterHoursGameCycles.AsNoTracking().Include(c => c.FiscalYear)
            .OrderByDescending(c => c.Status == GameCycleStatus.Active).ThenByDescending(c => c.StartUtc).ToListAsync();
        var ids = cycles.Select(c => c.Id).ToList();
        var players = await context.AfterHoursPlayerCycleStates.Where(s => ids.Contains(s.GameCycleId))
            .GroupBy(s => s.GameCycleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var families = await context.AfterHoursFamilyCycleStates.Where(s => ids.Contains(s.GameCycleId))
            .GroupBy(s => s.GameCycleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var archives = await context.AfterHoursCycleArchives.Where(a => ids.Contains(a.GameCycleId))
            .ToDictionaryAsync(a => a.GameCycleId, a => a.Id);
        return cycles.Select(c => new AdminCycleView(c.Id, c.Kind, c.Status, c.FiscalYearId, c.FiscalYear?.FiscalYearString ?? "?",
            c.StartUtc, c.EndUtc, c.IsPlayableAt(now), players.GetValueOrDefault(c.Id), families.GetValueOrDefault(c.Id),
            archives.TryGetValue(c.Id, out var a) ? a : null)).ToList();
    }

    // ------------------------------------------------------------------ PvP review

    public async Task<IReadOnlyList<SuspiciousPvpRow>> GetRecentPvpAsync(string actorUserId, int days = 7)
    {
        await EnsureOwnerAsync(actorUserId);
        var since = Now.AddDays(-Math.Clamp(days, 1, 60));
        await using var context = await contextFactory.CreateDbContextAsync();
        var battles = await context.AfterHoursPvpBattles.AsNoTracking().Include(b => b.Cargo)
            .Where(b => b.AcceptedAtUtc >= since).OrderBy(b => b.AcceptedAtUtc).ThenBy(b => b.Id).ToListAsync();
        var userIds = battles.SelectMany(b => new[] { b.AttackerUserId, b.DefenderUserId }).Distinct().ToList();
        var names = await NamesAsync(context, userIds);
        var memberships = await context.AfterHoursFamilyMemberships.AsNoTracking().Where(m => userIds.Contains(m.UserId)).ToListAsync();
        int? FamilyAt(string userId, DateTime at) => memberships
            .FirstOrDefault(m => m.UserId == userId && m.JoinedAtUtc <= at && (m.LeftAtUtc == null || m.LeftAtUtc > at))?.FamilyId;

        var rows = battles.Select(b =>
        {
            var pair = battles.Where(o => o.AttackerUserId == b.AttackerUserId && o.DefenderUserId == b.DefenderUserId).ToList();
            var between = battles.Where(o => (o.AttackerUserId == b.AttackerUserId && o.DefenderUserId == b.DefenderUserId)
                                             || (o.AttackerUserId == b.DefenderUserId && o.DefenderUserId == b.AttackerUserId)).ToList();
            var directionChanges = between.Zip(between.Skip(1)).Count(p => p.First.AttackerUserId != p.Second.AttackerUserId);
            var flags = new List<string>();
            if (pair.Count >= PairRepeatFlag) flags.Add($"Same pair {pair.Count}× in {days} days");
            if (b.LootMultiplier <= LowLootMultiplierFlag) flags.Add("Loot multiplier ≤ 0.25");
            if (directionChanges >= 2) flags.Add("Alternating attacks");
            var attackerFamily = FamilyAt(b.AttackerUserId, b.AcceptedAtUtc);
            return new SuspiciousPvpRow(b.Id, b.AcceptedAtUtc, names.GetValueOrDefault(b.AttackerUserId, "Unknown"), names.GetValueOrDefault(b.DefenderUserId, "Unknown"),
                b.AttackerWon, b.AttackerEffectivePower, b.DefenderEffectivePower,
                b.AttackerEffectivePower == 0 ? 0 : Math.Round((decimal)b.DefenderEffectivePower / b.AttackerEffectivePower, 2),
                b.LootMultiplier, b.WalletStolen, b.Cargo.Sum(c => (long)c.Quantity * CargoCatalogue.FencePrice(c.CargoType)),
                attackerFamily is not null && attackerFamily == FamilyAt(b.DefenderUserId, b.AcceptedAtUtc),
                pair.Count, pair.Count(o => o.AttackerWon), flags);
        });
        return rows.OrderByDescending(r => r.Flags.Count > 0).ThenByDescending(r => r.AcceptedAtUtc).ThenByDescending(r => r.BattleId).ToList();
    }

    // ------------------------------------------------------------------ cosmetic awards

    public async Task<IReadOnlyList<CosmeticAwardView>> GetAwardsAsync(string actorUserId)
    {
        await EnsureOwnerAsync(actorUserId);
        await using var context = await contextFactory.CreateDbContextAsync();
        var awards = await context.AfterHoursCosmeticAwards.AsNoTracking()
            .OrderBy(a => a.RevokedAtUtc != null).ThenByDescending(a => a.GrantedAtUtc).ToListAsync();
        var names = await NamesAsync(context, awards.Select(a => a.GrantedByUserId));
        return awards.Select(a => new CosmeticAwardView(a, names.GetValueOrDefault(a.GrantedByUserId))).ToList();
    }

    public async Task<IReadOnlyList<AwardRecipientOption>> GetAwardRecipientsAsync(string actorUserId)
    {
        await EnsureOwnerAsync(actorUserId);
        await using var context = await contextFactory.CreateDbContextAsync();
        var ids = await context.AfterHoursPlayerCycleStates.Select(s => s.UserId).Distinct().ToListAsync();
        var names = await NamesAsync(context, ids);
        return names.Select(n => new AwardRecipientOption(n.Key, n.Value)).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<AfterHoursCosmeticAward> GrantAwardAsync(string actorUserId, string recipientUserId, string title, string? description, int? fiscalYearId, int? cycleArchiveId)
    {
        await EnsureOwnerAsync(actorUserId);
        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length < AfterHoursCosmeticAward.TitleMinLength || trimmedTitle.Length > AfterHoursCosmeticAward.TitleMaxLength)
            throw new ArgumentException($"Titles are {AfterHoursCosmeticAward.TitleMinLength}–{AfterHoursCosmeticAward.TitleMaxLength} characters.", nameof(title));
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription?.Length > AfterHoursCosmeticAward.DescriptionMaxLength)
            throw new ArgumentException($"Descriptions are at most {AfterHoursCosmeticAward.DescriptionMaxLength} characters.", nameof(description));

        await using var context = await contextFactory.CreateDbContextAsync();
        var names = await NamesAsync(context, [recipientUserId]);
        if (!names.TryGetValue(recipientUserId, out var recipientName))
            throw new InvalidOperationException("That player does not exist.");
        if (fiscalYearId is { } fy && !await context.FiscalYears.AnyAsync(f => f.Id == fy))
            throw new InvalidOperationException($"Fiscal year {fy} does not exist.");
        if (cycleArchiveId is { } archive && !await context.AfterHoursCycleArchives.AnyAsync(a => a.Id == archive))
            throw new InvalidOperationException($"Archive {archive} does not exist.");

        var award = new AfterHoursCosmeticAward
        {
            UserId = recipientUserId,
            RecipientName = recipientName,
            Title = trimmedTitle,
            Description = trimmedDescription,
            GrantedAtUtc = Now,
            GrantedByUserId = actorUserId,
            FiscalYearId = fiscalYearId,
            CycleArchiveId = cycleArchiveId
        };
        context.AfterHoursCosmeticAwards.Add(award);
        await context.SaveChangesAsync();
        return award;
    }

    public async Task RevokeAwardAsync(string actorUserId, int awardId)
    {
        await EnsureOwnerAsync(actorUserId);
        await using var context = await contextFactory.CreateDbContextAsync();
        var award = await context.AfterHoursCosmeticAwards.SingleOrDefaultAsync(a => a.Id == awardId)
            ?? throw new InvalidOperationException("Unknown award.");
        if (award.RevokedAtUtc is not null) return; // already revoked: nothing to do
        award.RevokedAtUtc = Now;
        award.RevokedByUserId = actorUserId;
        await context.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ helpers

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Server-side role check against the database, not the client or the page.</summary>
    private async Task EnsureOwnerAsync(string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new UnauthorizedAccessException("Owner only.");
        await using var context = await contextFactory.CreateDbContextAsync();
        var isOwner = await context.UserRoles.AnyAsync(ur => ur.UserId == actorUserId
            && context.Roles.Any(r => r.Id == ur.RoleId && r.Name == OwnerRole));
        if (!isOwner)
            throw new UnauthorizedAccessException("Owner only.");
    }

    private static Task<Dictionary<string, string>> NamesAsync(ApplicationDbContext context, IEnumerable<string> userIds)
    {
        var ids = userIds.Distinct().ToList();
        return context.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.Nickname) ? u.UserName ?? "Unknown" : u.Nickname);
    }
}
