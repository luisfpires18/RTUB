using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// AH-008 objectives and championships against the real SQLite schema. The daily set depends on the
/// Lisbon date and the weekly set on the week's parity, so each test moves the frozen clock to an instant
/// that has what it needs. Dice default to 1: crimes succeed, equal PvP fights go to the attacker.
/// </summary>
public class AfterHoursObjectiveTests : IClassFixture<AfterHoursPlayFactory>
{
    private readonly AfterHoursPlayFactory _factory;
    private readonly GameCycle _cycle;

    public AfterHoursObjectiveTests(AfterHoursPlayFactory factory)
    {
        _factory = factory;
        factory.Dice.Reset();
        factory.Now = AfterHoursPlayFactory.Start;
        factory.EnsureActiveCycleAsync().GetAwaiter().GetResult();
        using var scope = factory.Services.CreateScope();
        _cycle = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AfterHoursGameCycles.AsNoTracking()
            .Single(c => c.Status == GameCycleStatus.Active);
    }

    // ------------------------------------------------------------------ daily

    [Fact]
    public async Task Crimes_AdvanceDailyAndWeekly_CompleteOnce_AndReplaysDoNotCount()
    {
        await At(week: 1, "D01");
        var p = await PlayerAsync();

        for (var i = 0; i < 4; i++) await Accept(Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, $"c{i}"));
        (await Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, "c0")).Replayed.Should().BeTrue();

        var d01 = await RowAsync(p, ObjectivePeriod.Daily, Today.DayNumber, "D01");
        (d01.Progress, d01.XpAwarded, d01.CompletedAtUtc).Should().Be((3L, 15L, _factory.Now));
        (await RowAsync(p, ObjectivePeriod.Weekly, 1, "WA1")).Progress.Should().Be(4);
        (await RowAsync(p, ObjectivePeriod.Weekly, 1, "WA2")).Progress.Should().Be(4 * 35);
        (await StateAsync(p)).XP.Should().Be(4 * 20 + 15, "4 crimes and D01; 40 energy is short of D03's 60");
    }

    [Fact]
    public async Task Daily_ResetsAtLisbonMidnight_WithANewInstance()
    {
        await At(week: 1, "D01");
        var p = await PlayerAsync();
        await Accept(Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, "day1"));
        var firstDay = Today;
        var firstRows = await DailyRowsAsync(p);

        _factory.Now = LisbonCalendar.NextMidnightUtc(firstDay);
        await Accept(Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, "day2"));

        var rows = await DailyRowsAsync(p);
        rows.Select(r => r.PeriodKey).Distinct().Should().BeEquivalentTo(new[] { firstDay.DayNumber, firstDay.AddDays(1).DayNumber });
        rows.Where(r => r.PeriodKey == firstDay.DayNumber).Should().BeEquivalentTo(firstRows, "yesterday's progress is left as it was");
    }

    [Fact]
    public async Task CoverJobAndEnergy_DailyObjectives()
    {
        await At(week: null, "D02", "D03");
        var p = await PlayerAsync(heat: 0);

        await Accept(Actions().TakeCoverJobAsync(p.UserId, "cold")); // heat 0: reduces nothing
        (await FindRowAsync(p, ObjectivePeriod.Daily, Today.DayNumber, "D02")).Should().BeNull();

        await Update(p, s => (s.Heat, s.HeatUpdatedAtUtc, s.CoverJobDailyUsedOn) = (60, _factory.Now, null));
        await Accept(Actions().TakeCoverJobAsync(p.UserId, "hot"));
        (await RowAsync(p, ObjectivePeriod.Daily, Today.DayNumber, "D02")).XpAwarded.Should().Be(10);

        for (var i = 0; i < 4; i++) await Accept(Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, $"e{i}"));
        var d03 = await RowAsync(p, ObjectivePeriod.Daily, Today.DayNumber, "D03");
        (d03.Progress, d03.XpAwarded).Should().Be((60L, 15L), "10 + 10 cover-job energy + 4 × 10 crime energy");
    }

    // ------------------------------------------------------------------ cargo, contracts, donations

    [Fact]
    public async Task Fence_And_Donations_WeekA_FeedPlayerAndFamily()
    {
        await At(week: 1, "D04");
        var boss = await PlayerAsync(level: 5, wallet: 5_000);
        var familyId = await CreateFamilyAsync(boss);
        await GiveCargoAsync(boss, CargoType.Phone, 5);

        await Accept(Actions().SellToFenceAsync(boss.UserId, CargoType.Phone, 3, "f1"));
        await Accept(Actions().DonateToFamilyAsync(boss.UserId, 1_200, "d1"));

        (await RowAsync(boss, ObjectivePeriod.Daily, Today.DayNumber, "D04")).XpAwarded.Should().Be(10);
        (await RowAsync(boss, ObjectivePeriod.Weekly, 1, "WA3")).Progress.Should().Be(3);
        var wa5 = await RowAsync(boss, ObjectivePeriod.Weekly, 1, "WA5");
        (wa5.Progress, wa5.PointsAwarded).Should().Be((1_000L, 20));
        (await FamilyRowAsync(familyId, 1, "FA3")).Progress.Should().Be(3);
        (await FamilyRowAsync(familyId, 1, "FA2")).Progress.Should().Be(1_200);
    }

    [Fact]
    public async Task Contracts_WeekB_FeedPlayerAndFamily()
    {
        await At(week: 2, "D04");
        var boss = await PlayerAsync(level: 5, wallet: 5_000);
        var familyId = await CreateFamilyAsync(boss);
        var contract = (await Scope().GetRequiredService<IBuyerContractService>().GetCurrentContractsAsync(boss.UserId))[0].Contract;
        await GiveCargoAsync(boss, contract.CargoType, contract.Quantity);

        await Accept(Actions().DeliverContractAsync(boss.UserId, contract.Id, "k1"));

        (await RowAsync(boss, ObjectivePeriod.Weekly, 2, "WB3")).Progress.Should().Be(1);
        (await RowAsync(boss, ObjectivePeriod.Daily, Today.DayNumber, "D04")).Progress.Should().Be(1);
        (await FamilyRowAsync(familyId, 2, "FB3")).Progress.Should().Be(1);
    }

    // ------------------------------------------------------------------ PvP

    [Fact]
    public async Task Pvp_PointsPerDistinctTarget_ScaledByLoot_CappedAt20_SameFamilyZero()
    {
        await At(week: 1);
        var attacker = await PlayerAsync(pvpReady: true, level: 5, wallet: 5_000, skills: 5); // power 20
        var even = await PlayerAsync(pvpReady: true, skills: 5);   // 20 → multiplier 1 → 10
        var weaker = await PlayerAsync(pvpReady: true);            // 16 → 0.64 → 6
        var other = await PlayerAsync(pvpReady: true, skills: 5);  // 10, but capped
        var mate = await PlayerAsync(pvpReady: true, level: 5);
        await CreateFamilyAsync(attacker);
        await JoinFamilyAsync(attacker, mate);

        await Attack(attacker, mate, "m");                          // same family: nothing
        await Attack(attacker, even, "e", minutes: 6);
        await Attack(attacker, weaker, "w", minutes: 12);
        await Attack(attacker, other, "o", minutes: 18);

        var pvp = await RowAsync(attacker, ObjectivePeriod.Weekly, 1, "WPVP");
        (pvp.PointsAwarded, pvp.CompletedAtUtc is not null).Should().Be((20, true), "10 + 6 = 16, then capped at 20");
        var credits = await CreditsAsync(attacker);
        credits.Select(c => c.IndividualPoints).Should().Equal(10, 6, 4);
        credits.Should().NotContain(c => c.DefenderUserId == mate.UserId);

        await Attack(attacker, even, "e2", minutes: 60 * 25); // same target again, same week
        (await CreditsAsync(attacker)).Where(c => c.DefenderUserId == even.UserId).Should().ContainSingle(c => c.CountsForIndividual);
    }

    [Fact]
    public async Task Pvp_DailyWin_CountsForAttackerOrDefender()
    {
        await At(week: null, "D05");
        var attacker = await PlayerAsync(pvpReady: true);
        var strong = await PlayerAsync(pvpReady: true, skills: 12);
        var victim = await PlayerAsync(pvpReady: true);

        await Attack(attacker, strong, "lose");                  // the defender wins
        (await RowAsync(strong, ObjectivePeriod.Daily, Today.DayNumber, "D05")).XpAwarded.Should().Be(15);
        (await FindRowAsync(attacker, ObjectivePeriod.Daily, Today.DayNumber, "D05")).Should().BeNull();

        await Attack(attacker, victim, "win", minutes: 60);     // past recovery and cooldown
        (await RowAsync(attacker, ObjectivePeriod.Daily, Today.DayNumber, "D05")).XpAwarded.Should().Be(15);
    }

    [Fact]
    public async Task FamilyPvp_ATargetCountsOncePerFamilyPerWeek()
    {
        await At(week: 1);
        var a = await PlayerAsync(pvpReady: true, level: 5, wallet: 5_000);
        var b = await PlayerAsync(pvpReady: true, level: 5);
        var target = await PlayerAsync(pvpReady: true);
        var familyId = await CreateFamilyAsync(a);
        await JoinFamilyAsync(a, b);

        await Attack(a, target, "fa");
        await Attack(b, target, "fb", minutes: 7 * 60);          // after the target's 6 h protection

        (await FamilyRowAsync(familyId, 1, "FA4")).Progress.Should().Be(1);
        (await RowAsync(a, ObjectivePeriod.Weekly, 1, "WPVP")).PointsAwarded.Should().Be(10);
        (await RowAsync(b, ObjectivePeriod.Weekly, 1, "WPVP")).PointsAwarded.Should().Be(10, "individual credit is per attacker");
    }

    // ------------------------------------------------------------------ family attribution

    [Fact]
    public async Task FamilyProgress_StaysWithTheFamilyWhereItWasEarned()
    {
        await At(week: 1);
        var bossA = await PlayerAsync(level: 5, wallet: 5_000);
        var bossB = await PlayerAsync(level: 5, wallet: 5_000);
        var mover = await PlayerAsync(level: 5);
        var familyA = await CreateFamilyAsync(bossA);
        var familyB = await CreateFamilyAsync(bossB);
        await JoinFamilyAsync(bossA, mover);

        for (var i = 0; i < 2; i++) await Accept(Actions().CommitCrimeAsync(mover.UserId, "C01", CrimeApproach.Standard, $"a{i}"));
        await Accept(Actions().LeaveFamilyAsync(mover.UserId, "leave"));
        await Accept(Actions().CommitCrimeAsync(mover.UserId, "C01", CrimeApproach.Standard, "solo"));

        _factory.Now += TimeSpan.FromHours(72);
        var week = ObjectiveCatalogue.WeekOf(_cycle, _factory.Now).Index;
        await JoinFamilyAsync(bossB, mover);
        await Update(mover, s => (s.Energy, s.EnergyUpdatedAtUtc) = (240, _factory.Now));
        for (var i = 0; i < 3; i++) await Accept(Actions().CommitCrimeAsync(mover.UserId, "C01", CrimeApproach.Standard, $"b{i}"));

        (await FamilyRowAsync(familyA, 1, "FA1")).Progress.Should().Be(2, "earned while in A, kept by A");
        (await FamilyRowAsync(familyB, week, ObjectiveCatalogue.FamilyFor(week)[0].Key)).Progress.Should().Be(3);
    }

    [Fact]
    public async Task ConcurrentFinalActions_AwardASharedFamilyObjectiveOnce()
    {
        await At(week: 1);
        var boss = await PlayerAsync(level: 5, wallet: 5_000);
        var mate = await PlayerAsync(level: 5);
        var familyId = await CreateFamilyAsync(boss);
        await JoinFamilyAsync(boss, mate);
        await SeedFamilyRowAsync(familyId, 1, "FA1", progress: 29, target: 30);

        await Task.WhenAll(
            Task.Run(() => Actions().CommitCrimeAsync(boss.UserId, "C01", CrimeApproach.Standard, "last-a")),
            Task.Run(() => Actions().CommitCrimeAsync(mate.UserId, "C01", CrimeApproach.Standard, "last-b")));

        var row = await FamilyRowAsync(familyId, 1, "FA1");
        (row.Progress, row.PointsAwarded, row.CompletedAtUtc is not null).Should().Be((30L, 25, true));
    }

    [Fact]
    public async Task LeavingDuringGameplay_AttributesEachCrimeByItsSerializedOrder()
    {
        await At(week: 2);
        var boss = await PlayerAsync(level: 5, wallet: 5_000);
        var member = await PlayerAsync(level: 5);
        var familyId = await CreateFamilyAsync(boss);
        await JoinFamilyAsync(boss, member);

        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() => Actions().CommitCrimeAsync(member.UserId, "C01", CrimeApproach.Standard, $"x{i}")))
            .Append(Task.Run(() => Actions().LeaveFamilyAsync(member.UserId, "x-leave"))).ToList();
        await Task.WhenAll(tasks);

        // The family's count and the member's "crimes while in a family" come from the same snapshot.
        var family = (await FindFamilyRowAsync(familyId, 2, "FB1"))?.Progress ?? 0;
        var asMember = (await FindRowAsync(member, ObjectivePeriod.Weekly, 2, "WB5"))?.Progress ?? 0;
        family.Should().Be(asMember).And.BeInRange(0, 8);
        (await RowAsync(member, ObjectivePeriod.Weekly, 2, "WB1")).Progress.Should().Be(8);
    }

    // ------------------------------------------------------------------ concurrency

    [Fact]
    public async Task ParallelCrimesFenceAndDonations_AddExactly()
    {
        await At(week: 1);
        var boss = await PlayerAsync(level: 5, wallet: 10_000);
        var familyId = await CreateFamilyAsync(boss);
        await GiveCargoAsync(boss, CargoType.Phone, 6);

        var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() => (i % 3) switch
        {
            0 => Actions().CommitCrimeAsync(boss.UserId, "C01", CrimeApproach.Standard, $"pc{i}"),
            1 => Actions().SellToFenceAsync(boss.UserId, CargoType.Phone, 1, $"pc{i}"),
            _ => Actions().DonateToFamilyAsync(boss.UserId, 70, $"pc{i}")
        })));

        results.Should().OnlyContain(r => r.Accepted);
        (await RowAsync(boss, ObjectivePeriod.Weekly, 1, "WA1")).Progress.Should().Be(8);
        (await RowAsync(boss, ObjectivePeriod.Weekly, 1, "WA3")).Progress.Should().Be(8, "6 given + 8 phones from crimes, 8 sold");
        (await FamilyRowAsync(familyId, 1, "FA2")).Progress.Should().Be(8 * 70);
        (await FamilyRowAsync(familyId, 1, "FA1")).Progress.Should().Be(8);
    }

    [Fact]
    public async Task TwentyParallelCrimes_ExactProgress_AndEachDailyXpOnce()
    {
        await At(week: 1, "D01");
        var p = await PlayerAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => Task.Run(() => Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, $"burst{i}"))));

        results.Should().OnlyContain(r => r.Accepted);
        (await RowAsync(p, ObjectivePeriod.Weekly, 1, "WA2")).Progress.Should().Be(20 * 35, "no lost progress");
        var wa1 = await RowAsync(p, ObjectivePeriod.Weekly, 1, "WA1");
        (wa1.Progress, wa1.PointsAwarded).Should().Be((12L, 20));
        var dailyXp = ObjectiveCatalogue.DailyFor(Today).Count(d => d.Key is "D01" or "D03") * 15L;
        (await StateAsync(p)).XP.Should().Be(20 * 20 + dailyXp, "each daily objective pays its XP exactly once");
    }

    // ------------------------------------------------------------------ leaderboards

    [Fact]
    public async Task Leaderboards_BestTwelve_SharedRanks_NoEconomyFields()
    {
        await At(week: 1);
        var top = await PlayerAsync();
        var tieA = await PlayerAsync();
        var tieB = await PlayerAsync();
        for (var week = 1; week <= 15; week++) await SeedPlayerWeekAsync(top, week, week);  // best 12 = weeks 4..15
        await SeedPlayerWeekAsync(tieA, 2, 40);
        await SeedPlayerWeekAsync(tieB, 3, 40);
        await SeedPlayerWeekAsync(tieB, 1, 7);   // stored points, whatever today's catalogue says

        var boards = await Scope().GetRequiredService<IObjectiveService>().GetLeaderboardsAsync(tieA.UserId);

        var rows = boards!.Individual.Where(r => new[] { top.StateId, tieA.StateId, tieB.StateId }.Contains(r.Id)).ToList();
        var topRow = rows.Single(r => r.Id == top.StateId);
        (topRow.AnnualScore, topRow.ScoringWeeks, topRow.CurrentWeekScore).Should().Be((Enumerable.Range(4, 12).Sum(), 15, 1));
        topRow.CountedWeeks.Should().Equal(Enumerable.Range(4, 12));
        var a = rows.Single(r => r.Id == tieA.StateId);
        var b = rows.Single(r => r.Id == tieB.StateId);
        (a.AnnualScore, b.AnnualScore).Should().Be((40, 47));
        a.IsMine.Should().BeTrue();
        typeof(StandingRow).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Cash") || n.Contains("Wallet") || n.Contains("Bank"));

        await SeedPlayerWeekAsync(tieA, 5, 7);
        var tied = (await Scope().GetRequiredService<IObjectiveService>().GetLeaderboardsAsync(tieA.UserId))!.Individual
            .Where(r => r.Id == tieA.StateId || r.Id == tieB.StateId).ToList();
        tied.Select(r => r.AnnualScore).Distinct().Should().ContainSingle();
        tied.Select(r => r.Rank).Distinct().Should().ContainSingle("equal scores share a rank; nobody is declared ahead");
    }

    [Fact]
    public async Task Objectives_LeaveFinanceAndMyTunoUntouched()
    {
        await At(week: 1);
        var p = await PlayerAsync(level: 5, wallet: 5_000);
        await using var db = await DbAsync();
        var before = (await db.Transactions.CountAsync(), await db.Characters.CountAsync(), await db.Users.Where(u => u.Id == p.UserId).Select(u => u.FidelisBalance).SingleAsync());

        await CreateFamilyAsync(p);
        for (var i = 0; i < 3; i++) await Accept(Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, $"fin{i}"));
        await Accept(Actions().DonateToFamilyAsync(p.UserId, 100, "fin-d"));

        await using var after = await DbAsync();
        (await after.Transactions.CountAsync(), await after.Characters.CountAsync(), await after.Users.Where(u => u.Id == p.UserId).Select(u => u.FidelisBalance).SingleAsync())
            .Should().Be(before);
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Player(string UserId, int StateId);

    private DateOnly Today => LisbonCalendar.DateOf(_factory.Now);

    /// <summary>Moves the clock to noon UTC on the first day from Start whose week parity and daily set fit.</summary>
    private Task At(int? week, params string[] dailyKeys)
    {
        for (var d = 0; d < 50; d++)
        {
            var t = AfterHoursPlayFactory.Start.AddDays(d);
            var index = ObjectiveCatalogue.WeekOf(_cycle, t).Index;
            if (week is { } w && index % 2 != w % 2) continue;
            var keys = ObjectiveCatalogue.DailyFor(LisbonCalendar.DateOf(t)).Select(x => x.Key).ToList();
            if (dailyKeys.All(keys.Contains) && t.AddDays(4) < _cycle.EndUtc)
            {
                _factory.Now = t;
                return Task.CompletedTask;
            }
        }
        throw new InvalidOperationException("No suitable day");
    }

    private IServiceProvider Scope() => _factory.Services.CreateScope().ServiceProvider;
    private IAfterHoursActionService Actions() => Scope().GetRequiredService<IAfterHoursActionService>();

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private static async Task<AfterHoursActionResult> Accept(Task<AfterHoursActionResult> action)
    {
        var result = await action;
        result.Accepted.Should().BeTrue(result.Error);
        return result;
    }

    private async Task<Player> PlayerAsync(bool pvpReady = false, int level = 1, long wallet = 400, int skills = 4, int heat = 0)
    {
        var name = $"ah8-{Guid.NewGuid():N}";
        var users = Scope().GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        var state = await Scope().GetRequiredService<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(user.Id);
        var player = new Player(user.Id, state!.Id);
        await Update(player, s =>
        {
            (s.Level, s.XP, s.WalletCash, s.Heat) = (level, AfterHoursLevels.XpForLevel(level), wallet, heat);
            (s.Toughness, s.Stealth, s.Smarts, s.Charisma) = (skills, skills, skills, skills);
            if (pvpReady) s.CreatedAt = _factory.Now.AddDays(-4);
        });
        return player;
    }

    private async Task Update(Player p, Action<PlayerCycleState> change)
    {
        await using var db = await DbAsync();
        var s = await db.AfterHoursPlayerCycleStates.SingleAsync(x => x.Id == p.StateId);
        change(s);
        await db.SaveChangesAsync();
    }

    private async Task<PlayerCycleState> StateAsync(Player p)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCycleStates.AsNoTracking().SingleAsync(s => s.Id == p.StateId);
    }

    private async Task Attack(Player attacker, Player defender, string key, int minutes = 0)
    {
        if (minutes > 0) _factory.Now += TimeSpan.FromMinutes(minutes);
        await Accept(Actions().AttackAsync(attacker.UserId, defender.StateId, PvpTactic.Negotiation, RiskStance.Standard, null, null, null, key));
    }

    private async Task<int> CreateFamilyAsync(Player boss) =>
        (await Accept(Actions().CreateFamilyAsync(boss.UserId, $"F{Guid.NewGuid():N}"[..16], null, $"fam-{Guid.NewGuid():N}"))).Receipt!.FamilyId!.Value;

    private async Task JoinFamilyAsync(Player boss, Player joiner)
    {
        var familyId = (await Accept(Actions().InviteToFamilyAsync(boss.UserId, joiner.StateId, $"inv-{Guid.NewGuid():N}"))).Receipt!.FamilyId;
        await using var db = await DbAsync();
        var invite = await db.AfterHoursFamilyInvitations.SingleAsync(i => i.FamilyId == familyId && i.InvitedUserId == joiner.UserId && i.Status == FamilyInvitationStatus.Pending);
        await Accept(Actions().AcceptFamilyInvitationAsync(joiner.UserId, invite.Id, $"acc-{Guid.NewGuid():N}"));
    }

    private async Task GiveCargoAsync(Player p, CargoType cargo, int quantity)
    {
        await using var db = await DbAsync();
        db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = p.StateId, CargoType = cargo, Quantity = quantity });
        await db.SaveChangesAsync();
    }

    private async Task<PlayerObjectiveProgress?> FindRowAsync(Player p, ObjectivePeriod period, int key, string objective)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerObjectiveProgress.AsNoTracking()
            .SingleOrDefaultAsync(r => r.PlayerCycleStateId == p.StateId && r.Period == period && r.PeriodKey == key && r.ObjectiveKey == objective);
    }

    private async Task<PlayerObjectiveProgress> RowAsync(Player p, ObjectivePeriod period, int key, string objective) =>
        (await FindRowAsync(p, period, key, objective)) ?? throw new InvalidOperationException($"No {objective} row");

    private async Task<List<PlayerObjectiveProgress>> DailyRowsAsync(Player p)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerObjectiveProgress.AsNoTracking()
            .Where(r => r.PlayerCycleStateId == p.StateId && r.Period == ObjectivePeriod.Daily).ToListAsync();
    }

    private async Task<FamilyObjectiveProgress?> FindFamilyRowAsync(int familyId, int week, string key)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursFamilyObjectiveProgress.AsNoTracking()
            .SingleOrDefaultAsync(r => r.FamilyId == familyId && r.GameCycleId == _cycle.Id && r.Week == week && r.ObjectiveKey == key);
    }

    private async Task<FamilyObjectiveProgress> FamilyRowAsync(int familyId, int week, string key) =>
        (await FindFamilyRowAsync(familyId, week, key)) ?? throw new InvalidOperationException($"No {key} row");

    private async Task<List<PvpObjectiveCredit>> CreditsAsync(Player attacker)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPvpObjectiveCredits.AsNoTracking().Where(c => c.AttackerStateId == attacker.StateId).OrderBy(c => c.Id).ToListAsync();
    }

    private async Task SeedFamilyRowAsync(int familyId, int week, string key, long progress, long target)
    {
        await using var db = await DbAsync();
        db.AfterHoursFamilyObjectiveProgress.Add(new FamilyObjectiveProgress { FamilyId = familyId, GameCycleId = _cycle.Id, Week = week, ObjectiveKey = key, Progress = progress, Target = target });
        await db.SaveChangesAsync();
    }

    private async Task SeedPlayerWeekAsync(Player p, int week, int points)
    {
        await using var db = await DbAsync();
        db.AfterHoursPlayerObjectiveProgress.Add(new PlayerObjectiveProgress
        {
            GameCycleId = _cycle.Id, PlayerCycleStateId = p.StateId, Period = ObjectivePeriod.Weekly, PeriodKey = week,
            ObjectiveKey = "WA1", Category = ObjectiveCategory.SoloCrime, Progress = 12, Target = 12,
            CompletedAtUtc = _factory.Now, PointsAwarded = points
        });
        await db.SaveChangesAsync();
    }
}
