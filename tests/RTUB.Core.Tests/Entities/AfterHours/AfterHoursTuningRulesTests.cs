using FluentAssertions;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

/// <summary>AH-010 tuning snapshot, validation and catch-up rules that need no database.</summary>
public class AfterHoursTuningRulesTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Defaults_AreTheCanonicalAndEarlierUnitValues()
    {
        var t = AfterHoursTuning.Default;
        (t.BankDepositFeePercent, t.FamilyCreationCost, t.EnergyRegenMinutesPerPoint, t.HeatDecayMinutesPerPoint, t.CrimeHeatBlockThreshold)
            .Should().Be((2, 1_500L, 6, 10, 80));
        (t.CoverJobEnergyCost, t.CoverJobHeatReduction, t.CoverJobHighHeatThreshold, t.CoverJobCashReward, t.CoverJobXpReward)
            .Should().Be((10, 15, 50, 20L, 12L));
        (t.PvpEnabled, t.PvpAttackEnergyCost, t.PvpGlobalCooldownMinutes, t.PvpDefenderProtectionHours, t.PvpSameTargetCooldownHours, t.PvpNewPlayerProtectionHours)
            .Should().Be((true, 20, 5, 6, 24, 72));
        (t.PvpRecoveryCautiousMinutes, t.PvpRecoveryStandardMinutes, t.PvpRecoveryRecklessMinutes, t.TrainingEnergyCost, t.TrainingPointStorageCap)
            .Should().Be((10, 15, 30, 20, 3));
        (t.CatchUpStartCycleDay, t.CatchUpLevelCeiling, t.CatchUpXpMultiplier).Should().Be((42, 12, 1.5m));
        (t.CrimeCashMultiplier, t.BaseFenceCashMultiplier, t.BuyerContractCashMultiplier, t.TrainingCashCostMultiplier, t.EquipmentPriceMultiplier)
            .Should().Be((1m, 1m, 1m, 1m, 1m));
        AfterHoursTuning.Settings.Should().HaveCount(29).And.OnlyHaveUniqueItems(s => s.Key);
    }

    [Fact]
    public void EveryKey_AcceptsItsOwnDefault_AndRejectsGarbage()
    {
        foreach (var setting in AfterHoursTuning.Settings)
        {
            setting.Normalize(setting.Read(AfterHoursTuning.Default), out var normalized).Should().BeNull(setting.Key);
            normalized.Should().Be(setting.Read(AfterHoursTuning.Default));
            setting.Normalize("not a value", out _).Should().NotBeNull(setting.Key);
            setting.Normalize("", out _).Should().NotBeNull(setting.Key);
        }
    }

    [Theory]
    [InlineData("BankDepositFeePercent", "-1")]
    [InlineData("BankDepositFeePercent", "101")]
    [InlineData("EnergyRegenMinutesPerPoint", "0")]
    [InlineData("PvpGlobalCooldownMinutes", "0")]
    [InlineData("CoverJobCashReward", "-5")]
    [InlineData("CatchUpXpMultiplier", "0.9")]
    [InlineData("CatchUpLevelCeiling", "0")]
    [InlineData("CatchUpLevelCeiling", "21")]
    [InlineData("CrimeHeatBlockThreshold", "5")]
    [InlineData("PvpEnabled", "maybe")]
    [InlineData("CrimeCashMultiplier", "0")]
    [InlineData("CrimeCashMultiplier", "0.2")]
    [InlineData("BaseFenceCashMultiplier", "4.01")]
    [InlineData("BuyerContractCashMultiplier", "-1")]
    [InlineData("TrainingCashCostMultiplier", "1,5")]
    [InlineData("EquipmentPriceMultiplier", "free")]
    public void OutOfRange_IsRefused(string key, string value) =>
        AfterHoursTuning.Find(key)!.Normalize(value, out _).Should().NotBeNull();

    [Fact]
    public void Overrides_ApplyOverDefaults_AndBadRowsAreSkippedAndReported()
    {
        var tuning = AfterHoursTuning.From(
        [
            KeyValuePair.Create("BankDepositFeePercent", "5"),
            KeyValuePair.Create("CatchUpXpMultiplier", " 2.25 "),
            KeyValuePair.Create("PvpEnabled", "False"),
            KeyValuePair.Create("CoverJobXpReward", "lots"),
            KeyValuePair.Create("NoSuchKey", "1"),
        ], out var invalid);

        (tuning.BankDepositFeePercent, tuning.CatchUpXpMultiplier, tuning.PvpEnabled, tuning.CoverJobXpReward).Should().Be((5, 2.25m, false, 12L));
        invalid.Select(i => i.Key).Should().BeEquivalentTo(["CoverJobXpReward", "NoSuchKey"]);
    }

    [Fact]
    public void Energy_UsesTheTunedCadence_AndKeepsThePartialInterval()
    {
        var state = PlayerCycleState.CreateInitial(1, "u", Start);
        state.Energy = 100;
        var tuning = AfterHoursTuning.Default with { EnergyRegenMinutesPerPoint = 3 };

        state.Reconcile(Start.AddMinutes(13), tuning);

        state.Energy.Should().Be(104);
        state.EnergyUpdatedAtUtc.Should().Be(Start.AddMinutes(12), "the 1 minute left over carries into the next interval");
    }

    [Fact]
    public void Training_CapComesFromTuning_AndALowerCapNeverTakesPointsAway()
    {
        var state = PlayerCycleState.CreateInitial(1, "u", Start);
        state.Reconcile(Start.AddDays(10), AfterHoursTuning.Default with { TrainingPointStorageCap = 5 });
        state.TrainingPoints.Should().Be(5);

        state.Reconcile(Start.AddDays(12), AfterHoursTuning.Default);
        state.TrainingPoints.Should().Be(5, "lowering the cap stops growth but keeps what is stored");
    }

    [Fact]
    public void CatchUp_StartsAtExactlyDay42_AndStopsAtTheCeiling()
    {
        var t = AfterHoursTuning.Default;
        var boundary = Start.AddDays(42);

        CatchUpRules.IsActive(Start, 5, boundary.AddTicks(-1), t).Should().BeFalse();
        CatchUpRules.IsActive(Start, 5, boundary, t).Should().BeTrue();
        CatchUpRules.IsActive(Start, 11, boundary, t).Should().BeTrue();
        CatchUpRules.IsActive(Start, 12, boundary, t).Should().BeFalse();
        CatchUpRules.XpMultiplier(Start, 1, boundary, t).Should().Be(1.5m);
        CatchUpRules.XpMultiplier(Start, 12, boundary, t).Should().Be(1m);
    }

    [Theory]
    [InlineData(20, 30)]
    [InlineData(5, 8)]   // 7.5 rounds half up, like every crime multiplier
    [InlineData(12, 18)]
    [InlineData(0, 0)]
    public void CatchUp_RoundsHalfUp(long xp, long expected) => CatchUpRules.Apply(xp, 1.5m).Should().Be(expected);

    [Fact]
    public void PausedPvp_BlocksTheAttackerFirst()
    {
        var state = PlayerCycleState.CreateInitial(1, "u", Start);
        PvpRules.AttackerBlockReason(state, Start, AfterHoursTuning.Default with { PvpEnabled = false }).Should().Be(PvpRules.PausedMessage);
        PvpRules.AttackerBlockReason(state, Start).Should().BeNull();
        PvpRules.AttackerRecovery(RiskStance.Reckless, AfterHoursTuning.Default with { PvpRecoveryRecklessMinutes = 45 }).Should().Be(TimeSpan.FromMinutes(45));
    }

    [Fact]
    public void EconomyDefaults_ReproduceTheCatalogueValuesExactly()
    {
        var t = AfterHoursTuning.Default;
        foreach (var crime in CrimeCatalogue.All)
            foreach (var approach in Enum.GetValues<CrimeApproach>())
            {
                var cash = CrimeRules.Odds(crime, approach, PlayerCycleState.CreateInitial(1, "u", Start)).Cash;
                t.CrimeCash(cash).Should().Be(cash);
            }
        foreach (var cargo in Enum.GetValues<CargoType>())
            for (var quantity = 1; quantity <= 5; quantity++)
                t.FencePayout(cargo, quantity).Should().Be(quantity * CargoCatalogue.FencePrice(cargo));
        foreach (var item in GearCatalogue.All) t.GearPrice(item).Should().Be(item.Price);
        for (var rank = 4; rank <= 12; rank++) t.TrainingCost(rank).Should().Be(TrainingRules.CashCost(rank));
        t.ContractCash(460).Should().Be(460);
    }

    [Fact]
    public void EachMultiplier_ScalesOnlyItsAmount_RoundedHalfUp()
    {
        var t = AfterHoursTuning.Default with
        {
            CrimeCashMultiplier = 1.25m, BaseFenceCashMultiplier = 0.5m, BuyerContractCashMultiplier = 2m,
            TrainingCashCostMultiplier = 1.5m, EquipmentPriceMultiplier = 0.25m
        };

        t.CrimeCash(35).Should().Be(44, "43.75 rounds half up");
        t.FencePayout(CargoType.Phone, 1).Should().Be(8, "7.5 rounds half up");
        t.ContractCash(460).Should().Be(920);
        t.TrainingCost(4).Should().Be(180);
        t.GearPrice(GearCatalogue.Find("W1")!).Should().Be(100);
        CargoCatalogue.FencePrice(CargoType.Phone).Should().Be(15, "the catalogue itself never changes");
        GearCatalogue.Find("W1")!.Price.Should().Be(400);
    }

    [Fact]
    public void AMalformedMultiplierRow_FallsBackToOne()
    {
        var tuning = AfterHoursTuning.From([KeyValuePair.Create("CrimeCashMultiplier", "10"), KeyValuePair.Create("EquipmentPriceMultiplier", "abc")], out var invalid);
        (tuning.CrimeCashMultiplier, tuning.EquipmentPriceMultiplier).Should().Be((1m, 1m));
        invalid.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(3, "1.1", 50)]   // 45 × 1.1 = 49.5 → 50, rounded once on the total (not 17 × 3 = 51)
    [InlineData(1, "1.1", 17)]   // 16.5 → 17
    [InlineData(1, "0.5", 8)]    // 7.5 → 8
    [InlineData(2, "0.5", 15)]
    [InlineData(3, "1.0", 45)]
    public void FencePayout_RoundsTheTotalOnce(int phones, string multiplier, long payout) =>
        (AfterHoursTuning.Default with { BaseFenceCashMultiplier = decimal.Parse(multiplier, System.Globalization.CultureInfo.InvariantCulture) })
            .FencePayout(CargoType.Phone, phones).Should().Be(payout);
}
