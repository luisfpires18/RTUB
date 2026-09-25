using System.Data.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// AH-009 yearbook and rollover against the real SQLite schema: the single-Active index, the archive's
/// unique indexes and the write transaction are database behaviour. The clock is frozen per test. Tests in
/// this class share one database and run sequentially; each starts its own cycle in its own fiscal years.
/// </summary>
public class AfterHoursRolloverTests : IClassFixture<AfterHoursRolloverFactory>
{
    private static int _nextYear = 4000;
    private readonly AfterHoursRolloverFactory _factory;

    public AfterHoursRolloverTests(AfterHoursRolloverFactory factory)
    {
        _factory = factory;
        factory.Failure.Clear();
        factory.Dice.Reset();
    }

    // ------------------------------------------------------------------ archive contents

    [Fact]
    public async Task LiveArchive_SnapshotsEveryPlayer_Best12_CompetitionRanks_Stats_AndCoChampions()
    {
        var (cycle, _) = await StartLiveAsync();
        var a = await PlayerAsync("Alpha");
        var b = await PlayerAsync("Bravo");
        var c = await PlayerAsync("Charlie");
        var d = await PlayerAsync("Delta");
        await WeeklyScoresAsync(cycle.Id, a.StateId, [5, .. Enumerable.Repeat(50, 12)]); // 13 weeks, best 12 = 600
        await WeeklyScoresAsync(cycle.Id, b.StateId, [.. Enumerable.Repeat(50, 12)]);      // 600: tied
        await WeeklyScoresAsync(cycle.Id, c.StateId, [0, 30]);
        await UpdateStateAsync(a.StateId, s => (s.Level, s.XP, s.Toughness, s.Stealth, s.Smarts, s.Charisma, s.WalletCash, s.BankCash) = (7, 5_000, 5, 6, 7, 8, 9_999, 8_888));
        var f1 = await FamilyAsync(cycle.Id, 500, a, c);
        var f2 = await FamilyAsync(cycle.Id, 0, b);
        var f3 = await FamilyAsync(cycle.Id, 0, d);
        await FamilyScoresAsync(cycle.Id, f1.Id, 100, 100);
        await FamilyScoresAsync(cycle.Id, f2.Id, 50, 100, 50);
        var live = await ObjectivesAsync().GetLeaderboardsAsync(a.UserId);

        _factory.Now = cycle.EndUtc;
        var result = await Rollover().RolloverLiveAsync(cycle.Id);

        var archive = (await ArchivesAsync()).Single(x => x.Id == result.ArchiveId);
        (archive.GameCycleId, archive.Kind, archive.Official, archive.FiscalYearId, archive.StartUtc, archive.EndUtc, archive.ArchivedAtUtc)
            .Should().Be((cycle.Id, GameCycleKind.Live, true, cycle.FiscalYearId, cycle.StartUtc, cycle.EndUtc, cycle.EndUtc));
        archive.Players.Should().HaveCount(4, "every player state of the cycle is archived");
        var byUser = archive.Players.ToDictionary(p => p.UserId);
        (byUser[a.UserId].AnnualScore, byUser[a.UserId].Rank, byUser[a.UserId].ScoringWeeks, byUser[a.UserId].IsChampion).Should().Be((600, 1, 13, true));
        (byUser[b.UserId].AnnualScore, byUser[b.UserId].Rank, byUser[b.UserId].IsChampion).Should().Be((600, 1, true));
        (byUser[c.UserId].AnnualScore, byUser[c.UserId].Rank, byUser[c.UserId].ScoringWeeks, byUser[c.UserId].IsChampion).Should().Be((30, 3, 1, false));
        (byUser[d.UserId].AnnualScore, byUser[d.UserId].Rank, byUser[d.UserId].IsChampion).Should().Be((0, 4, false));
        var alpha = byUser[a.UserId];
        (alpha.DisplayName, alpha.Level, alpha.XP, alpha.Toughness, alpha.Stealth, alpha.Smarts, alpha.Charisma, alpha.FamilyId, alpha.FamilyName)
            .Should().Be((a.Name, 7, 5_000L, 5, 6, 7, 8, (int?)f1.Id, (string?)f1.Name));
        byUser[d.UserId].FamilyName.Should().Be(f3.Name);

        // The yearbook ranks exactly as the live leaderboard did at the end.
        archive.Players.Select(p => (p.DisplayName, p.AnnualScore, p.Rank))
            .Should().BeEquivalentTo(live!.Individual.Select(r => (r.Name, r.AnnualScore, r.Rank)));
        archive.Families.Select(f => (f.FamilyName, f.AnnualScore, f.Rank))
            .Should().BeEquivalentTo(live.Families.Select(r => (r.Name, r.AnnualScore, r.Rank)));

        var families = archive.Families.ToDictionary(f => f.FamilyId);
        (families[f1.Id].AnnualScore, families[f1.Id].Rank, families[f1.Id].ScoringWeeks, families[f1.Id].IsChampion).Should().Be((200, 1, 2, true));
        (families[f2.Id].AnnualScore, families[f2.Id].Rank, families[f2.Id].IsChampion).Should().Be((200, 1, true));
        (families[f3.Id].AnnualScore, families[f3.Id].Rank, families[f3.Id].IsChampion).Should().Be((0, 3, false));
        families[f1.Id].Members.Select(m => (m.UserId, m.DisplayName, m.Role))
            .Should().BeEquivalentTo([(a.UserId, a.Name, FamilyRole.Boss), (c.UserId, c.Name, FamilyRole.Member)]);

        typeof(YearbookPlayerEntry).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Wallet") || n.Contains("Bank") || n.Contains("Cash") || n.Contains("Cargo") || n.Contains("Battle"));
    }

    [Fact]
    public async Task LiveArchive_WhereNobodyScored_RanksEveryoneFirst_WithNoChampion()
    {
        var (cycle, _) = await StartLiveAsync();
        var a = await PlayerAsync("Zero-A");
        var b = await PlayerAsync("Zero-B");
        await FamilyAsync(cycle.Id, 0, a);

        _factory.Now = cycle.EndUtc;
        var archive = await ArchiveOfAsync((await Rollover().RolloverLiveAsync(cycle.Id)).ArchiveId);

        archive.Official.Should().BeTrue();
        archive.Players.Should().HaveCount(2).And.OnlyContain(p => p.Rank == 1 && p.AnnualScore == 0 && !p.IsChampion);
        archive.Families.Should().ContainSingle().Which.IsChampion.Should().BeFalse();
    }

    [Fact]
    public async Task PilotArchive_IsNonOfficial_AndNeverHasAChampion()
    {
        var (pilot, fiscalYear) = await StartPilotAsync();
        var a = await PlayerAsync("Pilot-A");
        var b = await PlayerAsync("Pilot-B");
        await WeeklyScoresAsync(pilot.Id, a.StateId, 80);
        var family = await FamilyAsync(pilot.Id, 0, b);
        await FamilyScoresAsync(pilot.Id, family.Id, 75);

        var result = await Rollover().TransitionPilotToLiveAsync(pilot.Id, fiscalYear.Id, _factory.Now, pilot.EndUtc.AddDays(200));

        var archive = await ArchiveOfAsync(result.ArchiveId);
        (archive.Kind, archive.Official).Should().Be((GameCycleKind.Pilot, false));
        archive.Players.Single(p => p.UserId == a.UserId).Should().Match<YearbookPlayerEntry>(p => p.Rank == 1 && p.AnnualScore == 80 && !p.IsChampion);
        archive.Families.Single().Should().Match<YearbookFamilyEntry>(f => f.Rank == 1 && f.AnnualScore == 75 && !f.IsChampion);
    }

    [Fact]
    public async Task Snapshots_UseTheEndOfCycleRoster_AndNeverChangeAfterwards()
    {
        var (cycle, _) = await StartLiveAsync();
        var boss = await PlayerAsync("Boss");
        var leftBefore = await PlayerAsync("LeftBefore");
        var leftAfter = await PlayerAsync("LeftAfter");
        var family = await FamilyAsync(cycle.Id, 0, boss, leftAfter);
        await AddMembershipAsync(family.Id, leftBefore.UserId, joined: cycle.StartUtc, left: cycle.EndUtc.AddDays(-1));
        var joinedAfter = await UserAsync("JoinedAfter");
        await WeeklyScoresAsync(cycle.Id, boss.StateId, 40);
        await FamilyScoresAsync(cycle.Id, family.Id, 50);

        // History, not today's roster: one member left after the cycle ended, another joined after it.
        await using (var db = await DbAsync())
        {
            (await db.AfterHoursFamilyMemberships.SingleAsync(m => m.UserId == leftAfter.UserId)).LeftAtUtc = cycle.EndUtc.AddHours(1);
            await db.SaveChangesAsync();
        }
        await AddMembershipAsync(family.Id, joinedAfter.UserId, joined: cycle.EndUtc.AddMinutes(30), left: null);

        _factory.Now = cycle.EndUtc.AddHours(2);
        var result = await Rollover().RolloverLiveAsync(cycle.Id);
        var before = await ArchiveOfAsync(result.ArchiveId);

        var entry = before.Families.Single();
        entry.Members.Select(m => m.UserId).Should().BeEquivalentTo([boss.UserId, leftAfter.UserId]);
        before.Players.Single(p => p.UserId == leftAfter.UserId).FamilyName.Should().Be(family.Name);
        before.Players.Single(p => p.UserId == leftBefore.UserId).FamilyId.Should().BeNull();

        // Later: renames, family moves, and changed stored points (a rule change) in the old cycle.
        await using (var db = await DbAsync())
        {
            (await db.Users.SingleAsync(u => u.Id == boss.UserId)).Nickname = "Renamed";
            (await db.AfterHoursFamilies.SingleAsync(f => f.Id == family.Id)).Name = "Renamed Fam";
            (await db.AfterHoursFamilyMemberships.SingleAsync(m => m.UserId == boss.UserId && m.LeftAtUtc == null)).LeftAtUtc = _factory.Now;
            foreach (var row in await db.AfterHoursPlayerObjectiveProgress.Where(r => r.GameCycleId == cycle.Id).ToListAsync())
                row.PointsAwarded = 1;
            await db.SaveChangesAsync();
        }
        await Rollover().RolloverLiveAsync(cycle.Id); // a replay must not refresh anything either

        var after = await ArchiveOfAsync(result.ArchiveId);
        after.Should().BeEquivalentTo(before);
        after.Players.Single(p => p.UserId == boss.UserId).Should().Match<YearbookPlayerEntry>(p => p.DisplayName == boss.Name && p.AnnualScore == 40 && p.FamilyName == family.Name);
    }

    [Fact]
    public async Task ArchivedEntries_SurviveDeletionOfTheAccount_Unchanged()
    {
        var (cycle, _) = await StartLiveAsync();
        var champion = await PlayerAsync("Gone-Champ");
        var teammate = await PlayerAsync("Stays");
        await WeeklyScoresAsync(cycle.Id, champion.StateId, 70);
        await WeeklyScoresAsync(cycle.Id, teammate.StateId, 20);
        var family = await FamilyAsync(cycle.Id, 0, teammate, champion);
        await FamilyScoresAsync(cycle.Id, family.Id, 50);
        _factory.Now = cycle.EndUtc;
        var result = await Rollover().RolloverLiveAsync(cycle.Id);
        var before = await ArchiveOfAsync(result.ArchiveId);
        before.Players.Single(p => p.UserId == champion.UserId).Should().Match<YearbookPlayerEntry>(p => p.IsChampion && p.Rank == 1 && p.AnnualScore == 70);

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.DeleteAsync((await users.FindByIdAsync(champion.UserId))!)).Succeeded.Should().BeTrue();
        }

        await using (var db = await DbAsync())
        {
            (await db.Users.AnyAsync(u => u.Id == champion.UserId)).Should().BeFalse();
            (await db.AfterHoursPlayerCycleStates.AnyAsync(s => s.UserId == champion.UserId)).Should().BeFalse("live game data still goes with the account");
        }
        var after = await ArchiveOfAsync(result.ArchiveId);
        after.Should().BeEquivalentTo(before, "the yearbook is history, not live account data");
        var gone = after.Players.Single(p => p.UserId == champion.UserId);
        (gone.DisplayName, gone.Rank, gone.AnnualScore, gone.IsChampion, gone.FamilyId, gone.FamilyName)
            .Should().Be((champion.Name, 1, 70, true, (int?)family.Id, (string?)family.Name));
        after.Families.Single().Members.Should().Contain(m => m.UserId == champion.UserId && m.DisplayName == champion.Name && m.Role == FamilyRole.Member);
    }

    // ------------------------------------------------------------------ reset boundary

    [Fact]
    public async Task LiveRollover_ResetsPower_KeepsIdentity_AndLeavesTheOldCycleUntouched()
    {
        var (cycle, nextYear) = await StartLiveAsync();
        var attacker = await PlayerAsync("Old-Attacker");
        var defender = await PlayerAsync("Old-Defender");
        await UpdateStateAsync(attacker.StateId, s =>
        {
            s.CreatedAt = _factory.Now.AddDays(-4);
            (s.Level, s.XP, s.WalletCash, s.BankCash, s.Toughness) = (9, 8_000, 3_000, 2_000, 9);
            s.EquippedWeaponKey = "W1";
            s.Gear.Add(new PlayerGear { ItemKey = "W1", Slot = GearSlot.Weapon, Tier = 1 });
            s.Cargo.Add(new PlayerCargo { CargoType = CargoType.Phone, Quantity = 5 });
        });
        await UpdateStateAsync(defender.StateId, s => s.CreatedAt = _factory.Now.AddDays(-4));
        var family = await FamilyAsync(cycle.Id, 700, attacker, defender);
        var membershipIds = await MembershipIdsAsync(family.Id);
        (await Actions().CommitCrimeAsync(attacker.UserId, "C01", CrimeApproach.Standard, "old-crime")).Accepted.Should().BeTrue();
        (await Actions().AttackAsync(defender.UserId, attacker.StateId, PvpTactic.Negotiation, RiskStance.Standard, null, null, null, "old-pvp")).Accepted.Should().BeTrue();
        await Contracts().GetCurrentContractsAsync(attacker.UserId);
        var oldRows = await OldCycleRowsAsync(cycle.Id);
        oldRows.Battles.Should().BeGreaterThan(0);
        oldRows.Contracts.Should().Be(3);
        oldRows.Objectives.Should().BeGreaterThan(0);

        _factory.Now = cycle.EndUtc;
        var result = await Rollover().RolloverLiveAsync(cycle.Id);

        // Identity: same user, family and memberships.
        await using (var db = await DbAsync())
        {
            var next = await db.AfterHoursGameCycles.SingleAsync(c => c.Status == GameCycleStatus.Active);
            (next.Id, next.Kind, next.FiscalYearId, next.StartUtc, next.EndUtc).Should().Be(
                (result.TargetCycleId, GameCycleKind.Live, nextYear.Id, RolloverRules.SeptemberStartUtc(nextYear.StartYear), RolloverRules.SeptemberStartUtc(nextYear.EndYear)));
            (await db.AfterHoursGameCycles.SingleAsync(c => c.Id == cycle.Id)).Status.Should().Be(GameCycleStatus.Finished);
            (await db.AfterHoursPlayerCycleStates.CountAsync(s => s.GameCycleId == next.Id)).Should().Be(0, "states are created lazily, not cloned");
            (await db.AfterHoursFamilyCycleStates.CountAsync(s => s.GameCycleId == next.Id)).Should().Be(0);
        }
        (await MembershipIdsAsync(family.Id)).Should().Equal(membershipIds);

        // Power: a clean state on first access.
        var fresh = await States().GetOrCreateForActiveCycleAsync(attacker.UserId);
        fresh!.GameCycleId.Should().Be(result.TargetCycleId);
        (fresh.Level, fresh.XP, fresh.WalletCash, fresh.BankCash, fresh.Energy, fresh.Heat).Should().Be((1, 0L, 400L, 0L, 240, 0));
        (fresh.Toughness, fresh.Stealth, fresh.Smarts, fresh.Charisma, fresh.TrainingPoints).Should().Be((4, 4, 4, 4, 1));
        fresh.Gear.Should().BeEmpty();
        fresh.Cargo.Should().BeEmpty();
        (fresh.EquippedWeaponKey, fresh.JailUntilUtc, fresh.PvpInitiatedAtUtc, fresh.PvpProtectedUntilUtc, fresh.PvpRecoveryUntilUtc, fresh.PvpCooldownUntilUtc, fresh.DefenceTactic)
            .Should().Be(((string?)null, (DateTime?)null, (DateTime?)null, (DateTime?)null, (DateTime?)null, (DateTime?)null, (PvpTactic?)null));
        var objectives = await ObjectivesAsync().GetOverviewAsync(attacker.UserId);
        objectives!.Week.Index.Should().Be(1);
        objectives.Weekly.Concat(objectives.Daily).Should().OnlyContain(o => o.Progress == 0 && !o.Completed);
        objectives.Family!.Objectives.Should().OnlyContain(o => o.Progress == 0);
        (await ObjectivesAsync().GetLeaderboardsAsync(attacker.UserId))!.Individual.Should().OnlyContain(r => r.AnnualScore == 0);

        // Family: same identity, fresh treasury.
        var overview = await Families().GetOverviewAsync(attacker.UserId);
        (overview!.Family!.Id, overview.Treasury, overview.Members.Count).Should().Be((family.Id, 0L, 2));
        (await Actions().DonateToFamilyAsync(attacker.UserId, 50, "new-donation")).Accepted.Should().BeTrue();
        await using (var db = await DbAsync())
        {
            var treasuries = await db.AfterHoursFamilyCycleStates.Where(s => s.FamilyId == family.Id).OrderBy(s => s.GameCycleId).ToListAsync();
            treasuries.Select(t => (t.GameCycleId, t.TreasuryCash)).Should().Equal((cycle.Id, 700L), (result.TargetCycleId, 50L));
        }
        (await Contracts().GetCurrentContractsAsync(attacker.UserId)).Should().HaveCount(3).And.OnlyContain(c => c.Contract.GameCycleId == result.TargetCycleId);

        // History: the old cycle's rows are exactly as they were.
        (await OldCycleRowsAsync(cycle.Id)).Should().Be(oldRows);
    }

    // ------------------------------------------------------------------ refusals and time

    [Fact]
    public async Task LiveRollover_WaitsForEndUtc_ToTheTick()
    {
        var (cycle, _) = await StartLiveAsync();
        await PlayerAsync("Tick");
        var counts = await CountsAsync();

        _factory.Now = cycle.EndUtc.AddTicks(-1);
        await FluentActions.Invoking(() => Rollover().RolloverLiveAsync(cycle.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*annual rollover waits for its end*");
        (await CountsAsync()).Should().Be(counts);
        (await CycleAsync(cycle.Id)).Status.Should().Be(GameCycleStatus.Active);

        _factory.Now = cycle.EndUtc;
        (await Rollover().RolloverLiveAsync(cycle.Id)).Replayed.Should().BeFalse();
    }

    [Fact]
    public async Task LiveRollover_WithoutTheNextFiscalYear_RefusesAtomically()
    {
        var (cycle, _) = await StartLiveAsync(withNextYear: false);
        await PlayerAsync("NoYear");
        _factory.Now = cycle.EndUtc;
        var counts = await CountsAsync();

        await FluentActions.Invoking(() => Rollover().RolloverLiveAsync(cycle.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("No RTUB fiscal year starts in*");

        (await CountsAsync()).Should().Be(counts);
        (await CycleAsync(cycle.Id)).Status.Should().Be(GameCycleStatus.Active);
    }

    [Fact]
    public async Task LiveRollover_WithTwoCandidateFiscalYears_Refuses()
    {
        var (cycle, next) = await StartLiveAsync();
        await FiscalYearAsync(next.StartYear, next.EndYear);
        _factory.Now = cycle.EndUtc;

        await FluentActions.Invoking(() => Rollover().RolloverLiveAsync(cycle.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*ambiguous*");
        (await CycleAsync(cycle.Id)).Status.Should().Be(GameCycleStatus.Active);
    }

    [Fact]
    public async Task Rollover_RefusesTheWrongKind_Scheduled_AndFinishedWithoutArchive()
    {
        var (live, fiscalYear) = await StartLiveAsync();
        _factory.Now = live.EndUtc;
        var archives = (await CountsAsync()).Archives;
        await FluentActions.Invoking(() => Rollover().TransitionPilotToLiveAsync(live.Id, fiscalYear.Id, live.EndUtc, live.EndUtc.AddDays(10)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*is a Live cycle*");

        var scheduled = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Live, live.StartUtc, live.EndUtc);
        await FluentActions.Invoking(() => Rollover().RolloverLiveAsync(scheduled.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*is Scheduled*");

        await using (var db = await DbAsync())
            await db.AfterHoursGameCycles.Where(c => c.Id == live.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, GameCycleStatus.Finished)); // a legacy unarchived finish
        await FluentActions.Invoking(() => Rollover().RolloverLiveAsync(live.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*is Finished*");

        var (pilot, _) = await StartPilotAsync();
        await FluentActions.Invoking(() => Rollover().RolloverLiveAsync(pilot.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*is a Pilot cycle*");
        (await CountsAsync()).Archives.Should().Be(archives);
    }

    // ------------------------------------------------------------------ idempotency and concurrency

    [Fact]
    public async Task LiveRollover_Repeated_ReturnsTheSameResult_AndChangesNothing()
    {
        var (cycle, _) = await StartLiveAsync();
        var a = await PlayerAsync("Repeat-A");
        await WeeklyScoresAsync(cycle.Id, a.StateId, 60);
        await FamilyAsync(cycle.Id, 0, a);
        _factory.Now = cycle.EndUtc;

        var first = await Rollover().RolloverLiveAsync(cycle.Id);
        var counts = await CountsAsync();
        var archive = await ArchiveOfAsync(first.ArchiveId);
        _factory.Now = cycle.EndUtc.AddDays(3);
        var second = await Rollover().RolloverLiveAsync(cycle.Id);

        second.Should().Be(first with { Replayed = true });
        (await CountsAsync()).Should().Be(counts);
        (await ArchiveOfAsync(first.ArchiveId)).Should().BeEquivalentTo(archive);
        counts.Active.Should().Be(1);
    }

    [Fact]
    public async Task LiveRollover_ConcurrentCalls_ProduceOneArchive_AndOneTargetCycle()
    {
        for (var round = 0; round < 3; round++)
        {
            var (cycle, _) = await StartLiveAsync();
            var a = await PlayerAsync($"Race-{round}-A");
            var b = await PlayerAsync($"Race-{round}-B");
            await WeeklyScoresAsync(cycle.Id, a.StateId, 20);
            await FamilyAsync(cycle.Id, 0, a, b);
            _factory.Now = cycle.EndUtc;
            var before = await CountsAsync();

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Rollover().RolloverLiveAsync(cycle.Id))));

            results.Count(r => !r.Replayed).Should().Be(1);
            results.Select(r => (r.ArchiveId, r.TargetCycleId)).Distinct().Should().ContainSingle();
            var after = await CountsAsync();
            (after.Archives - before.Archives, after.Players - before.Players, after.Families - before.Families, after.Members - before.Members, after.Cycles - before.Cycles, after.Active)
                .Should().Be((1, 2, 1, 2, 1, 1));
        }
    }

    [Fact]
    public async Task PilotToLive_FinishesEarly_StartsClean_IsIdempotent_AndRefusesADifferentTarget()
    {
        var (pilot, fiscalYear) = await StartPilotAsync();
        var player = await PlayerAsync("Pilot-Player");
        await UpdateStateAsync(player.StateId, s => (s.Level, s.WalletCash) = (6, 5_000));
        await WeeklyScoresAsync(pilot.Id, player.StateId, 90);
        var family = await FamilyAsync(pilot.Id, 300, player);
        _factory.Now.Should().BeBefore(pilot.EndUtc, "a Pilot may finish before its planned end");
        var (start, end) = (_factory.Now, RolloverRules.SeptemberStartUtc(fiscalYear.EndYear));

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => Rollover().TransitionPilotToLiveAsync(pilot.Id, fiscalYear.Id, start, end))));
        var counts = await CountsAsync();
        var replay = await Rollover().TransitionPilotToLiveAsync(pilot.Id, fiscalYear.Id, start, end);

        results.Count(r => !r.Replayed).Should().Be(1);
        results.Append(replay).Select(r => (r.ArchiveId, r.TargetCycleId)).Distinct().Should().ContainSingle();
        replay.Replayed.Should().BeTrue();
        (await CountsAsync()).Should().Be(counts);
        var live = await CycleAsync(replay.TargetCycleId);
        (live.Kind, live.Status, live.FiscalYearId, live.StartUtc, live.EndUtc).Should().Be((GameCycleKind.Live, GameCycleStatus.Active, fiscalYear.Id, start, end));
        (await CycleAsync(pilot.Id)).Status.Should().Be(GameCycleStatus.Finished);

        await FluentActions.Invoking(() => Rollover().TransitionPilotToLiveAsync(pilot.Id, fiscalYear.Id, start, end.AddDays(1)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already rolled over*");

        // Nothing from the Pilot carries over: power, score or treasury.
        var fresh = await States().GetOrCreateForActiveCycleAsync(player.UserId);
        (fresh!.GameCycleId, fresh.Level, fresh.WalletCash).Should().Be((live.Id, 1, 400L));
        (await ObjectivesAsync().GetLeaderboardsAsync(player.UserId))!.Individual.Single().AnnualScore.Should().Be(0);
        var overview = await Families().GetOverviewAsync(player.UserId);
        (overview!.Family!.Id, overview.Treasury).Should().Be((family.Id, 0L));
    }

    [Fact]
    public async Task PilotToLive_ValidatesTheTarget_AndChangesNothingWhenRefused()
    {
        var (pilot, fiscalYear) = await StartPilotAsync();
        var now = _factory.Now;
        var counts = await CountsAsync();

        await FluentActions.Invoking(() => Rollover().TransitionPilotToLiveAsync(pilot.Id, fiscalYear.Id, DateTime.SpecifyKind(now, DateTimeKind.Unspecified), now.AddDays(10)))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Rollover().TransitionPilotToLiveAsync(pilot.Id, fiscalYear.Id, now, now))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Rollover().TransitionPilotToLiveAsync(pilot.Id, 999_999, now, now.AddDays(10)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not exist*");
        await FluentActions.Invoking(() => Rollover().TransitionPilotToLiveAsync(pilot.Id, fiscalYear.Id, now.AddDays(-10), now.AddTicks(-1)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already be over*");

        (await CountsAsync()).Should().Be(counts);
        (await CycleAsync(pilot.Id)).Status.Should().Be(GameCycleStatus.Active);
    }

    // ------------------------------------------------------------------ failure safety

    [Theory]
    [InlineData("INSERT INTO \"AfterHoursYearbookFamilyMembers\"")]
    [InlineData("INSERT INTO \"AfterHoursGameCycles\"")]
    [InlineData("UPDATE \"AfterHoursCycleArchives\"")]
    public async Task AFailureMidRollover_RollsEverythingBack_AndARetrySucceedsOnce(string failingStatement)
    {
        var (cycle, _) = await StartLiveAsync();
        var a = await PlayerAsync("Crash-A");
        await WeeklyScoresAsync(cycle.Id, a.StateId, 30);
        await FamilyAsync(cycle.Id, 0, a);
        _factory.Now = cycle.EndUtc;
        var counts = await CountsAsync();

        _factory.Failure.FailOn(failingStatement);
        await FluentActions.Invoking(() => Rollover().RolloverLiveAsync(cycle.Id)).Should().ThrowAsync<Exception>();
        _factory.Failure.Clear();

        (await CountsAsync()).Should().Be(counts, "no partial archive, no partial next cycle");
        (await CycleAsync(cycle.Id)).Status.Should().Be(GameCycleStatus.Active);

        var retry = await Rollover().RolloverLiveAsync(cycle.Id);
        var again = await Rollover().RolloverLiveAsync(cycle.Id);
        (retry.Replayed, again.Replayed).Should().Be((false, true));
        var after = await CountsAsync();
        (after.Archives - counts.Archives, after.Players - counts.Players, after.Families - counts.Families, after.Members - counts.Members, after.Cycles - counts.Cycles, after.Active)
            .Should().Be((1, 1, 1, 1, 1, 1));
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Player(string UserId, int StateId, string Name);

    private IAfterHoursRolloverService Rollover() => Service<IAfterHoursRolloverService>();
    private IGameCycleService Cycles() => Service<IGameCycleService>();
    private IPlayerCycleStateService States() => Service<IPlayerCycleStateService>();
    private IAfterHoursActionService Actions() => Service<IAfterHoursActionService>();
    private IObjectiveService ObjectivesAsync() => Service<IObjectiveService>();
    private IFamilyService Families() => Service<IFamilyService>();
    private IBuyerContractService Contracts() => Service<IBuyerContractService>();
    private T Service<T>() where T : notnull => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<T>();

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private Task<IReadOnlyList<CycleArchive>> ArchivesAsync() => Service<IYearbookService>().GetArchivesAsync();

    private async Task<CycleArchive> ArchiveOfAsync(int archiveId) => (await ArchivesAsync()).Single(a => a.Id == archiveId);

    /// <summary>A Live cycle over the Lisbon academic year of a fresh fiscal year, Active, clock 100 days in.</summary>
    private async Task<(GameCycle Cycle, FiscalYear Next)> StartLiveAsync(bool withNextYear = true)
    {
        var year = Interlocked.Add(ref _nextYear, 3);
        await FinishActiveAsync();
        var fiscalYear = await FiscalYearAsync(year, year + 1);
        await FiscalYearAsync(year + 2, year + 3); // a decoy: never the "next" year
        var next = withNextYear ? await FiscalYearAsync(year + 1, year + 2) : fiscalYear;
        var cycle = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Live, RolloverRules.SeptemberStartUtc(year), RolloverRules.SeptemberStartUtc(year + 1));
        await Cycles().ActivateAsync(cycle.Id);
        _factory.Now = cycle.StartUtc.AddDays(100);
        return (await CycleAsync(cycle.Id), next);
    }

    /// <summary>An Active Pilot of 50 days in a fresh fiscal year, clock 5 days in.</summary>
    private async Task<(GameCycle Cycle, FiscalYear FiscalYear)> StartPilotAsync()
    {
        var year = Interlocked.Add(ref _nextYear, 3);
        await FinishActiveAsync();
        var fiscalYear = await FiscalYearAsync(year, year + 1);
        var start = RolloverRules.SeptemberStartUtc(year).AddDays(10);
        var cycle = await Cycles().CreateCycleAsync(fiscalYear.Id, GameCycleKind.Pilot, start, start.AddDays(50));
        await Cycles().ActivateAsync(cycle.Id);
        _factory.Now = start.AddDays(5);
        return (await CycleAsync(cycle.Id), fiscalYear);
    }

    /// <summary>Test isolation only: a raw status change, not a rollover.</summary>
    private async Task FinishActiveAsync()
    {
        await using var db = await DbAsync();
        await db.AfterHoursGameCycles.Where(c => c.Status == GameCycleStatus.Active).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, GameCycleStatus.Finished));
    }

    private async Task<FiscalYear> FiscalYearAsync(int start, int end)
    {
        await using var db = await DbAsync();
        var fiscalYear = FiscalYear.Create(start, end);
        db.FiscalYears.Add(fiscalYear);
        await db.SaveChangesAsync();
        return fiscalYear;
    }

    private async Task<GameCycle> CycleAsync(int id)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursGameCycles.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    private async Task<(string UserId, string Name)> UserAsync(string label)
    {
        var name = $"ah9-{label}-{Guid.NewGuid():N}"[..30];
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        return (user.Id, name);
    }

    /// <summary>A user with a lazily created state in the active cycle.</summary>
    private async Task<Player> PlayerAsync(string label)
    {
        var (userId, name) = await UserAsync(label);
        var state = await States().GetOrCreateForActiveCycleAsync(userId);
        return new Player(userId, state!.Id, name);
    }

    private async Task UpdateStateAsync(int stateId, Action<PlayerCycleState> change)
    {
        await using var db = await DbAsync();
        var state = await db.AfterHoursPlayerCycleStates.Include(s => s.Gear).Include(s => s.Cargo).SingleAsync(s => s.Id == stateId);
        change(state);
        await db.SaveChangesAsync();
    }

    private static readonly (string Key, ObjectiveCategory Category)[] WeeklySlots =
    [
        ("WA1", ObjectiveCategory.SoloCrime), ("WA2", ObjectiveCategory.SoloCrime), ("WA3", ObjectiveCategory.CargoContracts),
        ("WPVP", ObjectiveCategory.Pvp), ("WA5", ObjectiveCategory.FamilyParticipation)
    ];

    /// <summary>Stored weekly awarded points: week i + 1 scores <paramref name="scores"/>[i] (at most 100).</summary>
    private async Task WeeklyScoresAsync(int cycleId, int stateId, params int[] scores)
    {
        await using var db = await DbAsync();
        for (var week = 1; week <= scores.Length; week++)
        {
            var left = scores[week - 1];
            foreach (var (key, category) in WeeklySlots.TakeWhile(_ => left > 0))
            {
                var points = Math.Min(20, left);
                left -= points;
                db.AfterHoursPlayerObjectiveProgress.Add(new PlayerObjectiveProgress
                {
                    GameCycleId = cycleId, PlayerCycleStateId = stateId, Period = ObjectivePeriod.Weekly, PeriodKey = week,
                    ObjectiveKey = key, Category = category, Progress = 1, Target = 1, CompletedAtUtc = _factory.Now, PointsAwarded = points
                });
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Stored family weekly points: week i + 1 scores <paramref name="scores"/>[i] (at most 100).</summary>
    private async Task FamilyScoresAsync(int cycleId, int familyId, params int[] scores)
    {
        await using var db = await DbAsync();
        for (var week = 1; week <= scores.Length; week++)
        {
            var left = scores[week - 1];
            for (var slot = 1; left > 0; slot++)
            {
                var points = Math.Min(25, left);
                left -= points;
                db.AfterHoursFamilyObjectiveProgress.Add(new FamilyObjectiveProgress
                {
                    FamilyId = familyId, GameCycleId = cycleId, Week = week, ObjectiveKey = $"FA{slot}",
                    Progress = 1, Target = 1, CompletedAtUtc = _factory.Now, PointsAwarded = points
                });
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>A family (first player Boss, the rest Members, joined at the cycle start) with this cycle's treasury.</summary>
    private async Task<Family> FamilyAsync(int cycleId, long treasury, params Player[] players)
    {
        var name = $"F{Guid.NewGuid():N}"[..12];
        await using var db = await DbAsync();
        var family = new Family { Name = name, NormalizedName = name.ToUpperInvariant(), CreatedAtUtc = _factory.Now.AddDays(-1), CreatedByUserId = players[0].UserId };
        db.AfterHoursFamilies.Add(family);
        await db.SaveChangesAsync();
        for (var i = 0; i < players.Length; i++)
            db.AfterHoursFamilyMemberships.Add(new FamilyMembership
            {
                FamilyId = family.Id, UserId = players[i].UserId, Role = i == 0 ? FamilyRole.Boss : FamilyRole.Member, JoinedAtUtc = _factory.Now.AddDays(-1).AddMinutes(i)
            });
        db.AfterHoursFamilyCycleStates.Add(new FamilyCycleState { FamilyId = family.Id, GameCycleId = cycleId, TreasuryCash = treasury });
        await db.SaveChangesAsync();
        return family;
    }

    private async Task AddMembershipAsync(int familyId, string userId, DateTime joined, DateTime? left)
    {
        await using var db = await DbAsync();
        db.AfterHoursFamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = FamilyRole.Member, JoinedAtUtc = joined, LeftAtUtc = left });
        await db.SaveChangesAsync();
    }

    private async Task<List<int>> MembershipIdsAsync(int familyId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursFamilyMemberships.Where(m => m.FamilyId == familyId && m.LeftAtUtc == null).OrderBy(m => m.Id).Select(m => m.Id).ToListAsync();
    }

    private sealed record OldRows(string States, int Battles, int Rounds, int Contracts, int Completions, int Objectives, long ObjectivePoints, int Receipts, long Treasury, int Cargo, int Gear);

    /// <summary>A fingerprint of every gameplay row of a cycle, to prove rollover never touches history.</summary>
    private async Task<OldRows> OldCycleRowsAsync(int cycleId)
    {
        await using var db = await DbAsync();
        var states = await db.AfterHoursPlayerCycleStates.AsNoTracking().Where(s => s.GameCycleId == cycleId).OrderBy(s => s.Id).ToListAsync();
        var stateIds = states.Select(s => s.Id).ToList();
        return new OldRows(
            string.Join("|", states.Select(s => $"{s.Id}:{s.Level}:{s.XP}:{s.WalletCash}:{s.BankCash}:{s.Energy}:{s.Heat}:{s.Toughness}:{s.EquippedWeaponKey}:{s.PvpRecoveryUntilUtc:O}:{s.PvpProtectedUntilUtc:O}")),
            await db.AfterHoursPvpBattles.CountAsync(b => b.GameCycleId == cycleId),
            await db.Set<PvpBattleRound>().CountAsync(),
            await db.AfterHoursBuyerContracts.CountAsync(c => c.GameCycleId == cycleId),
            await db.AfterHoursBuyerContractCompletions.CountAsync(c => stateIds.Contains(c.PlayerCycleStateId)),
            await db.AfterHoursPlayerObjectiveProgress.CountAsync(r => r.GameCycleId == cycleId),
            await db.AfterHoursPlayerObjectiveProgress.Where(r => r.GameCycleId == cycleId).SumAsync(r => r.XpAwarded + r.PointsAwarded),
            await db.AfterHoursPlayerActionReceipts.CountAsync(r => stateIds.Contains(r.PlayerCycleStateId)),
            await db.AfterHoursFamilyCycleStates.Where(s => s.GameCycleId == cycleId).SumAsync(s => s.TreasuryCash),
            await db.AfterHoursPlayerCargo.Where(c => stateIds.Contains(c.PlayerCycleStateId)).SumAsync(c => c.Quantity),
            await db.AfterHoursPlayerGear.CountAsync(g => stateIds.Contains(g.PlayerCycleStateId)));
    }

    private async Task<(int Archives, int Players, int Families, int Members, int Cycles, int Active)> CountsAsync()
    {
        await using var db = await DbAsync();
        return (await db.AfterHoursCycleArchives.CountAsync(), await db.AfterHoursYearbookPlayers.CountAsync(),
            await db.AfterHoursYearbookFamilies.CountAsync(), await db.AfterHoursYearbookFamilyMembers.CountAsync(),
            await db.AfterHoursGameCycles.CountAsync(), await db.AfterHoursGameCycles.CountAsync(c => c.Status == GameCycleStatus.Active));
    }
}

/// <summary>The After Hours play host plus a switch that makes one kind of SQL statement fail, to crash a rollover midway.</summary>
public class AfterHoursRolloverFactory : AfterHoursPlayFactory
{
    public FailingCommandInterceptor Failure { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(ConnectionString);
                options.EnableSensitiveDataLogging();
                options.AddInterceptors(Failure);
            });
        });
    }

    public sealed class FailingCommandInterceptor : DbCommandInterceptor
    {
        private volatile string? _fragment;

        public void FailOn(string sqlFragment) => _fragment = sqlFragment;

        public void Clear() => _fragment = null;

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) =>
            Check(command, result);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Check(command, result));

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
            Check(command, result);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Check(command, result));

        private T Check<T>(DbCommand command, T result) =>
            _fragment is { } fragment && command.CommandText.Contains(fragment)
                ? throw new InvalidOperationException($"Injected failure on: {fragment}")
                : result;
    }
}
