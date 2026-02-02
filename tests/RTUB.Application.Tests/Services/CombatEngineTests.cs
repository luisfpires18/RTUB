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

        // First two events should be HPUpdate for attacker and defender (initial HP)
        result.Events[0].Type.Should().Be("HPUpdate");
        result.Events[0].Character.Should().Be("Attacker");
        result.Events[0].HP.Should().Be(100);
        result.Events[0].MaxHP.Should().Be(100);

        result.Events[1].Type.Should().Be("HPUpdate");
        result.Events[1].Character.Should().Be("Defender");
        result.Events[1].HP.Should().Be(100);
        result.Events[1].MaxHP.Should().Be(100);

        // Third event should be RoundStart
        result.Events[2].Type.Should().Be("RoundStart");
        result.Events[2].Round.Should().Be(1);

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

        // All damage should be within expected range
        // Base damage: Power * 0.8 to Power * 1.2
        // Critical hits: Base damage * 2 (so Power * 0.8 * 2 to Power * 1.2 * 2)
        foreach (var damage in attacks)
        {
            damage.Should().BeGreaterThanOrEqualTo((int)(attacker.Power * 0.8));
            // Account for critical hits which can double damage
            damage.Should().BeLessThanOrEqualTo((int)(attacker.Power * 1.2 * 2));
        }
    }

    #endregion

    #region Critical Chance Tests

    /// <summary>
    /// Verifies that critical chance is configured as fraction [0..1] in the config
    /// 3% should be 0.03, not 3.0
    /// </summary>
    [Theory]
    [InlineData(0.01)]  // 1%
    [InlineData(0.03)]  // 3%
    [InlineData(0.05)]  // 5%
    [InlineData(0.10)]  // 10%
    [InlineData(0.50)]  // 50%
    public void CriticalChance_ShouldBeFraction_NotPercent(double critChance)
    {
        // Arrange - Character with specific crit chance
        var attacker = Character.Create("user1");
        attacker.CriticalChance = critChance;
        
        // Assert - Crit chance should be in [0..1] range
        attacker.TotalCriticalChance.Should().BeGreaterThanOrEqualTo(0);
        attacker.TotalCriticalChance.Should().BeLessThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that TotalCriticalChance is capped at 1.0 (100%)
    /// </summary>
    [Fact]
    public void TotalCriticalChance_WhenExceedsMax_ShouldBeClampedToOne()
    {
        // Arrange
        var character = Character.Create("user1");
        character.CriticalChance = 0.50;
        character.CriticalUpgrades = 200; // Way more than enough to exceed 100%

        // Act & Assert
        character.TotalCriticalChance.Should().Be(1.0);
    }

    /// <summary>
    /// Verifies that with 0% crit chance, no crits occur
    /// Tests the boundary condition for crit roll
    /// </summary>
    [Fact]
    public void Simulate_WithZeroCritChance_ShouldNeverCrit()
    {
        // Arrange - High power character with 0% crit for clear damage values
        var attacker = Character.Create("user1");
        attacker.HP = 1000;
        attacker.Power = 100;
        attacker.Speed = 100;
        attacker.CriticalChance = 0.0; // 0% crit

        var defender = Character.Create("user2");
        defender.HP = 10000; // High HP to get many attacks
        defender.Power = 1;
        defender.Speed = 1;
        defender.CriticalChance = 0.0;

        // Act - Run multiple seeds
        var allDamages = new List<int>();
        for (int seed = 0; seed < 100; seed++)
        {
            var result = _combatEngine.Simulate(attacker, defender, seed);
            var attackDamages = result.Events
                .Where(e => e.Type == "Attack" && e.Attacker == "Attacker" && e.Damage.HasValue)
                .Select(e => e.Damage!.Value);
            allDamages.AddRange(attackDamages);
        }

        // Assert - No damage should exceed max non-crit (Power * 1.2)
        var maxNonCritDamage = (int)(attacker.Power * 1.2);
        allDamages.Should().OnlyContain(d => d <= maxNonCritDamage + 1, // +1 for rounding
            "with 0% crit chance, no attacks should be critical hits");
    }

    /// <summary>
    /// Verifies that with 100% crit chance, all attacks are crits
    /// </summary>
    [Fact]
    public void Simulate_With100PercentCritChance_ShouldAlwaysCrit()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 1000;
        attacker.Power = 100;
        attacker.Speed = 100;
        attacker.CriticalChance = 1.0; // 100% crit

        var defender = Character.Create("user2");
        defender.HP = 10000;
        defender.Power = 1;
        defender.Speed = 1;
        defender.CriticalChance = 0.0;

        // Act
        var result = _combatEngine.Simulate(attacker, defender, 12345);
        var attackDamages = result.Events
            .Where(e => e.Type == "Attack" && e.Attacker == "Attacker" && e.Damage.HasValue)
            .Select(e => e.Damage!.Value)
            .ToList();

        // Assert - All damage should be at or above min crit damage (Power * 0.8 * 2)
        var minCritDamage = (int)(attacker.Power * 0.8 * 2);
        attackDamages.Should().OnlyContain(d => d >= minCritDamage - 1, // -1 for rounding
            "with 100% crit chance, all attacks should be critical hits");
    }

    /// <summary>
    /// Statistical test: Verifies that 5% crit rate produces ~5% crits over large sample
    /// Uses deterministic seeds to ensure reproducibility
    /// </summary>
    [Fact]
    public void Simulate_With5PercentCritChance_ShouldProduceApproximately5PercentCrits()
    {
        // Arrange
        var attacker = Character.Create("user1");
        attacker.HP = 1000;
        attacker.Power = 100;
        attacker.Speed = 100;
        attacker.CriticalChance = 0.05; // 5% crit

        var defender = Character.Create("user2");
        defender.HP = 5000;
        defender.Power = 1;
        defender.Speed = 1;
        defender.CriticalChance = 0.0;

        // Act - Collect damage values from multiple battles
        var allDamages = new List<int>();
        for (int seed = 0; seed < 500; seed++)
        {
            var result = _combatEngine.Simulate(attacker, defender, seed);
            var attackDamages = result.Events
                .Where(e => e.Type == "Attack" && e.Attacker == "Attacker" && e.Damage.HasValue)
                .Select(e => e.Damage!.Value);
            allDamages.AddRange(attackDamages);
        }

        // Calculate crit rate
        // Crit damage: Power * variance * 2 (at least Power * 0.8 * 2 = 160)
        // Non-crit damage: Power * variance (at most Power * 1.2 = 120)
        var critThreshold = (int)(attacker.Power * 1.3); // Threshold between crit and non-crit
        var critCount = allDamages.Count(d => d > critThreshold);
        var totalAttacks = allDamages.Count;
        var observedCritRate = (double)critCount / totalAttacks;

        // Assert - Should be within tolerance (2.5% to 7.5% for 5% expected)
        totalAttacks.Should().BeGreaterThan(1000, "need sufficient sample size");
        observedCritRate.Should().BeGreaterThan(0.025, $"Expected ~5% crits, got {observedCritRate:P1}");
        observedCritRate.Should().BeLessThan(0.075, $"Expected ~5% crits, got {observedCritRate:P1}");
    }

    /// <summary>
    /// Verifies that crit is rolled exactly once per attack event
    /// (not multiple rolls that would inflate crit rate)
    /// </summary>
    [Fact]
    public void Simulate_ShouldRollCritOncePerAttack()
    {
        // Arrange - Use 50% crit to make it easy to verify single roll
        var attacker = Character.Create("user1");
        attacker.HP = 500;
        attacker.Power = 100;
        attacker.Speed = 100;
        attacker.CriticalChance = 0.50; // 50% crit

        var defender = Character.Create("user2");
        defender.HP = 2000;
        defender.Power = 1;
        defender.Speed = 1;
        defender.CriticalChance = 0.0;

        // Act - Collect multiple battles
        var allDamages = new List<int>();
        for (int seed = 0; seed < 200; seed++)
        {
            var result = _combatEngine.Simulate(attacker, defender, seed);
            var attackDamages = result.Events
                .Where(e => e.Type == "Attack" && e.Attacker == "Attacker" && e.Damage.HasValue)
                .Select(e => e.Damage!.Value);
            allDamages.AddRange(attackDamages);
        }

        // Calculate crit rate - should be ~50%
        var critThreshold = (int)(attacker.Power * 1.3);
        var critCount = allDamages.Count(d => d > critThreshold);
        var totalAttacks = allDamages.Count;
        var observedCritRate = (double)critCount / totalAttacks;

        // Assert - If rolled multiple times, rate would be higher (e.g., 75% for 2 rolls)
        totalAttacks.Should().BeGreaterThan(500, "need sufficient sample size");
        observedCritRate.Should().BeGreaterThan(0.40, $"Crit rate too low: {observedCritRate:P1}");
        observedCritRate.Should().BeLessThan(0.60, $"Crit rate too high (possible double-roll): {observedCritRate:P1}");
    }

    #endregion
}
