using FluentAssertions;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

/// <summary>AH-008 objective and championship rules that need no database.</summary>
public class AfterHoursObjectiveRulesTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
    private static GameCycle Cycle(int days = 365) =>
        new() { Id = 1, StartUtc = Start, EndUtc = Start.AddDays(days), Status = GameCycleStatus.Active };

    // ------------------------------------------------------------------ daily rotation

    [Fact]
    public void DailySet_IsThreeConsecutiveTemplates_SameForADate_RotatingDaily()
    {
        var day = new DateOnly(2026, 10, 1);
        var today = ObjectiveCatalogue.DailyFor(day).Select(d => d.Key).ToList();

        today.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        ObjectiveCatalogue.DailyFor(day).Select(d => d.Key).Should().Equal(today);
        var start = day.DayNumber % 5;
        today.Should().Equal(Enumerable.Range(0, 3).Select(i => $"D0{(start + i) % 5 + 1}"));
        ObjectiveCatalogue.DailyFor(day.AddDays(1)).Select(d => d.Key).Should().NotEqual(today);

        var seen = Enumerable.Range(0, 5).SelectMany(i => ObjectiveCatalogue.DailyFor(day.AddDays(i)).Select(d => d.Key)).Distinct();
        seen.Should().HaveCount(5, "every template comes round within five days");
    }

    [Fact]
    public void DailyRewards_AreXpOnly()
    {
        ObjectiveCatalogue.Daily.Select(d => (d.Key, d.Target, d.XpReward, d.Points)).Should().Equal(
            ("D01", 3L, 15L, 0), ("D02", 1L, 10L, 0), ("D03", 60L, 15L, 0), ("D04", 1L, 10L, 0), ("D05", 1L, 15L, 0));
    }

    // ------------------------------------------------------------------ weeks

    [Fact]
    public void Weeks_AreSevenDaysFromTheCycleStart_HalfOpen_AndTheLastOneIsShort()
    {
        var cycle = Cycle(days: 17);

        ObjectiveCatalogue.WeekOf(cycle, Start).Should().Be(new GameWeek(1, Start, Start.AddDays(7)));
        ObjectiveCatalogue.WeekOf(cycle, Start.AddDays(7).AddTicks(-1)).Index.Should().Be(1);
        ObjectiveCatalogue.WeekOf(cycle, Start.AddDays(7)).Should().Be(new GameWeek(2, Start.AddDays(7), Start.AddDays(14)));
        ObjectiveCatalogue.WeekOf(cycle, Start.AddDays(16)).Should().Be(new GameWeek(3, Start.AddDays(14), Start.AddDays(17)));
    }

    [Fact]
    public void WeeklySets_AlternateByParity_AndRespectCategoryCaps()
    {
        ObjectiveCatalogue.WeeklyFor(1).Select(d => d.Key).Should().Equal("WA1", "WA2", "WA3", "WPVP", "WA5");
        ObjectiveCatalogue.WeeklyFor(2).Select(d => d.Key).Should().Equal("WB1", "WB2", "WB3", "WPVP", "WB5");
        ObjectiveCatalogue.WeeklyFor(3).Should().BeSameAs(ObjectiveCatalogue.WeeklyFor(1));
        ObjectiveCatalogue.FamilyFor(1).Select(d => (d.Key, d.Target, d.Points)).Should().Equal(("FA1", 30L, 25), ("FA2", 3_000L, 25), ("FA3", 20L, 25), ("FA4", 4L, 25));
        ObjectiveCatalogue.FamilyFor(2).Select(d => (d.Key, d.Target, d.Points)).Should().Equal(("FB1", 40L, 25), ("FB2", 2_000L, 25), ("FB3", 4L, 25), ("FB4", 4L, 25));

        foreach (var week in new[] { 1, 2 })
        {
            var set = ObjectiveCatalogue.WeeklyFor(week);
            set.Sum(d => d.Points).Should().Be(100);
            set.GroupBy(d => d.Category!.Value).Should().OnlyContain(g => g.Sum(d => d.Points) == ObjectiveCatalogue.CategoryCap(g.Key));
            ObjectiveCatalogue.FamilyFor(week).Sum(d => d.Points).Should().Be(100);
        }
    }

    // ------------------------------------------------------------------ action → metrics

    [Fact]
    public void Metrics_ComeFromAcceptedReceipts_Only()
    {
        ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = PlayerActionKind.Crime, Succeeded = true, EnergyDelta = -10, WalletDelta = 35 })
            .Should().BeEquivalentTo(new Dictionary<ObjectiveMetric, long>
            {
                [ObjectiveMetric.EnergySpent] = 10, [ObjectiveMetric.CrimeEnergy] = 10,
                [ObjectiveMetric.SuccessfulCrimes] = 1, [ObjectiveMetric.CrimeCash] = 35
            });
        ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = PlayerActionKind.Crime, Succeeded = false, EnergyDelta = -10 })
            .Keys.Should().BeEquivalentTo([ObjectiveMetric.EnergySpent, ObjectiveMetric.CrimeEnergy]);
        ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = PlayerActionKind.CoverJob, EnergyDelta = -10, HeatDelta = 0 })
            .Keys.Should().BeEquivalentTo([ObjectiveMetric.EnergySpent], "a cover job at 0 heat reduces nothing");
        ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = PlayerActionKind.FenceSale, CargoDelta = -3, WalletDelta = 45 })
            .Should().BeEquivalentTo(new Dictionary<ObjectiveMetric, long> { [ObjectiveMetric.FencedUnits] = 3, [ObjectiveMetric.CargoMoved] = 3 });
        ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = PlayerActionKind.ContractDelivery, CargoDelta = -4, WalletDelta = 460 })
            .Should().BeEquivalentTo(new Dictionary<ObjectiveMetric, long> { [ObjectiveMetric.ContractsDelivered] = 1, [ObjectiveMetric.CargoMoved] = 4 });
        ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = PlayerActionKind.DonateToFamily, WalletDelta = -250 })
            .Should().BeEquivalentTo(new Dictionary<ObjectiveMetric, long> { [ObjectiveMetric.Donated] = 250 });
        ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = PlayerActionKind.TrainSkill, EnergyDelta = -20, WalletDelta = -120 })
            .Should().BeEquivalentTo(new Dictionary<ObjectiveMetric, long> { [ObjectiveMetric.EnergySpent] = 20 });
        ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = PlayerActionKind.PvpAttack, EnergyDelta = -20, PvpBattle = new PvpBattle { AttackerWon = true } })
            .Should().BeEquivalentTo(new Dictionary<ObjectiveMetric, long> { [ObjectiveMetric.EnergySpent] = 20, [ObjectiveMetric.PvpWins] = 1 });

        foreach (var kind in new[] { PlayerActionKind.Deposit, PlayerActionKind.Withdraw, PlayerActionKind.PurchaseGear, PlayerActionKind.CreateFamily, PlayerActionKind.EquipGear })
            ObjectiveRules.ActorMetrics(new PlayerActionReceipt { Action = kind, WalletDelta = -1_500 }).Should().BeEmpty($"{kind} counts for nothing");
    }

    [Fact]
    public void Advance_ClampsAtTheTarget_AndCompletesOnce()
    {
        var at = Start.AddDays(1);
        var row = new PlayerObjectiveProgress { Target = 3 };

        ObjectiveRules.Advance(row, 2, at).Should().BeFalse();
        ObjectiveRules.Advance(row, 5, at).Should().BeTrue();
        (row.Progress, row.CompletedAtUtc).Should().Be((3L, at));
        ObjectiveRules.Advance(row, 1, at.AddHours(1)).Should().BeFalse();
        row.CompletedAtUtc.Should().Be(at);
    }

    // ------------------------------------------------------------------ scoring

    [Theory]
    [InlineData("1", 10)]
    [InlineData("0.75", 7)]
    [InlineData("0.30", 3)]
    [InlineData("0.0999", 0)]
    public void PvpPoints_AreTenTimesTheLootMultiplier_Floored(string multiplier, int points) =>
        ObjectiveCatalogue.PvpPoints(decimal.Parse(multiplier, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(points);

    private static PlayerObjectiveProgress Weekly(ObjectiveCategory category, int points) =>
        new() { Period = ObjectivePeriod.Weekly, Category = category, PointsAwarded = points };

    [Fact]
    public void WeeklyScore_UsesStoredPoints_WithCategoryAndTotalCaps()
    {
        ObjectiveCatalogue.IndividualWeeklyScore([Weekly(ObjectiveCategory.SoloCrime, 7)]).Should().Be(7, "stored points, not today's catalogue");

        var overfull = new[]
        {
            Weekly(ObjectiveCategory.SoloCrime, 20), Weekly(ObjectiveCategory.SoloCrime, 20), Weekly(ObjectiveCategory.SoloCrime, 20),
            Weekly(ObjectiveCategory.CargoContracts, 30), Weekly(ObjectiveCategory.Pvp, 25), Weekly(ObjectiveCategory.FamilyParticipation, 20)
        };
        ObjectiveCatalogue.CategoryScores(overfull).Should().BeEquivalentTo(new Dictionary<ObjectiveCategory, int>
        {
            [ObjectiveCategory.SoloCrime] = 40, [ObjectiveCategory.CargoContracts] = 20,
            [ObjectiveCategory.Pvp] = 20, [ObjectiveCategory.FamilyParticipation] = 20
        });
        ObjectiveCatalogue.IndividualWeeklyScore(overfull).Should().Be(100);
        ObjectiveCatalogue.FamilyWeeklyScore([new FamilyObjectiveProgress { PointsAwarded = 75 }, new FamilyObjectiveProgress { PointsAwarded = 50 }]).Should().Be(100);
    }

    [Fact]
    public void BestTwelve_SumsAllWhenFewer_TheTopTwelveWhenMore()
    {
        var five = Enumerable.Range(1, 5).ToDictionary(w => w, w => w * 10);
        ObjectiveCatalogue.BestWeeksTotal(five).Should().BeEquivalentTo((150, new[] { 1, 2, 3, 4, 5 }));

        var twelve = Enumerable.Range(1, 12).ToDictionary(w => w, _ => 50);
        ObjectiveCatalogue.BestWeeksTotal(twelve).Total.Should().Be(600);

        var fifteen = Enumerable.Range(1, 15).ToDictionary(w => w, w => w); // 1..15 → best 12 are 4..15
        var (total, counted) = ObjectiveCatalogue.BestWeeksTotal(fifteen);
        total.Should().Be(Enumerable.Range(4, 12).Sum());
        counted.Should().Equal(Enumerable.Range(4, 12));

        ObjectiveCatalogue.BestWeeksTotal(new Dictionary<int, int> { [1] = 0, [2] = 30 }).Should().BeEquivalentTo((30, new[] { 2 }), "empty weeks are not scoring weeks");
    }
}
