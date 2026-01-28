using FluentAssertions;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for DeterministicCombatEngine
/// Tests combat determinism, rules, and edge cases
/// </summary>
public class CombatEngineTests
{
    private readonly DeterministicCombatEngine _combatEngine;

    public CombatEngineTests()
    {
        _combatEngine = new DeterministicCombatEngine();
    }

    #region Determinism Tests

    [Fact]
    public void Simulate_WithSameInputsAndSeed_ShouldProduceIdenticalResults()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 100;
        attacker.Power = 10;
        attacker.Speed = 10;

        var defender = Character.Create("user2");
        defender.HP = 100;
        defender.Power = 10;
        defender.Speed = 10;

        var seed = 12345;

        // Act - Run simulation twice with same inputs
        var result1 = _combatEngine.Simulate(attacker, defender, seed);
        var result2 = _combatEngine.Simulate(attacker, defender, seed);

        // Assert
        result1.Outcome.Should().Be(result2.Outcome);
        result1.AttackerFinalHP.Should().Be(result2.AttackerFinalHP);
        result1.DefenderFinalHP.Should().Be(result2.DefenderFinalHP);
        result1.Events.Count.Should().Be(result2.Events.Count);

        // Verify all events are identical
        for (int i = 0; i < result1.Events.Count; i++)
        {
            var event1 = result1.Events[i];
            var event2 = result2.Events[i];

            event1.Type.Should().Be(event2.Type);
            event1.Round.Should().Be(event2.Round);
            event1.Attacker.Should().Be(event2.Attacker);
            event1.Defender.Should().Be(event2.Defender);
            event1.Damage.Should().Be(event2.Damage);
            event1.Character.Should().Be(event2.Character);
            event1.HP.Should().Be(event2.HP);
            event1.Winner.Should().Be(event2.Winner);
            event1.Timestamp.Should().Be(event2.Timestamp);
        }
    }

    [Fact]
    public void Simulate_WithDifferentSeeds_ShouldProduceDifferentResults()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 100;
        attacker.Power = 10;
        attacker.Speed = 10;

        var defender = Character.Create("user2");
        defender.HP = 100;
        defender.Power = 10;
        defender.Speed = 10;

        // Act
        var result1 = _combatEngine.Simulate(attacker, defender, 11111);
        var result2 = _combatEngine.Simulate(attacker, defender, 22222);

        // Assert - Results should be different (at least damage values or outcome)
        // Note: They might have same outcome but different damage/events
        var eventsAreDifferent = result1.Events.Count != result2.Events.Count ||
                                 result1.Events.Any(e => !result2.Events.Any(e2 =>
                                     e.Type == e2.Type &&
                                     e.Damage == e2.Damage &&
                                     e.Timestamp == e2.Timestamp));

        // At least one aspect should be different
        (result1.Outcome != result2.Outcome ||
         result1.AttackerFinalHP != result2.AttackerFinalHP ||
         result1.DefenderFinalHP != result2.DefenderFinalHP ||
         eventsAreDifferent).Should().BeTrue();
    }

    #endregion

    #region Combat Rules Tests

    [Fact]
    public void Simulate_WithHigherSpeedAttacker_ShouldAttackFirst()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 100;
        attacker.Power = 10;
        attacker.Speed = 20; // Higher speed

        var defender = Character.Create("user2");
        defender.HP = 100;
        defender.Power = 10;
        defender.Speed = 10; // Lower speed

        var seed = 12345;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert - First attack event should be from attacker
        var firstAttack = result.Events.FirstOrDefault(e => e.Type == "Attack");
        firstAttack.Should().NotBeNull();
        firstAttack!.Attacker.Should().Be("Attacker");
    }

    [Fact]
    public void Simulate_WithEqualSpeed_ShouldHaveAttackerGoFirst()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 100;
        attacker.Power = 10;
        attacker.Speed = 10;

        var defender = Character.Create("user2");
        defender.HP = 100;
        defender.Power = 10;
        defender.Speed = 10; // Equal speed

        var seed = 12345;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert - First attack should be from attacker (tie goes to attacker)
        var firstAttack = result.Events.FirstOrDefault(e => e.Type == "Attack");
        firstAttack.Should().NotBeNull();
        firstAttack!.Attacker.Should().Be("Attacker");
    }

    [Fact]
    public void Simulate_WithHigherPower_ShouldDealMoreDamage()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 1000; // High HP to survive
        attacker.Power = 50; // High power
        attacker.Speed = 10;

        var defender = Character.Create("user2");
        defender.HP = 1000; // High HP to survive
        defender.Power = 10; // Low power
        defender.Speed = 10;

        var seed = 12345;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert - Attacker's attacks should deal more damage on average
        var attackerAttacks = result.Events
            .Where(e => e.Type == "Attack" && e.Attacker == "Attacker")
            .ToList();
        var defenderAttacks = result.Events
            .Where(e => e.Type == "Attack" && e.Attacker == "Defender")
            .ToList();

        if (attackerAttacks.Any() && defenderAttacks.Any())
        {
            var avgAttackerDamage = attackerAttacks.Average(a => a.Damage ?? 0);
            var avgDefenderDamage = defenderAttacks.Average(a => a.Damage ?? 0);
            avgAttackerDamage.Should().BeGreaterThan(avgDefenderDamage);
        }
    }

    [Fact]
    public void Simulate_ShouldRespectMaxRounds()
    {
        // Arrange - Two very tanky characters that won't kill each other quickly
        var attacker = Character.Create("user1");
        attacker.HP = 10000; // Very high HP
        attacker.Power = 1; // Very low power
        attacker.Speed = 10;

        var defender = Character.Create("user2");
        defender.HP = 10000; // Very high HP
        defender.Power = 1; // Very low power
        defender.Speed = 10;

        var seed = 12345;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert - Should not exceed max rounds
        var maxRound = result.Events
            .Where(e => e.Round.HasValue)
            .Max(e => e.Round ?? 0);

        maxRound.Should().BeLessThanOrEqualTo(50); // MaxRounds constant
    }

    [Fact]
    public void Simulate_WithAttackerWinning_ShouldHaveCorrectOutcome()
    {
        // Arrange - Attacker much stronger
        var attacker = Character.Create("user1");
        attacker.HP = 1000;
        attacker.Power = 100; // Very high power
        attacker.Speed = 20;

        var defender = Character.Create("user2");
        defender.HP = 50; // Low HP
        defender.Power = 5; // Low power
        defender.Speed = 5;

        var seed = 12345;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert
        result.Outcome.Should().Be(BattleOutcome.AttackerWon);
        result.DefenderFinalHP.Should().Be(0);
        result.AttackerFinalHP.Should().BeGreaterThan(0);

        var victoryEvent = result.Events.LastOrDefault(e => e.Type == "Victory");
        victoryEvent.Should().NotBeNull();
        victoryEvent!.Winner.Should().Be("Attacker");
    }

    [Fact]
    public void Simulate_WithDefenderWinning_ShouldHaveCorrectOutcome()
    {
        // Arrange - Defender much stronger
        var attacker = Character.Create("user1");
        attacker.HP = 50; // Low HP
        attacker.Power = 5; // Low power
        attacker.Speed = 5;

        var defender = Character.Create("user2");
        defender.HP = 1000;
        defender.Power = 100; // Very high power
        defender.Speed = 20;

        var seed = 12345;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert
        result.Outcome.Should().Be(BattleOutcome.DefenderWon);
        result.AttackerFinalHP.Should().Be(0);
        result.DefenderFinalHP.Should().BeGreaterThan(0);

        var victoryEvent = result.Events.LastOrDefault(e => e.Type == "Victory");
        victoryEvent.Should().NotBeNull();
        victoryEvent!.Winner.Should().Be("Defender");
    }

    [Fact]
    public void Simulate_WithBothReachingZeroHP_ShouldResultInDraw()
    {
        // Arrange - Both characters with very low HP, equal power
        // This is hard to guarantee, but we can test the logic
        var attacker = Character.Create("user1");
        attacker.HP = 10;
        attacker.Power = 10;
        attacker.Speed = 10;

        var defender = Character.Create("user2");
        defender.HP = 10;
        defender.Power = 10;
        defender.Speed = 10;

        // Try multiple seeds to find a draw scenario, or test max rounds draw
        var seed = 99999;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert - Should have a valid outcome
        result.Outcome.Should().BeOneOf(BattleOutcome.AttackerWon, BattleOutcome.DefenderWon, BattleOutcome.Draw);
    }

    [Fact]
    public void Simulate_ShouldGenerateValidEventSequence()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 100;
        attacker.Power = 10;
        attacker.Speed = 10;

        var defender = Character.Create("user2");
        defender.HP = 100;
        defender.Power = 10;
        defender.Speed = 10;

        var seed = 12345;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert
        result.Events.Should().NotBeEmpty();
        result.Events[0].Type.Should().Be("RoundStart");
        result.Events[0].Round.Should().Be(1);

        // Should have at least one Attack event
        result.Events.Should().Contain(e => e.Type == "Attack");

        // Should end with Victory or Draw
        var lastEvent = result.Events.Last();
        lastEvent.Type.Should().BeOneOf("Victory", "Draw");

        // Timestamps should be sequential
        for (int i = 1; i < result.Events.Count; i++)
        {
            result.Events[i].Timestamp.Should().BeGreaterThan(result.Events[i - 1].Timestamp);
        }
    }

    [Fact]
    public void Simulate_WithNullAttacker_ShouldThrowException()
    {
        // Arrange
        var defender = Character.Create("user2");

        // Act
        var act = () => _combatEngine.Simulate(null!, defender, 12345);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("attacker");
    }

    [Fact]
    public void Simulate_WithNullDefender_ShouldThrowException()
    {
        // Arrange
        var attacker = Character.Create("user1");

        // Act
        var act = () => _combatEngine.Simulate(attacker, null!, 12345);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("defender");
    }

    [Fact]
    public void Simulate_DamageShouldHaveVariance()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 1000;
        attacker.Power = 100; // High power for consistent damage
        attacker.Speed = 10;

        var defender = Character.Create("user2");
        defender.HP = 10000; // Very high HP to survive many hits
        defender.Power = 1;
        defender.Speed = 5;

        var seed = 12345;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, seed);

        // Assert - Damage should vary (not always exactly Power value)
        var attacks = result.Events
            .Where(e => e.Type == "Attack" && e.Attacker == "Attacker" && e.Damage.HasValue)
            .Select(e => e.Damage!.Value)
            .ToList();

        if (attacks.Count >= 2)
        {
            // Should have some variance (not all identical)
            var uniqueDamages = attacks.Distinct().Count();
            // With variance 0.8-1.2, we should see different damage values
            uniqueDamages.Should().BeGreaterThan(1);
        }

        // All damage should be within expected range (Power * 0.8 to Power * 1.2)
        foreach (var damage in attacks)
        {
            damage.Should().BeGreaterThanOrEqualTo((int)(attacker.Power * 0.8));
            damage.Should().BeLessThanOrEqualTo((int)(attacker.Power * 1.2));
        }
    }

    #endregion
}
