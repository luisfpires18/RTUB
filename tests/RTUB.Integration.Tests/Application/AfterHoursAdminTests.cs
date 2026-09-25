using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
/// AH-010 against the real SQLite schema with the frozen clock and scripted dice: database-backed tuning, catch-up
/// XP, the PvP pause, Owner-only admin operations, cosmetic awards and the PvP review view. The class shares one
/// Pilot cycle; every test resets tuning and uses fresh players.
/// </summary>
public class AfterHoursAdminTests : IClassFixture<AfterHoursAdminFactory>
{
    private readonly AfterHoursAdminFactory _factory;
    private static DateTime Start => AfterHoursPlayFactory.Start;

    public AfterHoursAdminTests(AfterHoursAdminFactory factory)
    {
        _factory = factory;
        factory.Now = Start;
        factory.Dice.Reset();
        factory.EnsureActiveCycleAsync().GetAwaiter().GetResult();
        using var db = factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
        db.AfterHoursTuningSettings.ExecuteDelete();
    }

    // ------------------------------------------------------------------ tuning

    [Fact]
    public async Task Tuning_WithNoRows_IsTheDefault_AndEveryKeyIsListed()
    {
        (await Tuning().GetAsync()).Should().Be(AfterHoursTuning.Default);
        var settings = await Tuning().GetSettingsAsync();
        settings.Select(s => s.Setting.Key).Should().Equal(AfterHoursTuning.Settings.Select(s => s.Key));
        settings.Should().OnlyContain(s => s.OverrideValue == null && s.EffectiveValue == s.DefaultValue && s.InvalidReason == null);
    }

    [Fact]
    public async Task Tuning_InvalidOrUnknownUpdates_AreRefused_AndStoreNothing()
    {
        var owner = await OwnerAsync();
        await FluentActions.Invoking(() => Admin().SetTuningAsync(owner, "BankDepositFeePercent", "150")).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Admin().SetTuningAsync(owner, "BankDepositFeePercent", "two")).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Admin().SetTuningAsync(owner, "LevelCap", "30")).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Admin().SetTuningAsync(owner, "CatchUpXpMultiplier", "0.5")).Should().ThrowAsync<ArgumentException>();

        await using var db = await DbAsync();
        (await db.AfterHoursTuningSettings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Tuning_ChangesAreStoredWithWhoAndWhen_AndResetRestoresTheDefault()
    {
        var owner = await OwnerAsync();
        (await Admin().SetTuningAsync(owner, "CatchUpXpMultiplier", " 2.0 ")).Should().Be("2.0");
        (await Admin().SetTuningAsync(owner, "CatchUpXpMultiplier", "1.75")).Should().Be("1.75");

        await using (var db = await DbAsync())
        {
            var row = await db.AfterHoursTuningSettings.SingleAsync();
            (row.Key, row.Value, row.UpdatedByUserId, row.UpdatedAtUtc).Should().Be(("CatchUpXpMultiplier", "1.75", owner, Start));
            (await db.AuditLogs.CountAsync(a => a.EntityType == nameof(AfterHoursTuningSetting))).Should().BeGreaterThanOrEqualTo(2, "tuning changes are audited");
        }
        (await Tuning().GetAsync()).CatchUpXpMultiplier.Should().Be(1.75m);

        await Admin().ResetTuningAsync(owner, "CatchUpXpMultiplier");
        (await Tuning().GetAsync()).Should().Be(AfterHoursTuning.Default);
    }

    [Fact]
    public async Task Tuning_AHandEditedBadRow_FallsBackToTheDefault_AndIsReported()
    {
        var owner = await OwnerAsync();
        await using (var db = await DbAsync())
        {
            db.AfterHoursTuningSettings.Add(new AfterHoursTuningSetting { Key = "CoverJobXpReward", Value = "lots", UpdatedAtUtc = Start });
            await db.SaveChangesAsync();
        }

        (await Tuning().GetAsync()).CoverJobXpReward.Should().Be(12);
        (await Tuning().GetInvalidRowsAsync()).Should().ContainSingle(r => r.Key == "CoverJobXpReward" && r.Value == "lots");
        var status = await Admin().GetStatusAsync(owner);
        status.Warnings.Should().Contain(w => w.Contains("CoverJobXpReward"));
        status.Readiness.Single(c => c.Name == "Tuning overrides valid").Passed.Should().BeFalse();
        status.Ready.Should().BeFalse();

        var p = await PlayerAsync("badrow");
        (await Actions().TakeCoverJobAsync(p.UserId, "c")).Receipt!.XpDelta.Should().Be(12, "play goes on with the default");
    }

    [Fact]
    public async Task Tuning_BankFee_Cover_Family_And_Training_UseTheLiveValues()
    {
        var owner = await OwnerAsync();
        await Admin().SetTuningAsync(owner, "BankDepositFeePercent", "10");
        await Admin().SetTuningAsync(owner, "CoverJobEnergyCost", "5");
        await Admin().SetTuningAsync(owner, "CoverJobCashReward", "99");
        await Admin().SetTuningAsync(owner, "CoverJobXpReward", "7");
        await Admin().SetTuningAsync(owner, "CoverJobHeatReduction", "2");
        await Admin().SetTuningAsync(owner, "FamilyCreationCost", "1000");
        await Admin().SetTuningAsync(owner, "TrainingEnergyCost", "25");

        var p = await PlayerAsync("tuned", level: 5, wallet: 1_300);
        await UpdateAsync(p.StateId, s => s.Heat = 6);
        (await Actions().DepositAsync(p.UserId, 100, "d")).Receipt!.BankDelta.Should().Be(90);
        var cover = (await Actions().TakeCoverJobAsync(p.UserId, "c")).Receipt!;
        (cover.EnergyDelta, cover.WalletDelta, cover.XpDelta, cover.HeatDelta).Should().Be((-5, 99L, 7L, -2));
        var family = (await Actions().CreateFamilyAsync(p.UserId, $"T{Guid.NewGuid():N}"[..10], null, "f")).Receipt!;
        family.WalletDelta.Should().Be(-1_000);
        (await Actions().TrainSkillAsync(p.UserId, PlayerSkill.Toughness, "t")).Receipt!.EnergyDelta.Should().Be(-25);
    }

    [Fact]
    public async Task Tuning_EnergyAndHeatCadence_ApplyFromTheStoredTimestamp_KeepingTheRemainder()
    {
        var owner = await OwnerAsync();
        var p = await PlayerAsync("cadence");
        await Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, "spend"); // energy 230, heat 3 at Start
        await Admin().SetTuningAsync(owner, "EnergyRegenMinutesPerPoint", "3");
        await Admin().SetTuningAsync(owner, "HeatDecayMinutesPerPoint", "5");

        _factory.Now = Start.AddMinutes(13);
        var view = await States().GetOrCreateForActiveCycleAsync(p.UserId);
        (view!.Energy, view.Heat).Should().Be((234, 1), "13 min is 4 energy points at 3 min and 2 heat points at 5 min");

        await Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, "again");
        var stored = await StateAsync(p.StateId);
        (stored.Energy, stored.EnergyUpdatedAtUtc).Should().Be((224, Start.AddMinutes(12)), "the leftover minute carries over");
    }

    [Fact]
    public async Task Tuning_PvpEnergyCooldownAndProtections_UseTheLiveValues()
    {
        var owner = await OwnerAsync();
        await Admin().SetTuningAsync(owner, "PvpAttackEnergyCost", "30");
        await Admin().SetTuningAsync(owner, "PvpGlobalCooldownMinutes", "7");
        await Admin().SetTuningAsync(owner, "PvpDefenderProtectionHours", "2");
        await Admin().SetTuningAsync(owner, "PvpNewPlayerProtectionHours", "1");
        var attacker = await PlayerAsync("pvp-a");
        var defender = await PlayerAsync("pvp-d");

        (await Attack(attacker, defender, "early")).Error.Should().Be("That player is new and still protected.");
        _factory.Now = Start.AddMinutes(61);
        var result = await Attack(attacker, defender, "on");

        result.Accepted.Should().BeTrue(result.Error);
        result.Receipt!.EnergyDelta.Should().Be(-30);
        var battle = result.Receipt.PvpBattle!;
        battle.AttackerWon.Should().BeTrue();
        (battle.AttackerCooldownUntilUtc, battle.DefenderProtectedUntilUtc).Should().Be((_factory.Now.AddMinutes(7), (DateTime?)_factory.Now.AddHours(2)));
    }

    [Fact]
    public async Task Tuning_ConcurrentEdits_KeepOneRow_AndEachActionSeesOneWholeSnapshot()
    {
        var owner = await OwnerAsync();
        var p = await PlayerAsync("race", wallet: 1_000);

        var work = Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
        {
            if (i % 2 == 0) await Admin().SetTuningAsync(owner, "BankDepositFeePercent", i % 4 == 0 ? "10" : "2");
            else await Actions().DepositAsync(p.UserId, 100, $"dep{i}");
        }));
        await Task.WhenAll(work);

        await using var db = await DbAsync();
        (await db.AfterHoursTuningSettings.CountAsync(r => r.Key == "BankDepositFeePercent")).Should().Be(1);
        var receipts = await db.AfterHoursPlayerActionReceipts.Where(r => r.PlayerCycleStateId == p.StateId && r.Action == PlayerActionKind.Deposit).ToListAsync();
        receipts.Should().HaveCount(8).And.OnlyContain(r => r.BankDelta == 90 || r.BankDelta == 98);
    }

    [Fact]
    public async Task Tuning_ChangesFutureActionsOnly_ReplaysKeepTheirStoredResult()
    {
        var owner = await OwnerAsync();
        var p = await PlayerAsync("future");
        await UpdateAsync(p.StateId, s => s.Heat = 60);
        var first = (await Actions().TakeCoverJobAsync(p.UserId, "k")).Receipt!;
        await Admin().SetTuningAsync(owner, "CoverJobXpReward", "50");

        var replay = await Actions().TakeCoverJobAsync(p.UserId, "k");
        var next = await Actions().TakeCoverJobAsync(p.UserId, "k2");

        (first.XpDelta, replay.Replayed, replay.Receipt!.XpDelta, next.Receipt!.XpDelta).Should().Be((12L, true, 12L, 50L));
        await using var db = await DbAsync();
        (await db.AfterHoursPlayerActionReceipts.SingleAsync(r => r.Id == first.Id)).XpDelta.Should().Be(12);
    }

    // ------------------------------------------------------------------ economy multipliers

    [Fact]
    public async Task EconomyMultipliers_ScaleOnlyTheirOwnCashAmount()
    {
        var owner = await OwnerAsync();
        await Admin().SetTuningAsync(owner, "CrimeCashMultiplier", "1.25");
        await Admin().SetTuningAsync(owner, "BaseFenceCashMultiplier", "0.5");
        await Admin().SetTuningAsync(owner, "BuyerContractCashMultiplier", "2");
        await Admin().SetTuningAsync(owner, "TrainingCashCostMultiplier", "1.5");
        await Admin().SetTuningAsync(owner, "EquipmentPriceMultiplier", "0.25");
        var p = await PlayerAsync("econ", level: 3, wallet: 5_000);
        var before = await StateAsync(p.StateId);
        var chance = CrimeRules.Odds(CrimeCatalogue.Find("C01")!, CrimeApproach.Standard, before).Chance;

        var crime = await Crime(p, "crime");
        (crime.WalletDelta, crime.XpDelta, crime.CargoDelta, crime.HeatDelta, crime.SuccessChance, crime.EnergyDelta)
            .Should().Be((44L, 20L, 1, 3, (int?)chance, -10), "35 × 1.25 = 43.75 → 44; XP, cargo, heat, odds and energy unchanged");

        await AddCargoAsync(p.StateId, CargoType.Phone, 1);
        var fence = (await Actions().SellToFenceAsync(p.UserId, CargoType.Phone, 2, "fence")).Receipt!;
        (fence.WalletDelta, fence.CargoDelta).Should().Be((15L, -2), "(2 × 15) × 0.5; cargo moved as asked");

        var contract = (await Contracts().GetCurrentContractsAsync(p.UserId)).First().Contract;
        await AddCargoAsync(p.StateId, contract.CargoType, contract.Quantity);
        var delivery = (await Actions().DeliverContractAsync(p.UserId, contract.Id, "deliver")).Receipt!;
        (delivery.WalletDelta, delivery.XpDelta, delivery.CargoDelta).Should().Be((contract.CashReward * 2, contract.XpReward, -contract.Quantity));
        await using (var db = await DbAsync())
            (await db.AfterHoursBuyerContracts.SingleAsync(c => c.Id == contract.Id)).CashReward.Should().Be(contract.CashReward, "the contract row is not rewritten");

        var train = (await Actions().TrainSkillAsync(p.UserId, PlayerSkill.Toughness, "train")).Receipt!;
        (train.WalletDelta, train.EnergyDelta, train.SkillRankAfter).Should().Be((-180L, -20, (int?)5), "(120 + 40·0) × 1.5; energy and rank rules unchanged");

        var gear = (await Actions().PurchaseGearAsync(p.UserId, "W1", "gear")).Receipt!;
        gear.WalletDelta.Should().Be(-100, "400 × 0.25");
        await using (var db = await DbAsync())
            (await db.AfterHoursPlayerGear.SingleAsync(g => g.PlayerCycleStateId == p.StateId)).Tier.Should().Be(1);
    }

    [Fact]
    public async Task FenceMultiplier_RoundsTheSaleTotalOnce_AndReplaysTheStoredPayout()
    {
        var owner = await OwnerAsync();
        var p = await PlayerAsync("fence-total");
        await AddCargoAsync(p.StateId, CargoType.Phone, 5);
        await Admin().SetTuningAsync(owner, "BaseFenceCashMultiplier", "1.1");

        var three = (await Actions().SellToFenceAsync(p.UserId, CargoType.Phone, 3, "f3")).Receipt!;
        var one = (await Actions().SellToFenceAsync(p.UserId, CargoType.Phone, 1, "f1")).Receipt!;
        (three.WalletDelta, three.CargoDelta, one.WalletDelta).Should().Be((50L, -3, 17L), "45 × 1.1 = 49.5 → 50 (not 17 × 3); 16.5 → 17");

        await Admin().ResetTuningAsync(owner, "BaseFenceCashMultiplier");
        var replay = await Actions().SellToFenceAsync(p.UserId, CargoType.Phone, 3, "f3");
        var plain = (await Actions().SellToFenceAsync(p.UserId, CargoType.Phone, 1, "f-plain")).Receipt!;
        (replay.Replayed, replay.Receipt!.WalletDelta, plain.WalletDelta).Should().Be((true, 50L, 15L));

        var state = await StateAsync(p.StateId);
        state.WalletCash.Should().Be(400L + 50 + 17 + 15, "the wallet matches the receipts; the replay paid nothing");
        await using var db = await DbAsync();
        (await db.AfterHoursPlayerCargo.SingleAsync(c => c.PlayerCycleStateId == p.StateId && c.CargoType == CargoType.Phone)).Quantity.Should().Be(0);
        (await db.AfterHoursPlayerObjectiveProgress.SingleAsync(r => r.PlayerCycleStateId == p.StateId && r.ObjectiveKey == "WA3")).Progress
            .Should().Be(5, "fence objectives count units, which the multiplier never changes");
    }

    [Fact]
    public async Task EconomyMultipliers_ReplaysStayExact_AndTheCrimeCashObjectiveTracksWhatWasPaid()
    {
        var owner = await OwnerAsync();
        var p = await PlayerAsync("econ-replay", level: 3, wallet: 2_000);
        await Admin().SetTuningAsync(owner, "CrimeCashMultiplier", "2");
        await Admin().SetTuningAsync(owner, "EquipmentPriceMultiplier", "1.5");
        var crime = await Crime(p, "c1");
        var gear = (await Actions().PurchaseGearAsync(p.UserId, "W1", "g1")).Receipt!;
        var wallet = (await StateAsync(p.StateId)).WalletCash;

        await Admin().ResetTuningAsync(owner, "CrimeCashMultiplier");
        await Admin().ResetTuningAsync(owner, "EquipmentPriceMultiplier");
        var crimeReplay = await Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, "c1");
        var gearReplay = await Actions().PurchaseGearAsync(p.UserId, "W1", "g1");
        var later = await Crime(p, "c2");

        (crime.WalletDelta, gear.WalletDelta).Should().Be((70L, -600L));
        (crimeReplay.Replayed, crimeReplay.Receipt!.WalletDelta, gearReplay.Replayed, gearReplay.Receipt!.WalletDelta).Should().Be((true, 70L, true, -600L));
        later.WalletDelta.Should().Be(35);
        (await StateAsync(p.StateId)).WalletCash.Should().Be(wallet + 35, "replays pay and charge nothing again");
        await using var db = await DbAsync();
        (await db.AfterHoursPlayerObjectiveProgress.SingleAsync(r => r.PlayerCycleStateId == p.StateId && r.ObjectiveKey == "WA2")).Progress
            .Should().Be(70 + 35, "weekly crime cash counts the cash actually paid");
    }

    [Fact]
    public async Task EconomyMultipliers_EachActionUsesOneSnapshot_ForItsCashAndItsObjective()
    {
        var owner = await OwnerAsync();
        var p = await PlayerAsync("econ-race");

        await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(async () =>
        {
            if (i % 2 == 0) await Admin().SetTuningAsync(owner, "CrimeCashMultiplier", i % 4 == 0 ? "2" : "1");
            else (await Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, $"r{i}")).Accepted.Should().BeTrue();
        })));

        await using var db = await DbAsync();
        var receipts = await db.AfterHoursPlayerActionReceipts.Where(r => r.PlayerCycleStateId == p.StateId).ToListAsync();
        receipts.Should().HaveCount(12).And.OnlyContain(r => r.WalletDelta == 35 || r.WalletDelta == 70);
        var paid = receipts.Sum(r => r.WalletDelta);
        (await db.AfterHoursPlayerObjectiveProgress.SingleAsync(r => r.PlayerCycleStateId == p.StateId && r.ObjectiveKey == "WA2")).Progress.Should().Be(paid);
        (await StateAsync(p.StateId)).WalletCash.Should().Be(400 + paid);
    }

    [Fact]
    public async Task EconomyMultipliers_AMalformedRowFallsBackToTheCatalogue_AndIsReported()
    {
        var owner = await OwnerAsync();
        await using (var db = await DbAsync())
        {
            db.AfterHoursTuningSettings.Add(new AfterHoursTuningSetting { Key = "CrimeCashMultiplier", Value = "9.5", UpdatedAtUtc = Start });
            await db.SaveChangesAsync();
        }

        (await Crime(await PlayerAsync("econ-bad"), "c")).WalletDelta.Should().Be(35);
        var status = await Admin().GetStatusAsync(owner);
        status.InvalidTuning.Should().ContainSingle(r => r.Key == "CrimeCashMultiplier");
        status.Ready.Should().BeFalse();
    }

    // ------------------------------------------------------------------ catch-up

    [Fact]
    public async Task CatchUp_StartsExactlyAtDay42_MultipliesCrimeAndCoverXpOnly()
    {
        var cycleStart = await CycleStartAsync();
        _factory.Now = cycleStart.AddDays(42).AddTicks(-1);
        var p = await PlayerAsync("cu");
        (await Crime(p, "before")).XpDelta.Should().Be(20, "one tick before day 42 ends: no catch-up");

        _factory.Now = cycleStart.AddDays(42);
        var success = await Crime(p, "on");
        (success.XpDelta, success.WalletDelta, success.CargoType, success.CargoDelta, success.HeatDelta).Should().Be((30L, 35L, (CargoType?)CargoType.Phone, 1, 3));

        _factory.Dice.Script(99, 99); // fail, not jailed
        var failure = await Crime(p, "fail");
        (failure.Succeeded, failure.XpDelta).Should().Be((false, 8L), "25% of 20 is 5, then ×1.5 = 7.5 rounds half up to 8");

        (await Actions().TakeCoverJobAsync(p.UserId, "cover")).Receipt!.XpDelta.Should().Be(18);

        var replay = await Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, "on");
        (replay.Replayed, replay.Receipt!.XpDelta).Should().Be((true, 30L));
        (await StateAsync(p.StateId)).XP.Should().Be(20 + 30 + 8 + 18 + await DailyXpAsync(p.StateId), "a replay awards nothing twice");
    }

    [Fact]
    public async Task CatchUp_IsForLevelsBelow12_AndTheActionThatReaches12StillGetsIt()
    {
        _factory.Now = (await CycleStartAsync()).AddDays(50);
        var eleven = await PlayerAsync("cu11", level: 11);
        var twelve = await PlayerAsync("cu12", level: 12);
        var edge = await PlayerAsync("cuedge", level: 11);
        await UpdateAsync(edge.StateId, s => s.XP = AfterHoursLevels.XpForLevel(12) - 10);

        (await Crime(eleven, "a")).XpDelta.Should().Be(30);
        (await Crime(twelve, "b")).XpDelta.Should().Be(20);
        var crossing = await Crime(edge, "c");
        (crossing.XpDelta, crossing.LevelBefore, crossing.LevelAfter).Should().Be((30L, 11, 12));
        (await Crime(edge, "d")).XpDelta.Should().Be(20, "level 12 now: catch-up is over");
    }

    [Fact]
    public async Task CatchUp_LeavesContractXp_DailyObjectiveXp_AndWeeklyProgressUnmultiplied()
    {
        _factory.Now = (await CycleStartAsync()).AddDays(42);
        var p = await PlayerAsync("cuobj");
        for (var i = 0; i < 6; i++) (await Crime(p, $"x{i}")).XpDelta.Should().Be(30);

        await using (var db = await DbAsync())
        {
            var daily = await db.AfterHoursPlayerObjectiveProgress.Where(r => r.PlayerCycleStateId == p.StateId && r.Period == ObjectivePeriod.Daily && r.CompletedAtUtc != null).ToListAsync();
            daily.Should().NotBeEmpty().And.OnlyContain(r => r.XpAwarded == ObjectiveCatalogue.Find(r.ObjectiveKey)!.XpReward);
            var cashRow = await db.AfterHoursPlayerObjectiveProgress.SingleAsync(r => r.PlayerCycleStateId == p.StateId && r.Period == ObjectivePeriod.Weekly && r.ObjectiveKey == "WA2");
            cashRow.Progress.Should().Be(6 * 35, "weekly crime-cash progress counts cash, which catch-up never touches");
        }

        var contract = (await Contracts().GetCurrentContractsAsync(p.UserId)).First().Contract;
        await using (var db = await DbAsync())
        {
            db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = p.StateId, CargoType = contract.CargoType, Quantity = contract.Quantity });
            await db.SaveChangesAsync();
        }
        (await Actions().DeliverContractAsync(p.UserId, contract.Id, "deliver")).Receipt!.XpDelta.Should().Be(contract.XpReward);
    }

    [Fact]
    public async Task CatchUp_TuningChangesFutureActions_NotAcceptedReceipts()
    {
        var owner = await OwnerAsync();
        _factory.Now = (await CycleStartAsync()).AddDays(45);
        var p = await PlayerAsync("cutune");
        var before = await Crime(p, "one");
        await Admin().SetTuningAsync(owner, "CatchUpXpMultiplier", "2");
        var after = await Crime(p, "two");

        (before.XpDelta, after.XpDelta).Should().Be((30L, 40L));
        await using var db = await DbAsync();
        (await db.AfterHoursPlayerActionReceipts.SingleAsync(r => r.Id == before.Id)).XpDelta.Should().Be(30);
    }

    // ------------------------------------------------------------------ PvP pause

    [Fact]
    public async Task PvpPause_RefusesNewAttacksBeforeAnythingChanges_AndResumeRestoresThem()
    {
        var owner = await OwnerAsync();
        var a = await PlayerAsync("pa", backdate: true);
        var b = await PlayerAsync("pb", backdate: true);
        var c = await PlayerAsync("pc", backdate: true);
        var first = await Attack(a, b, "first");
        first.Accepted.Should().BeTrue(first.Error);
        var battleId = first.Receipt!.PvpBattle!.Id;

        await Admin().SetPvpEnabledAsync(owner, false);
        _factory.Now = Start.AddMinutes(10); // past the cooldown, so only the pause can refuse
        var before = (await StateAsync(a.StateId), await StateAsync(c.StateId), _factory.Dice.Calls, await CountAsync());

        var refused = await Attack(a, c, "paused");

        refused.Error.Should().Be(PvpRules.PausedMessage);
        var afterA = await StateAsync(a.StateId);
        var afterC = await StateAsync(c.StateId);
        (afterA.Energy, afterA.EnergyUpdatedAtUtc, afterA.PvpCooldownUntilUtc, afterA.PvpRecoveryUntilUtc, afterA.PvpInitiatedAtUtc)
            .Should().Be((before.Item1.Energy, before.Item1.EnergyUpdatedAtUtc, before.Item1.PvpCooldownUntilUtc, before.Item1.PvpRecoveryUntilUtc, before.Item1.PvpInitiatedAtUtc));
        (afterC.PvpProtectedUntilUtc, afterC.WalletCash).Should().Be((before.Item2.PvpProtectedUntilUtc, before.Item2.WalletCash));
        _factory.Dice.Calls.Should().Be(before.Item3, "no dice are rolled");
        (await CountAsync()).Should().Be(before.Item4, "no battle and no receipt");

        (await Pvp().GetBattleAsync(a.UserId, battleId)).Should().NotBeNull("reports stay readable");
        (await Pvp().GetRecentBattlesAsync(b.UserId)).Should().ContainSingle();
        (await Actions().SaveDefenceAsync(a.UserId, PvpTactic.Setup, null, null, null, "def")).Accepted.Should().BeTrue("defences stay editable");
        (await Actions().CommitCrimeAsync(a.UserId, "C01", CrimeApproach.Standard, "crime")).Accepted.Should().BeTrue("only PvP is paused");

        await Admin().SetPvpEnabledAsync(owner, true);
        (await Attack(a, c, "resumed")).Accepted.Should().BeTrue();
    }

    // ------------------------------------------------------------------ security

    [Theory]
    [InlineData("Member")]
    [InlineData("Admin")]
    public async Task AdminOperations_RefuseAnyoneButTheOwner(string role)
    {
        var user = await UserAsync($"not-owner-{role}", role);
        var p = await PlayerAsync("victim");
        var before = await CountAsync();
        var admin = Admin();

        var calls = new Func<Task>[]
        {
            () => admin.GetStatusAsync(user),
            () => admin.SetTuningAsync(user, "BankDepositFeePercent", "50"),
            () => admin.ResetTuningAsync(user, "BankDepositFeePercent"),
            () => admin.SetPvpEnabledAsync(user, false),
            () => admin.CreatePilotAsync(user, 1, Start, Start.AddDays(10), false),
            () => admin.TransitionPilotToLiveAsync(user, 1, 1, Start, Start.AddDays(100)),
            () => admin.RolloverLiveAsync(user, 1),
            () => admin.GetRecentPvpAsync(user),
            () => admin.GetAwardsAsync(user),
            () => admin.GetAwardRecipientsAsync(user),
            () => admin.GrantAwardAsync(user, p.UserId, "Sneaky", null, null, null),
            () => admin.RevokeAwardAsync(user, 1),
            () => admin.GetStatusAsync(""),
        };
        foreach (var call in calls)
            await FluentActions.Invoking(call).Should().ThrowAsync<UnauthorizedAccessException>();

        (await CountAsync()).Should().Be(before);
        await using var db = await DbAsync();
        (await db.AfterHoursTuningSettings.CountAsync(), await db.AfterHoursCosmeticAwards.CountAsync(a => a.UserId == p.UserId)).Should().Be((0, 0));
    }

    [Fact]
    public async Task Status_ContainsNoSecrets()
    {
        var json = JsonSerializer.Serialize(await Admin().GetStatusAsync(await OwnerAsync()));
        json.Should().NotContainAny("Password", "password", "Secret", "ConnectionString", "AccessKey", "Data Source", "VAPID");
    }

    // ------------------------------------------------------------------ cosmetic awards

    [Fact]
    public async Task Awards_GrantListRevoke_Validated_WithoutGameplayEffect_AndSurviveAccountDeletion()
    {
        var owner = await OwnerAsync();
        var p = await PlayerAsync("awardee");
        var stateBefore = await StateAsync(p.StateId);

        await FluentActions.Invoking(() => Admin().GrantAwardAsync(owner, p.UserId, " x ", null, null, null)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Admin().GrantAwardAsync(owner, p.UserId, new string('t', 41), null, null, null)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Admin().GrantAwardAsync(owner, p.UserId, "Fine", new string('d', 161), null, null)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Admin().GrantAwardAsync(owner, "nobody", "Fine", null, null, null)).Should().ThrowAsync<InvalidOperationException>();

        var award = await Admin().GrantAwardAsync(owner, p.UserId, "  Night Owl  ", " First pilot ", null, null);
        var second = await Admin().GrantAwardAsync(owner, p.UserId, "Fixer", null, null, null);
        (award.Title, award.Description, award.RecipientName, award.GrantedByUserId).Should().Be(("Night Owl", "First pilot", p.Name, owner));
        (await Yearbook().GetActiveAwardsAsync(p.UserId)).Select(a => a.Title).Should().Equal("Night Owl", "Fixer");
        (await Admin().GetAwardsAsync(owner)).Should().Contain(v => v.Award.Id == award.Id);

        await Admin().RevokeAwardAsync(owner, second.Id);
        await Admin().RevokeAwardAsync(owner, second.Id); // idempotent
        (await Yearbook().GetActiveAwardsAsync(p.UserId)).Select(a => a.Title).Should().Equal("Night Owl");
        (await Admin().GetAwardsAsync(owner)).Single(v => v.Award.Id == second.Id).Award.RevokedByUserId.Should().Be(owner);

        var stateAfter = await StateAsync(p.StateId);
        (stateAfter.Level, stateAfter.XP, stateAfter.WalletCash, stateAfter.Toughness, stateAfter.Energy).Should().Be(
            (stateBefore.Level, stateBefore.XP, stateBefore.WalletCash, stateBefore.Toughness, stateBefore.Energy), "a title changes no stat");

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.DeleteAsync((await users.FindByIdAsync(p.UserId))!)).Succeeded.Should().BeTrue();
        }
        await using var db = await DbAsync();
        (await db.AfterHoursCosmeticAwards.CountAsync(a => a.UserId == p.UserId)).Should().Be(2, "cosmetic history outlives the account, like the yearbook");
    }

    // ------------------------------------------------------------------ PvP review

    [Fact]
    public async Task SuspiciousPvp_FlagsRepeatedPairs_LowLoot_AndAlternation_WithoutChangingAnything()
    {
        var owner = await OwnerAsync();
        await Admin().SetTuningAsync(owner, "PvpSameTargetCooldownHours", "1");
        await Admin().SetTuningAsync(owner, "PvpDefenderProtectionHours", "1");
        var a = await PlayerAsync("sus-a", backdate: true);
        var b = await PlayerAsync("sus-b", backdate: true);
        var strong = await PlayerAsync("sus-strong", backdate: true);
        var weak = await PlayerAsync("sus-weak", backdate: true);
        await UpdateAsync(strong.StateId, s => (s.Toughness, s.Stealth, s.Smarts, s.Charisma) = (10, 10, 10, 10));

        foreach (var (attacker, defender, key) in new[] { (a, b, "1"), (b, a, "2"), (a, b, "3"), (a, b, "4") })
        {
            (await Attack(attacker, defender, key)).Accepted.Should().BeTrue();
            _factory.Now = _factory.Now.AddHours(2);
        }
        (await Attack(strong, weak, "low")).Receipt!.PvpBattle!.LootMultiplier.Should().BeLessThanOrEqualTo(0.25m);
        var battles = await CountAsync();

        var rows = await Admin().GetRecentPvpAsync(owner);

        var aOnB = rows.Where(r => r.AttackerName == a.Name && r.DefenderName == b.Name).ToList();
        aOnB.Should().HaveCount(3).And.OnlyContain(r => r.PairCount == 3 && r.PairWins == 3);
        aOnB.Should().OnlyContain(r => r.Flags.Any(f => f.StartsWith("Same pair")) && r.Flags.Contains("Alternating attacks"));
        rows.Single(r => r.AttackerName == strong.Name).Flags.Should().Contain("Loot multiplier ≤ 0.25");
        rows.Take(rows.Count(r => r.Flags.Count > 0)).Should().OnlyContain(r => r.Flags.Count > 0, "flagged rows come first");
        (await CountAsync()).Should().Be(battles, "review is read-only");
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Player(string UserId, int StateId, string Name);

    private IAfterHoursAdminService Admin() => Service<IAfterHoursAdminService>();
    private IAfterHoursTuningService Tuning() => Service<IAfterHoursTuningService>();
    private IAfterHoursActionService Actions() => Service<IAfterHoursActionService>();
    private IPlayerCycleStateService States() => Service<IPlayerCycleStateService>();
    private IPvpService Pvp() => Service<IPvpService>();
    private IYearbookService Yearbook() => Service<IYearbookService>();
    private IBuyerContractService Contracts() => Service<IBuyerContractService>();
    private T Service<T>() where T : notnull => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<T>();

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private async Task<string> UserAsync(string label, string? role = null)
    {
        var name = $"ah10-{label}-{Guid.NewGuid():N}"[..34];
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        if (role is not null) (await users.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
        return user.Id;
    }

    private Task<string> OwnerAsync() => UserAsync("owner", "Owner");

    private async Task<Player> PlayerAsync(string label, int level = 1, long wallet = 400, bool backdate = false)
    {
        var userId = await UserAsync(label);
        var state = await States().GetOrCreateForActiveCycleAsync(userId);
        await UpdateAsync(state!.Id, s =>
        {
            (s.Level, s.XP, s.WalletCash) = (level, AfterHoursLevels.XpForLevel(level), wallet);
            if (backdate) s.CreatedAt = _factory.Now.AddDays(-4);
        });
        await using var db = await DbAsync();
        return new Player(userId, state.Id, (await db.Users.SingleAsync(u => u.Id == userId)).Nickname!);
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
        var row = await db.AfterHoursPlayerCargo.SingleOrDefaultAsync(c => c.PlayerCycleStateId == stateId && c.CargoType == cargo);
        if (row is null) db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = stateId, CargoType = cargo, Quantity = quantity });
        else row.Quantity += quantity;
        await db.SaveChangesAsync();
    }

    private async Task<PlayerCycleState> StateAsync(int stateId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCycleStates.AsNoTracking().SingleAsync(s => s.Id == stateId);
    }

    private async Task<DateTime> CycleStartAsync()
    {
        await using var db = await DbAsync();
        return (await db.AfterHoursGameCycles.SingleAsync(c => c.Status == GameCycleStatus.Active)).StartUtc;
    }

    private async Task<long> DailyXpAsync(int stateId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerObjectiveProgress.Where(r => r.PlayerCycleStateId == stateId).SumAsync(r => r.XpAwarded);
    }

    private async Task<(int Battles, int Receipts)> CountAsync()
    {
        await using var db = await DbAsync();
        return (await db.AfterHoursPvpBattles.CountAsync(), await db.AfterHoursPlayerActionReceipts.CountAsync());
    }

    private async Task<PlayerActionReceipt> Crime(Player p, string key)
    {
        var result = await Actions().CommitCrimeAsync(p.UserId, "C01", CrimeApproach.Standard, key);
        result.Receipt.Should().NotBeNull(result.Error);
        return result.Receipt!;
    }

    private Task<AfterHoursActionResult> Attack(Player attacker, Player defender, string key) =>
        Actions().AttackAsync(attacker.UserId, defender.StateId, PvpTactic.Negotiation, RiskStance.Standard, null, null, null, key);
}

/// <summary>The play host (settable clock, scripted dice) with After Hours enabled, so readiness sees a live feature.</summary>
public class AfterHoursAdminFactory : AfterHoursRolloverFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["AfterHours:Enabled"] = "true" }));
    }
}
