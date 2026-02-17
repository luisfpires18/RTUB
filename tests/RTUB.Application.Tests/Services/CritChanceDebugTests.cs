using FluentAssertions;
using RTUB.Core.Utilities;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Debug tests to verify crit chance behavior
/// </summary>
public class CritChanceDebugTests
{
    [Fact]
    public void Debug_ActualCritRate_With4Point5PercentChance()
    {
        // Arrange
        var critChance = 0.045; // 4.5%
        var crits = 0;
        var total = 100000;
        var rng = new SeededRandom(12345);

        // Act - Simulate exactly what CalculateDamage does
        for (int i = 0; i < total; i++)
        {
            // First call: variance (this is what CalculateDamage does first)
            var variance = rng.Next(0.8, 1.2);
            
            // Second call: crit check
            var roll = rng.NextDouble();
            if (roll < critChance)
            {
                crits++;
            }
        }

        var observedRate = (double)crits / total;

        // Assert - Should be approximately 4.5% (within reasonable tolerance)
        observedRate.Should().BeGreaterThan(0.04, $"Observed rate was {observedRate:P2}");
        observedRate.Should().BeLessThan(0.05, $"Observed rate was {observedRate:P2}");
    }

    [Fact]
    public void Debug_ActualCritRateInBattle_With4Point5PercentChance()
    {
        // Arrange
        var engine = new DeterministicCombatEngine();
        
        var attacker = Character.Create("user1");
        attacker.HP = 500;
        attacker.Power = 50;
        attacker.Speed = 50;
        attacker.CriticalChance = 0.045; // 4.5%

        var defender = Character.Create("user2");
        defender.HP = 5000; // High HP to get many attacks
        defender.Power = 1;
        defender.Speed = 1;
        defender.CriticalChance = 0.0;

        // Act - Run many battles and count crits
        var allDamages = new List<long>();
        for (int seed = 0; seed < 1000; seed++)
        {
            var result = engine.Simulate(attacker, defender, seed);
            var damages = result.Events
                .Where(e => e.Type == "Attack" && e.Attacker == "Attacker" && e.Damage.HasValue)
                .Select(e => e.Damage!.Value);
            allDamages.AddRange(damages);
        }

        // Determine crits vs non-crits based on damage
        // Non-crit: Power * 0.8 to Power * 1.2 = 40 to 60
        // Crit: Power * 0.8 * 2 to Power * 1.2 * 2 = 80 to 120
        var critThreshold = 65; // Anything above this is definitely a crit
        var crits = allDamages.Count(d => d > critThreshold);
        var observedRate = (double)crits / allDamages.Count;

        // Assert
        allDamages.Count.Should().BeGreaterThan(5000, "Need enough samples");
        observedRate.Should().BeGreaterThan(0.03, $"Observed rate was {observedRate:P2}, expected ~4.5%");
        observedRate.Should().BeLessThan(0.06, $"Observed rate was {observedRate:P2}, expected ~4.5%");
    }

    [Fact]
    public void Debug_VerifyTotalCriticalChanceCalculation()
    {
        // Arrange
        var character = Character.Create("user1");
        
        // Test various scenarios
        character.CriticalChance = 0.0; // 0% base (default)
        character.CriticalUpgrades = 9;  // 9 * 0.005 = 0.045 = 4.5%
        
        // Act
        var total = character.TotalCriticalChance;
        
        // Assert - Should be 0% + 4.5% = 4.5%
        total.Should().BeApproximately(0.045, 0.001, 
            $"Expected 0.045 (4.5%), got {total} ({total * 100:F1}%)");
    }
}
