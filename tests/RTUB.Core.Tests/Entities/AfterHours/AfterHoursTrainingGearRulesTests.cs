using FluentAssertions;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

/// <summary>AH-005 rules as pure functions: training points (Lisbon days), training, gear.</summary>
public class AfterHoursTrainingGearRulesTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PlayerCycleState NewState(int level = 1, long wallet = 400)
    {
        var state = PlayerCycleState.CreateInitial(1, "u", T0);
        state.XP = AfterHoursLevels.XpForLevel(level);
        state.Level = level;
        state.WalletCash = wallet;
        return state;
    }

    // ------------------------------------------------------------------ training points

    [Fact]
    public void NewState_StartsWithTodaysSinglePoint()
    {
        var s = NewState();

        (s.TrainingPoints, s.TrainingPointsDay).Should().Be((1, new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void PreAh005State_FirstReconcile_GrantsOnePoint_NoBacklog()
    {
        var s = NewState();
        s.TrainingPoints = 0;
        s.TrainingPointsDay = null;

        s.ReconcileTraining(T0.AddDays(30));

        (s.TrainingPoints, s.TrainingPointsDay).Should().Be((1, new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Points_OnePerLisbonDay_NeverTwiceInADay_CappedAtThree()
    {
        var s = NewState();
        s.ReconcileTraining(T0);

        s.ReconcileTraining(T0.AddHours(10)); // 22:00 UTC = 23:00 Lisbon (summer), same day
        s.TrainingPoints.Should().Be(1);

        s.ReconcileTraining(T0.AddDays(1));
        s.TrainingPoints.Should().Be(2);
        s.ReconcileTraining(T0.AddDays(1).AddHours(3));
        s.TrainingPoints.Should().Be(2);

        s.ReconcileTraining(T0.AddDays(9));
        s.TrainingPoints.Should().Be(3, "days away accumulate, but only up to three");
        s.TrainingPointsDay.Should().Be(new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void DayBoundary_IsLisbonMidnight_InSummerTime()
    {
        var s = NewState();
        s.TrainingPoints = 0;
        s.TrainingPointsDay = new DateOnly(2026, 9, 30);

        s.ReconcileTraining(new DateTime(2026, 9, 30, 22, 59, 59, DateTimeKind.Utc)); // 23:59:59 WEST
        s.TrainingPoints.Should().Be(0);

        s.ReconcileTraining(new DateTime(2026, 9, 30, 23, 0, 0, DateTimeKind.Utc)); // 00:00 WEST, 1 Oct
        s.TrainingPoints.Should().Be(1);
        LisbonCalendar.NextMidnightUtc(new DateOnly(2026, 9, 30)).Should().Be(new DateTime(2026, 9, 30, 23, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DayBoundary_IsLisbonMidnight_InWinterTime()
    {
        var s = NewState();
        s.TrainingPoints = 0;
        s.TrainingPointsDay = new DateOnly(2027, 1, 5);

        s.ReconcileTraining(new DateTime(2027, 1, 5, 23, 59, 59, DateTimeKind.Utc)); // same in WET
        s.TrainingPoints.Should().Be(0);

        s.ReconcileTraining(new DateTime(2027, 1, 6, 0, 0, 0, DateTimeKind.Utc));
        s.TrainingPoints.Should().Be(1);
    }

    // ------------------------------------------------------------------ training

    [Theory]
    [InlineData(1, 4)]
    [InlineData(4, 6)]
    [InlineData(12, 10)]
    [InlineData(16, 12)]
    [InlineData(20, 12)]
    public void SkillCap_ByLevel(int level, int cap) => TrainingRules.SkillCap(level).Should().Be(cap);

    [Theory]
    [InlineData(4, 120)]
    [InlineData(5, 160)]
    [InlineData(11, 400)]
    public void TrainingCost(int rank, long cost) => TrainingRules.CashCost(rank).Should().Be(cost);

    [Fact]
    public void Train_SpendsPointEnergyAndWalletCash_AndRaisesTheRank()
    {
        var s = NewState(level: 12, wallet: 1_000);
        s.BankCash = 5_000;
        s.Stealth = 6;

        var receipt = AfterHoursActions.TrainSkill(s, PlayerSkill.Stealth, T0).Receipt!;

        (s.Stealth, s.TrainingPoints, s.Energy, s.WalletCash, s.BankCash).Should().Be((7, 0, 220, 800L, 5_000L));
        (receipt.Action, receipt.Skill, receipt.SkillRankAfter, receipt.EnergyDelta, receipt.WalletDelta, receipt.Request)
            .Should().Be((PlayerActionKind.TrainSkill, PlayerSkill.Stealth, 7, -20, -200L, "train:Stealth"));
    }

    [Fact]
    public void Train_Refusals_ChangeNothing()
    {
        var atCap = NewState(level: 1);
        AfterHoursActions.TrainSkill(atCap, PlayerSkill.Smarts, T0).Error.Should().StartWith("At your current cap");

        var noPoints = NewState(level: 4);
        noPoints.TrainingPoints = 0;
        AfterHoursActions.TrainSkill(noPoints, PlayerSkill.Smarts, T0).Error.Should().StartWith("No training points");

        var tired = NewState(level: 4);
        tired.Energy = 19;
        AfterHoursActions.TrainSkill(tired, PlayerSkill.Smarts, T0).Error.Should().Be("Not enough energy.");

        var broke = NewState(level: 4, wallet: 119);
        broke.BankCash = 10_000;
        AfterHoursActions.TrainSkill(broke, PlayerSkill.Smarts, T0).Error.Should().Be("Not enough cash in your wallet.");
        (broke.Smarts, broke.WalletCash, broke.BankCash).Should().Be((4, 119L, 10_000L));
    }

    // ------------------------------------------------------------------ gear catalogue

    [Fact]
    public void GearCatalogue_TiersUnlocksAndAh005Prices()
    {
        GearCatalogue.All.Select(i => (i.Key, i.Name, i.Slot, i.Tier, i.UnlockLevel, i.Price)).Should().Equal(
            ("W1", "Brass Knuckles", GearSlot.Weapon, 1, 3, 400L),
            ("O1", "Hooded Jacket", GearSlot.Outfit, 1, 3, 500L),
            ("V1", "Lockpick Kit", GearSlot.VehicleTool, 1, 3, 600L),
            ("W2", "Switchblade", GearSlot.Weapon, 2, 10, 2_000L),
            ("O2", "Armored Jacket", GearSlot.Outfit, 2, 10, 2_750L),
            ("V2", "Modified Scooter", GearSlot.VehicleTool, 2, 10, 3_500L),
            ("W3", "Compact Pistol", GearSlot.Weapon, 3, 16, 9_000L),
            ("O3", "Tailored Protection", GearSlot.Outfit, 3, 16, 12_000L),
            ("V3", "Getaway Car", GearSlot.VehicleTool, 3, 16, 15_000L),
            ("W4", "Collector Weapon", GearSlot.Weapon, 4, 20, 40_000L),
            ("O4", "Reinforced Suit", GearSlot.Outfit, 4, 20, 50_000L),
            ("V4", "Specialist Rig", GearSlot.VehicleTool, 4, 20, 60_000L));
    }

    // ------------------------------------------------------------------ gear actions

    [Fact]
    public void Purchase_IsLevelLocked_WalletOnly_AndOnce()
    {
        AfterHoursActions.PurchaseGear(NewState(level: 2, wallet: 10_000), "W1").Error.Should().Be("Requires level 3.");

        var broke = NewState(level: 3, wallet: 399);
        broke.BankCash = 10_000;
        AfterHoursActions.PurchaseGear(broke, "W1").Error.Should().Be("Not enough cash in your wallet.");

        var s = NewState(level: 3, wallet: 1_000);
        var receipt = AfterHoursActions.PurchaseGear(s, "W1").Receipt!;
        (s.WalletCash, s.EquippedWeaponKey, receipt.GearKey, receipt.WalletDelta).Should().Be((600L, "W1", "W1", -400L));
        s.Gear.Should().ContainSingle().Which.Should().BeEquivalentTo(new { ItemKey = "W1", Slot = GearSlot.Weapon, Tier = 1 });

        AfterHoursActions.PurchaseGear(s, "W1").Error.Should().Be("You already own this.");
        s.WalletCash.Should().Be(600);
    }

    [Fact]
    public void Equip_OnePerSlot_ReplacesTheCurrentItem_AndBestOwnedIgnoresEquipping()
    {
        var s = NewState(level: 10, wallet: 10_000);
        AfterHoursActions.PurchaseGear(s, "W1");
        AfterHoursActions.PurchaseGear(s, "W2");
        AfterHoursActions.PurchaseGear(s, "O1");
        s.EquippedWeaponKey.Should().Be("W1", "a purchase only auto-equips into an empty slot");

        AfterHoursActions.EquipGear(s, "W2").Error.Should().BeNull();
        (s.EquippedWeaponKey, s.EquippedOutfitKey).Should().Be(("W2", "O1"));
        AfterHoursActions.EquipGear(s, "W2").Error.Should().Be("Already equipped.");
        AfterHoursActions.EquipGear(s, "V1").Error.Should().Be("You don't own this.");

        AfterHoursActions.EquipGear(s, "W1").Error.Should().BeNull();
        AfterHoursActions.UnequipGear(s, "O1").Error.Should().BeNull();
        s.EquippedOutfitKey.Should().BeNull();
        AfterHoursActions.UnequipGear(s, "O1").Error.Should().Be("That item is not equipped.");

        GearCatalogue.BestOwnedTiers(s.Gear).Should().Be(new GearTiers(2, 1, 0),
            "PvP will use the best owned tier, whatever is equipped");
    }
}
