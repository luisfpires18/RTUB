using FluentAssertions;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

/// <summary>AH-003 rules as pure functions: time reconciliation, XP, crimes, jail, cover jobs, bank.</summary>
public class AfterHoursRulesTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PlayerCycleState NewState() => PlayerCycleState.CreateInitial(1, "u", T0);

    private static Func<int> Rolls(params int[] rolls)
    {
        var queue = new Queue<int>(rolls);
        return () => queue.Dequeue();
    }

    // ------------------------------------------------------------------ energy

    [Fact]
    public void Energy_SixMinutes_RestoresOne()
    {
        var s = NewState();
        s.Energy = 100;

        s.ReconcileEnergy(T0.AddMinutes(6));

        s.Energy.Should().Be(101);
        s.EnergyUpdatedAtUtc.Should().Be(T0.AddMinutes(6));
    }

    [Fact]
    public void Energy_PartialInterval_IsCarriedOver()
    {
        var s = NewState();
        s.Energy = 100;

        s.ReconcileEnergy(T0.AddMinutes(13));
        s.Energy.Should().Be(102);
        s.EnergyUpdatedAtUtc.Should().Be(T0.AddMinutes(12), "the spare minute counts toward the next point");

        s.ReconcileEnergy(T0.AddMinutes(18));
        s.Energy.Should().Be(103);
    }

    [Fact]
    public void Energy_EmptyToFull_Takes24Hours_AndCaps()
    {
        var s = NewState();
        s.Energy = 0;

        s.ReconcileEnergy(T0.AddHours(24).AddMinutes(-6));
        s.Energy.Should().Be(239);

        s.ReconcileEnergy(T0.AddHours(48));
        s.Energy.Should().Be(240);
    }

    [Fact]
    public void Energy_WhenFull_BanksNoHiddenReserve()
    {
        var s = NewState(); // full at T0

        s.ReconcileEnergy(T0.AddHours(5));
        s.EnergyUpdatedAtUtc.Should().Be(T0.AddHours(5));

        s.Energy -= 10;
        s.ReconcileEnergy(T0.AddHours(5).AddMinutes(5));
        s.Energy.Should().Be(230, "regeneration starts from when energy was spent, not from the old timestamp");
    }

    // ------------------------------------------------------------------ heat

    [Fact]
    public void Heat_TenMinutes_DecaysOne_AndCarriesTheRemainder()
    {
        var s = NewState();
        s.Heat = 20;

        s.ReconcileHeat(T0.AddMinutes(10));
        s.Heat.Should().Be(19);

        s.ReconcileHeat(T0.AddMinutes(25));
        s.Heat.Should().Be(18);
        s.HeatUpdatedAtUtc.Should().Be(T0.AddMinutes(20));
    }

    [Fact]
    public void Heat_NeverGoesBelowZero_AndZeroBanksNothing()
    {
        var s = NewState();
        s.Heat = 2;

        s.ReconcileHeat(T0.AddHours(3));
        s.Heat.Should().Be(0);
        s.HeatUpdatedAtUtc.Should().Be(T0.AddHours(3));

        s.AddHeat(5, T0.AddHours(3));
        s.ReconcileHeat(T0.AddHours(3).AddMinutes(9));
        s.Heat.Should().Be(5);
    }

    [Fact]
    public void Crime_IsBlockedAtHeat80_AfterReconciliation()
    {
        var s = NewState();
        s.Heat = 81;

        AfterHoursActions.CommitCrime(s, "C01", CrimeApproach.Standard, T0, Rolls(1)).Error
            .Should().Contain("heat");

        var cooled = NewState();
        cooled.Heat = 81;
        var result = AfterHoursActions.CommitCrime(cooled, "C01", CrimeApproach.Standard, T0.AddMinutes(20), Rolls(1));
        result.Error.Should().BeNull("20 minutes decay 81 to 79");
    }

    // ------------------------------------------------------------------ XP

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 100)]
    [InlineData(5, 1_120)]
    [InlineData(8, 4_732)]
    [InlineData(10, 9_540)]
    [InlineData(12, 16_940)]
    [InlineData(16, 41_820)]
    [InlineData(20, 83_980)]
    public void XpCurve_Formula(int level, long xp)
    {
        AfterHoursLevels.XpForLevel(level).Should().Be(xp);
        AfterHoursLevels.LevelForXp(xp).Should().Be(level);
        if (level > 1) AfterHoursLevels.LevelForXp(xp - 1).Should().Be(level - 1);
    }

    [Fact]
    public void Level_CapsAt20_WhileXpKeepsAccumulating()
    {
        var s = NewState();
        s.AddXp(10_000_000);

        s.Level.Should().Be(20);
        s.XP.Should().Be(10_000_000);
    }

    // ------------------------------------------------------------------ crime odds

    [Theory]
    [InlineData(CrimeApproach.Careful, 90, 28, 20, 5, 2)]
    [InlineData(CrimeApproach.Standard, 82, 35, 20, 5, 3)]
    [InlineData(CrimeApproach.Bold, 74, 44, 22, 6, 7)]
    public void Odds_ApplyApproachModifiers_WithHalfUpRounding(
        CrimeApproach approach, int chance, long cash, long xp, long failureXp, int heat)
    {
        var odds = CrimeRules.Odds(CrimeCatalogue.Find("C01")!, approach, NewState());

        odds.Should().Be(new CrimeOdds(chance, cash, xp, failureXp, heat));
    }

    [Fact]
    public void Odds_Rounding_OnOddValues()
    {
        var c02 = CrimeCatalogue.Find("C02")!; // cash 50, XP 26, heat 4

        CrimeRules.Odds(c02, CrimeApproach.Bold, NewState()).Should().Be(new CrimeOdds(70, 63, 29, 7, 8)); // 62.5→63, 28.6→29, 7.25→7
        CrimeRules.Odds(c02, CrimeApproach.Standard, NewState()).FailureXp.Should().Be(7); // 6.5→7
        CrimeRules.Odds(CrimeCatalogue.Find("C05")!, CrimeApproach.Careful, NewState()).Heat.Should().Be(3); // ⌈5/2⌉
    }

    [Fact]
    public void Odds_SkillAndHeatShiftTheChance()
    {
        var c01 = CrimeCatalogue.Find("C01")!; // Stealth, base 82
        var s = NewState();
        s.Stealth = 7;
        s.Heat = 25;

        CrimeRules.Odds(c01, CrimeApproach.Standard, s).Chance.Should().Be(82 + 6 - 4);

        s.Toughness = 20; // not the crime's skill
        CrimeRules.Odds(c01, CrimeApproach.Standard, s).Chance.Should().Be(84);
    }

    [Fact]
    public void Odds_AreClampedTo15And95()
    {
        var s = NewState();
        s.Stealth = 20;
        CrimeRules.Odds(CrimeCatalogue.Find("C01")!, CrimeApproach.Careful, s).Chance.Should().Be(95);

        s.Smarts = 0;
        s.Heat = 200; // 58 - 8 - 40 - 8 = 2
        CrimeRules.Odds(CrimeCatalogue.Find("C12")!, CrimeApproach.Bold, s).Chance.Should().Be(15);
    }

    [Fact]
    public void Catalogue_HasTheTwelveManualCrimes()
    {
        CrimeCatalogue.All.Select(c => c.Id).Should().Equal(
            "C01", "C02", "C03", "C04", "C05", "C06", "C07", "C08", "C09", "C10", "C11", "C12");
        CrimeCatalogue.Find("C12").Should().BeEquivalentTo(new
        {
            RequiredLevel = 17, EnergyCost = 25, BaseChance = 58, Cash = 280L, Xp = 110L, Heat = 8, Skill = PlayerSkill.Smarts
        });
    }

    // ------------------------------------------------------------------ crime execution

    [Fact]
    public void Crime_LockedByLevel()
    {
        AfterHoursActions.CommitCrime(NewState(), "C02", CrimeApproach.Standard, T0, Rolls(1)).Error
            .Should().Be("Requires level 2.");
    }

    [Fact]
    public void Crime_RejectsUnknownCrime_InsufficientEnergy_AndJail()
    {
        AfterHoursActions.CommitCrime(NewState(), "C99", CrimeApproach.Standard, T0, Rolls(1)).Error.Should().NotBeNull();

        var tired = NewState();
        tired.Energy = 9;
        AfterHoursActions.CommitCrime(tired, "C01", CrimeApproach.Standard, T0, Rolls(1)).Error.Should().Be("Not enough energy.");

        var jailed = NewState();
        jailed.JailUntilUtc = T0.AddMinutes(1);
        AfterHoursActions.CommitCrime(jailed, "C01", CrimeApproach.Standard, T0, Rolls(1)).Error.Should().Be("You are in jail.");
        AfterHoursActions.CommitCrime(jailed, "C01", CrimeApproach.Standard, T0.AddMinutes(1), Rolls(1)).Error
            .Should().BeNull("jail ends by itself when its time has passed");
    }

    [Fact]
    public void Crime_Success_SpendsEnergy_PaysCashAndXp_AddsHeat()
    {
        var s = NewState();

        var receipt = AfterHoursActions.CommitCrime(s, "C01", CrimeApproach.Standard, T0, Rolls(82)).Receipt!;

        receipt.Succeeded.Should().BeTrue("a roll equal to the chance succeeds");
        receipt.SuccessChance.Should().Be(82);
        receipt.SuccessRoll.Should().Be(82);
        (s.Energy, s.WalletCash, s.XP, s.Heat, s.Level).Should().Be((230, 435L, 20L, 3, 1));
        (receipt.EnergyDelta, receipt.WalletDelta, receipt.XpDelta, receipt.HeatDelta).Should().Be((-10, 35L, 20L, 3));
        receipt.Request.Should().Be("crime:C01:Standard");
    }

    [Fact]
    public void Crime_Failure_Pays25PercentXp_NoCash_AddsHeat_AndMayJail()
    {
        var free = NewState();
        var r1 = AfterHoursActions.CommitCrime(free, "C01", CrimeApproach.Bold, T0, Rolls(75, 12)).Receipt!;
        r1.Succeeded.Should().BeFalse();
        r1.Jailed.Should().BeFalse("jail chance at level 1, heat 0 is 11%; roll 12 misses");
        (free.WalletCash, free.XP, free.Heat, free.Energy).Should().Be((400L, 6L, 7, 230));

        var caught = NewState();
        var r2 = AfterHoursActions.CommitCrime(caught, "C01", CrimeApproach.Bold, T0, Rolls(75, 11)).Receipt!;
        r2.Jailed.Should().BeTrue();
        r2.JailUntilUtc.Should().Be(T0.AddMinutes(2));
        caught.JailUntilUtc.Should().Be(T0.AddMinutes(2));
    }

    [Fact]
    public void Crime_LevelUp_IsRecorded()
    {
        var s = NewState();
        s.XP = 90;

        var receipt = AfterHoursActions.CommitCrime(s, "C01", CrimeApproach.Standard, T0, Rolls(1)).Receipt!;

        (receipt.LevelBefore, receipt.LevelAfter, s.Level).Should().Be((1, 2, 2));
    }

    // ------------------------------------------------------------------ jail policy (AH-003 choice)

    [Theory]
    [InlineData(1, 0, 11)]
    [InlineData(17, 79, 42)]
    [InlineData(1, 500, 60)]
    public void JailChance(int level, int heat, int expected) => JailPolicy.Chance(level, heat).Should().Be(expected);

    [Theory]
    [InlineData(1, 0, 2)]
    [InlineData(15, 40, 7)]
    [InlineData(17, 79, 8)]
    public void JailDuration(int level, int heat, int minutes) =>
        JailPolicy.Duration(level, heat).Should().Be(TimeSpan.FromMinutes(minutes));

    // ------------------------------------------------------------------ cover job

    [Fact]
    public void CoverJob_Costs10Energy_Cools15_Pays20And12Xp()
    {
        var s = NewState();
        s.Heat = 30;

        var receipt = AfterHoursActions.TakeCoverJob(s, T0).Receipt!;

        (s.Energy, s.Heat, s.WalletCash, s.XP).Should().Be((230, 15, 420L, 12L));
        (receipt.EnergyDelta, receipt.HeatDelta).Should().Be((-10, -15));
        s.CoverJobDailyUsedOn.Should().Be(DateOnly.FromDateTime(T0));
    }

    [Fact]
    public void CoverJob_LowHeat_OncePerUtcDay()
    {
        var s = NewState();

        AfterHoursActions.TakeCoverJob(s, T0).Error.Should().BeNull();
        AfterHoursActions.TakeCoverJob(s, T0.AddHours(11).AddMinutes(59)).Error.Should().Be("You already worked a cover job today.");
        AfterHoursActions.TakeCoverJob(s, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)).Error.Should().BeNull();
    }

    [Fact]
    public void CoverJob_HighHeat_RepeatsUntilBelow50_WithoutUsingTheDailyOne()
    {
        var s = NewState();
        s.Heat = 70;

        AfterHoursActions.TakeCoverJob(s, T0).Error.Should().BeNull();
        s.Heat.Should().Be(55);
        AfterHoursActions.TakeCoverJob(s, T0).Error.Should().BeNull();
        s.Heat.Should().Be(40);
        s.CoverJobDailyUsedOn.Should().BeNull("high-heat jobs do not use the daily allowance");

        AfterHoursActions.TakeCoverJob(s, T0).Error.Should().BeNull("the daily low-heat job is still available");
        AfterHoursActions.TakeCoverJob(s, T0).Error.Should().NotBeNull();
    }

    [Fact]
    public void CoverJob_NeverTakesHeatBelowZero()
    {
        var s = NewState();
        s.Heat = 4;

        var receipt = AfterHoursActions.TakeCoverJob(s, T0).Receipt!;

        s.Heat.Should().Be(0);
        receipt.HeatDelta.Should().Be(-4);
    }

    [Fact]
    public void CoverJob_RejectsLowEnergyAndJail()
    {
        var s = NewState();
        s.Energy = 9;
        AfterHoursActions.TakeCoverJob(s, T0).Error.Should().Be("Not enough energy.");

        var jailed = NewState();
        jailed.JailUntilUtc = T0.AddMinutes(3);
        AfterHoursActions.TakeCoverJob(jailed, T0).Error.Should().Be("You are in jail.");
    }

    // ------------------------------------------------------------------ bank

    [Theory]
    [InlineData(100, 2, 98)]
    [InlineData(50, 1, 49)]
    [InlineData(51, 2, 49)]
    [InlineData(400, 8, 392)]
    public void Deposit_Charges2PercentRoundedUp(long amount, long fee, long net)
    {
        var s = NewState();

        var receipt = AfterHoursActions.Deposit(s, amount).Receipt!;

        AfterHoursActions.DepositFee(amount).Should().Be(fee);
        (s.WalletCash, s.BankCash).Should().Be((400 - amount, net));
        (receipt.WalletDelta, receipt.BankDelta).Should().Be((-amount, net));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1)]   // fee 1, nothing left to credit
    [InlineData(401)] // more than the wallet
    public void Deposit_IsRefused_AndChangesNothing(long amount)
    {
        var s = NewState();

        AfterHoursActions.Deposit(s, amount).Error.Should().NotBeNull();
        (s.WalletCash, s.BankCash).Should().Be((400L, 0L));
    }

    [Fact]
    public void Withdraw_IsFree_AndCannotOverdraw()
    {
        var s = NewState();
        s.BankCash = 100;

        AfterHoursActions.Withdraw(s, 101).Error.Should().Be("Not enough cash in the bank.");
        AfterHoursActions.Withdraw(s, 100).Error.Should().BeNull();
        (s.WalletCash, s.BankCash).Should().Be((500L, 0L));
    }
}
