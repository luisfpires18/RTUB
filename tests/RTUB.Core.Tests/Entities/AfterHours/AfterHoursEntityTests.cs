using FluentAssertions;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

public class AfterHoursEntityTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(GameCycleKind.Pilot)]
    [InlineData(GameCycleKind.Live)]
    public void GameCycle_Create_IsScheduled_WithItsKindAndBoundaries(GameCycleKind kind)
    {
        var cycle = GameCycle.Create(7, kind, Start, Start.AddDays(30));

        cycle.FiscalYearId.Should().Be(7);
        cycle.Kind.Should().Be(kind);
        cycle.Status.Should().Be(GameCycleStatus.Scheduled);
        cycle.StartUtc.Should().Be(Start);
        cycle.EndUtc.Should().Be(Start.AddDays(30));
    }

    [Fact]
    public void GameCycle_Kinds_AreDistinct()
    {
        GameCycleKind.Pilot.Should().NotBe(GameCycleKind.Live);
        ((int)GameCycleKind.Pilot).Should().NotBe((int)GameCycleKind.Live);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GameCycle_Create_RejectsEndNotAfterStart(int days)
    {
        var act = () => GameCycle.Create(1, GameCycleKind.Live, Start, Start.AddDays(days));

        act.Should().Throw<ArgumentException>().WithParameterName("endUtc");
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void GameCycle_Create_RejectsNonUtcBoundaries(DateTimeKind kind)
    {
        var notUtc = DateTime.SpecifyKind(Start, kind);

        FluentActions.Invoking(() => GameCycle.Create(1, GameCycleKind.Live, notUtc, Start.AddDays(1)))
            .Should().Throw<ArgumentException>().WithParameterName("startUtc");
        FluentActions.Invoking(() => GameCycle.Create(1, GameCycleKind.Live, Start, DateTime.SpecifyKind(Start.AddDays(1), kind)))
            .Should().Throw<ArgumentException>().WithParameterName("endUtc");
    }

    [Fact]
    public void GameCycle_IsPlayableAt_OnlyWhenActive_AndInsideStartInclusiveEndExclusive()
    {
        var cycle = GameCycle.Create(1, GameCycleKind.Live, Start, Start.AddDays(1));

        cycle.IsPlayableAt(Start).Should().BeFalse("a Scheduled cycle is not playable");

        cycle.Status = GameCycleStatus.Active;
        cycle.IsPlayableAt(Start.AddTicks(-1)).Should().BeFalse();
        cycle.IsPlayableAt(Start).Should().BeTrue();
        cycle.IsPlayableAt(Start.AddDays(1).AddTicks(-1)).Should().BeTrue();
        cycle.IsPlayableAt(Start.AddDays(1)).Should().BeFalse();

        cycle.Status = GameCycleStatus.Finished;
        cycle.IsPlayableAt(Start).Should().BeFalse();
    }

    [Fact]
    public void PlayerCycleState_CreateInitial_HasExactlyTheStartingValues()
    {
        var state = PlayerCycleState.CreateInitial(3, "user-1", Start);

        state.GameCycleId.Should().Be(3);
        state.UserId.Should().Be("user-1");
        state.Level.Should().Be(1);
        state.XP.Should().Be(0);
        state.WalletCash.Should().Be(400);
        state.BankCash.Should().Be(0);
        state.MaxEnergy.Should().Be(240);
        state.Energy.Should().Be(240);
        state.EnergyUpdatedAtUtc.Should().Be(Start);
        state.Heat.Should().Be(0);
        state.HeatUpdatedAtUtc.Should().Be(Start);
        state.Toughness.Should().Be(4);
        state.Stealth.Should().Be(4);
        state.Smarts.Should().Be(4);
        state.Charisma.Should().Be(4);
    }

    [Fact]
    public void PlayerCycleState_CreateInitial_RejectsMissingUserAndNonUtcTime()
    {
        FluentActions.Invoking(() => PlayerCycleState.CreateInitial(1, " ", Start))
            .Should().Throw<ArgumentException>().WithParameterName("userId");
        FluentActions.Invoking(() => PlayerCycleState.CreateInitial(1, "u", DateTime.SpecifyKind(Start, DateTimeKind.Local)))
            .Should().Throw<ArgumentException>().WithParameterName("utcNow");
    }
}
