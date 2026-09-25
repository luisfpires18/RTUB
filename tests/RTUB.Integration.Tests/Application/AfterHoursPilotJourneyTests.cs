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
/// AH-010 final pilot validation: one compact journey through the application services on real SQLite, from an
/// Owner starting a Pilot to Live play continuing after the transition. Each step asserts only what it proves.
/// </summary>
public class AfterHoursPilotJourneyTests : IClassFixture<AfterHoursAdminFactory>
{
    private readonly AfterHoursAdminFactory _factory;
    private static DateTime T0 => AfterHoursPlayFactory.Start;

    public AfterHoursPilotJourneyTests(AfterHoursAdminFactory factory)
    {
        _factory = factory;
        factory.Now = T0;
        factory.Dice.Reset();
    }

    [Fact]
    public async Task PrivatePilot_FromCreationToLive()
    {
        // 1. The Owner starts a 10-day Pilot. Catch-up is tuned to start on day 1 so the short pilot exercises it.
        var owner = await UserAsync("owner", "Owner");
        var year = await FiscalYearAsync(6000);
        var pilot = await Admin().CreatePilotAsync(owner, year.Id, T0, T0.AddDays(10), false);
        await Admin().SetTuningAsync(owner, "CatchUpStartCycleDay", "1");
        (await Admin().GetStatusAsync(owner)).Ready.Should().BeTrue();

        // 2. Two players; their states are created on first access. (Level and cash are seeded for the family step.)
        var alice = await PlayerAsync("alice");
        var bob = await PlayerAsync("bob");
        await UpdateAsync(alice.StateId, s => (s.Level, s.XP, s.WalletCash, s.CreatedAt) = (5, AfterHoursLevels.XpForLevel(5), 3_000, T0.AddDays(-4)));
        await UpdateAsync(bob.StateId, s => s.CreatedAt = T0.AddDays(-4));

        // 3. Crimes on day 0: no catch-up yet.
        (await Actions().CommitCrimeAsync(bob.UserId, "C01", CrimeApproach.Standard, "b-crime-1")).Receipt!.XpDelta.Should().Be(20);

        // 4. Day 1: catch-up applies to crimes below level 12.
        _factory.Now = T0.AddDays(1);
        (await Actions().CommitCrimeAsync(bob.UserId, "C01", CrimeApproach.Standard, "b-crime-2")).Receipt!.XpDelta.Should().Be(30);

        // 5. Cargo: fence the phones, then deliver a contract with seeded cargo.
        (await Actions().SellToFenceAsync(bob.UserId, CargoType.Phone, 2, "b-fence")).Receipt!.WalletDelta.Should().Be(30);
        var contract = (await Service<IBuyerContractService>().GetCurrentContractsAsync(alice.UserId)).First().Contract;
        await AddCargoAsync(alice.StateId, contract.CargoType, contract.Quantity);
        (await Actions().DeliverContractAsync(alice.UserId, contract.Id, "a-contract")).Accepted.Should().BeTrue();

        // 6. Training and equipment.
        (await Actions().TrainSkillAsync(alice.UserId, PlayerSkill.Toughness, "a-train")).Receipt!.SkillRankAfter.Should().Be(5);
        (await Actions().PurchaseGearAsync(alice.UserId, "W1", "a-gear")).Accepted.Should().BeTrue();

        // 7. PvP: Alice beats Bob.
        var fight = await Actions().AttackAsync(alice.UserId, bob.StateId, PvpTactic.Counterattack, RiskStance.Standard, "W1", null, null, "a-pvp");
        fight.Receipt!.PvpBattle!.AttackerWon.Should().BeTrue();

        // 8. Family: create, invite, join, donate.
        var familyId = (await Actions().CreateFamilyAsync(alice.UserId, "Pilot Crew", null, "a-fam")).Receipt!.FamilyId!.Value;
        (await Actions().InviteToFamilyAsync(alice.UserId, bob.StateId, "a-invite")).Accepted.Should().BeTrue();
        var invitation = (await Service<IFamilyService>().GetOverviewAsync(bob.UserId))!.IncomingInvitations.Single();
        (await Actions().AcceptFamilyInvitationAsync(bob.UserId, invitation.InvitationId, "b-join")).Accepted.Should().BeTrue();
        (await Actions().DonateToFamilyAsync(bob.UserId, 50, "b-donate")).Accepted.Should().BeTrue();

        // 9–10. Objectives advanced and standings are readable.
        var objectives = await Service<IObjectiveService>().GetOverviewAsync(bob.UserId);
        objectives!.Weekly.Concat(objectives.Daily).Should().Contain(o => o.Progress > 0);
        var boards = await Service<IObjectiveService>().GetLeaderboardsAsync(alice.UserId);
        boards!.Individual.Should().HaveCount(2);
        boards.Families.Should().ContainSingle(f => f.Id == familyId);

        // A cosmetic title granted during the Pilot.
        await Admin().GrantAwardAsync(owner, alice.UserId, "Pilot Pioneer", "Played the first pilot", year.Id, null);

        // 11–12. The Owner ends the Pilot and starts Live.
        _factory.Now = T0.AddDays(5);
        var liveEnd = RolloverRules.SeptemberStartUtc(year.EndYear);
        var transition = await Admin().TransitionPilotToLiveAsync(owner, pilot.Id, year.Id, _factory.Now, liveEnd);

        // 13. The Pilot archive is non-official, with no champion.
        var archive = (await Service<IYearbookService>().GetArchivesAsync()).Single(a => a.Id == transition.ArchiveId);
        (archive.Official, archive.Kind, archive.Players.Count).Should().Be((false, GameCycleKind.Pilot, 2));
        archive.Players.Should().OnlyContain(p => !p.IsChampion);
        archive.Families.Should().OnlyContain(f => !f.IsChampion);

        // 14. New states start clean.
        var fresh = await Service<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(alice.UserId);
        (fresh!.GameCycleId, fresh.Level, fresh.XP, fresh.WalletCash, fresh.Toughness, fresh.Gear.Count, fresh.Cargo.Count)
            .Should().Be((transition.TargetCycleId, 1, 0L, 400L, 4, 0, 0));

        // 15. The family and its membership survive, with a fresh treasury.
        var family = await Service<IFamilyService>().GetOverviewAsync(bob.UserId);
        (family!.Family!.Id, family.Members.Count, family.Treasury).Should().Be((familyId, 2, 0L));

        // 16. The cosmetic award survives.
        (await Service<IYearbookService>().GetActiveAwardsAsync(alice.UserId)).Should().ContainSingle(a => a.Title == "Pilot Pioneer");

        // 17. Live play continues.
        (await Actions().CommitCrimeAsync(bob.UserId, "C01", CrimeApproach.Standard, "live-crime")).Accepted.Should().BeTrue();
        (await Actions().DonateToFamilyAsync(bob.UserId, 10, "live-donate")).Accepted.Should().BeTrue();
        await using var db = await DbAsync();
        (await db.AfterHoursPlayerCycleStates.CountAsync(s => s.GameCycleId == pilot.Id)).Should().Be(2, "the Pilot's rows stay as history");
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Player(string UserId, int StateId);

    private IAfterHoursAdminService Admin() => Service<IAfterHoursAdminService>();
    private IAfterHoursActionService Actions() => Service<IAfterHoursActionService>();
    private T Service<T>() where T : notnull => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<T>();

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private async Task<string> UserAsync(string label, string? role = null)
    {
        var name = $"ah10j-{label}-{Guid.NewGuid():N}"[..34];
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        if (role is not null) (await users.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
        return user.Id;
    }

    private async Task<Player> PlayerAsync(string label)
    {
        var userId = await UserAsync(label);
        var state = await Service<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(userId);
        return new Player(userId, state!.Id);
    }

    private async Task<FiscalYear> FiscalYearAsync(int start)
    {
        await using var db = await DbAsync();
        var year = FiscalYear.Create(start, start + 1);
        db.FiscalYears.Add(year);
        await db.SaveChangesAsync();
        return year;
    }

    private async Task UpdateAsync(int stateId, Action<PlayerCycleState> change)
    {
        await using var db = await DbAsync();
        change(await db.AfterHoursPlayerCycleStates.SingleAsync(s => s.Id == stateId));
        await db.SaveChangesAsync();
    }

    private async Task AddCargoAsync(int stateId, CargoType cargo, int quantity)
    {
        await using var db = await DbAsync();
        db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = stateId, CargoType = cargo, Quantity = quantity });
        await db.SaveChangesAsync();
    }
}
