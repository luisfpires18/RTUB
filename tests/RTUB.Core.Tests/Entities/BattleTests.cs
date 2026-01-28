using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public class BattleTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var attackerId = 1;
        var defenderId = 2;
        var seed = 12345;
        var outcome = BattleOutcome.AttackerWon;

        // Act
        var battle = Battle.Create(attackerId, defenderId, seed, outcome);

        // Assert
        battle.Should().NotBeNull();
        battle.AttackerCharacterId.Should().Be(attackerId);
        battle.DefenderCharacterId.Should().Be(defenderId);
        battle.Seed.Should().Be(seed);
        battle.Outcome.Should().Be(outcome);
        battle.AttackerXP.Should().Be(0);
        battle.DefenderXP.Should().Be(0);
        battle.AttackerFidelis.Should().Be(0m);
        battle.DefenderFidelis.Should().Be(0m);
        battle.ReplayJson.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidAttackerId_ShouldThrowException(int attackerId)
    {
        // Act
        var act = () => Battle.Create(attackerId, 2, 12345, BattleOutcome.AttackerWon);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Attacker character ID*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidDefenderId_ShouldThrowException(int defenderId)
    {
        // Act
        var act = () => Battle.Create(1, defenderId, 12345, BattleOutcome.AttackerWon);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Defender character ID*");
    }

    [Fact]
    public void Create_WithSameAttackerAndDefender_ShouldThrowException()
    {
        // Act
        var act = () => Battle.Create(1, 1, 12345, BattleOutcome.AttackerWon);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be the same character*");
    }

    [Fact]
    public void Create_WithAllOutcomes_ShouldCreateInstance()
    {
        // Arrange & Act
        var attackerWon = Battle.Create(1, 2, 12345, BattleOutcome.AttackerWon);
        var defenderWon = Battle.Create(1, 2, 12345, BattleOutcome.DefenderWon);
        var draw = Battle.Create(1, 2, 12345, BattleOutcome.Draw);

        // Assert
        attackerWon.Outcome.Should().Be(BattleOutcome.AttackerWon);
        defenderWon.Outcome.Should().Be(BattleOutcome.DefenderWon);
        draw.Outcome.Should().Be(BattleOutcome.Draw);
    }

    #endregion

    #region SetRewards Tests

    [Fact]
    public void SetRewards_WithValidData_ShouldSetRewards()
    {
        // Arrange
        var battle = Battle.Create(1, 2, 12345, BattleOutcome.AttackerWon);
        var xp = 50;
        var fidelis = 10m;

        // Act
        battle.SetRewards(xp, fidelis);

        // Assert
        battle.AttackerXP.Should().Be(xp);
        battle.AttackerFidelis.Should().Be(fidelis);
        battle.DefenderXP.Should().Be(0); // Always 0 for AI
        battle.DefenderFidelis.Should().Be(0m); // Always 0 for AI
    }

    [Fact]
    public void SetRewards_WithZeroValues_ShouldSetRewards()
    {
        // Arrange
        var battle = Battle.Create(1, 2, 12345, BattleOutcome.DefenderWon);

        // Act
        battle.SetRewards(0, 0m);

        // Assert
        battle.AttackerXP.Should().Be(0);
        battle.AttackerFidelis.Should().Be(0m);
        battle.DefenderXP.Should().Be(0);
        battle.DefenderFidelis.Should().Be(0m);
    }

    [Fact]
    public void SetRewards_WithNegativeXP_ShouldThrowException()
    {
        // Arrange
        var battle = Battle.Create(1, 2, 12345, BattleOutcome.AttackerWon);

        // Act
        var act = () => battle.SetRewards(-1, 10m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Attacker XP*");
    }

    [Fact]
    public void SetRewards_WithNegativeFidelis_ShouldThrowException()
    {
        // Arrange
        var battle = Battle.Create(1, 2, 12345, BattleOutcome.AttackerWon);

        // Act
        var act = () => battle.SetRewards(50, -1m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Attacker Fidelis*");
    }

    [Fact]
    public void SetRewards_MultipleTimes_ShouldUpdateRewards()
    {
        // Arrange
        var battle = Battle.Create(1, 2, 12345, BattleOutcome.AttackerWon);

        // Act
        battle.SetRewards(50, 10m);
        battle.SetRewards(100, 20m);

        // Assert
        battle.AttackerXP.Should().Be(100);
        battle.AttackerFidelis.Should().Be(20m);
        battle.DefenderXP.Should().Be(0);
        battle.DefenderFidelis.Should().Be(0m);
    }

    #endregion

    #region SetReplay Tests

    [Fact]
    public void SetReplay_WithValidJson_ShouldSetReplay()
    {
        // Arrange
        var battle = Battle.Create(1, 2, 12345, BattleOutcome.AttackerWon);
        var replayJson = "{\"events\":[]}";

        // Act
        battle.SetReplay(replayJson);

        // Assert
        battle.ReplayJson.Should().Be(replayJson);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void SetReplay_WithEmptyOrNull_ShouldThrowException(string? replayJson)
    {
        // Arrange
        var battle = Battle.Create(1, 2, 12345, BattleOutcome.AttackerWon);

        // Act
        var act = () => battle.SetReplay(replayJson!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Replay JSON*");
    }

    [Fact]
    public void SetReplay_MultipleTimes_ShouldUpdateReplay()
    {
        // Arrange
        var battle = Battle.Create(1, 2, 12345, BattleOutcome.AttackerWon);
        var replayJson1 = "{\"events\":[]}";
        var replayJson2 = "{\"events\":[{\"type\":\"Attack\"}]}";

        // Act
        battle.SetReplay(replayJson1);
        battle.SetReplay(replayJson2);

        // Assert
        battle.ReplayJson.Should().Be(replayJson2);
    }

    #endregion
}
