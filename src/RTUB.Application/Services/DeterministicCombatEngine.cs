using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Deterministic combat engine implementation
/// Simulates time-based battles using seeded RNG for reproducibility
/// Characters attack when their action timer reaches 0, based on their ActionTime stat
/// </summary>
public class DeterministicCombatEngine : ICombatEngine
{
    private const double MaxBattleTime = 300000; // 5 minutes max battle time in ms
    private const double DamageVarianceMin = 0.8;
    private const double DamageVarianceMax = 1.2;
    private const double TimeStepMs = 10; // Simulation time step in milliseconds

    /// <summary>
    /// Simulates a battle between two characters using time-based combat
    /// </summary>
    public CombatResult Simulate(Character attacker, Character defender, int seed)
    {
        if (attacker == null)
            throw new ArgumentNullException(nameof(attacker));
        if (defender == null)
            throw new ArgumentNullException(nameof(defender));

        var rng = new SeededRandom(seed);
        var events = new List<CombatEvent>();
        var eventIndex = 0;

        // Initialize HP - use CurrentHP if available (persistent HP system), otherwise use TotalHP
        var attackerHP = attacker.CurrentHP ?? attacker.TotalHP;
        var defenderHP = defender.CurrentHP ?? defender.TotalHP;

        // Get action times (in seconds, convert to ms)
        var attackerActionTimeMs = attacker.ActionTime * 1000;
        var defenderActionTimeMs = defender.ActionTime * 1000;

        // Initialize action timers (start filled, drain to 0)
        var attackerTimer = attackerActionTimeMs;
        var defenderTimer = defenderActionTimeMs;

        // Emit initial HP values for both characters
        events.Add(new CombatEvent
        {
            Type = "HPUpdate",
            Character = "Attacker",
            HP = attackerHP,
            MaxHP = attacker.TotalHP,
            ActionTime = attacker.ActionTime,
            SimTime = 0,
            Timestamp = eventIndex++
        });

        events.Add(new CombatEvent
        {
            Type = "HPUpdate",
            Character = "Defender",
            HP = defenderHP,
            MaxHP = defender.TotalHP,
            ActionTime = defender.ActionTime,
            SimTime = 0,
            Timestamp = eventIndex++
        });

        // Emit battle start
        events.Add(new CombatEvent
        {
            Type = "BattleStart",
            SimTime = 0,
            Timestamp = eventIndex++
        });

        // Battle loop - time-based simulation
        double currentTime = 0;

        while (currentTime < MaxBattleTime && attackerHP > 0 && defenderHP > 0)
        {
            // Calculate time until next action
            var timeToAttackerAction = attackerTimer;
            var timeToDefenderAction = defenderTimer;
            var timeStep = Math.Min(timeToAttackerAction, timeToDefenderAction);

            // Advance time
            currentTime += timeStep;
            attackerTimer -= timeStep;
            defenderTimer -= timeStep;

            // Process attacker action if timer reached 0
            if (attackerTimer <= 0 && attackerHP > 0 && defenderHP > 0)
            {
                var (damage, isCritical) = CalculateDamage(attacker.TotalPower, attacker.TotalCriticalChance, rng);
                defenderHP = Math.Max(0, defenderHP - damage);

                events.Add(new CombatEvent
                {
                    Type = "Attack",
                    Attacker = "Attacker",
                    Defender = "Defender",
                    Damage = damage,
                    IsCritical = isCritical,
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });

                events.Add(new CombatEvent
                {
                    Type = "HPUpdate",
                    Character = "Defender",
                    HP = defenderHP,
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });

                if (defenderHP <= 0)
                {
                    events.Add(new CombatEvent
                    {
                        Type = "KO",
                        Character = "Defender",
                        SimTime = currentTime,
                        Timestamp = eventIndex++
                    });

                    events.Add(new CombatEvent
                    {
                        Type = "Victory",
                        Winner = "Attacker",
                        SimTime = currentTime,
                        Timestamp = eventIndex++
                    });
                    break;
                }

                // Reset attacker timer
                attackerTimer = attackerActionTimeMs;
            }

            // Process defender action if timer reached 0
            if (defenderTimer <= 0 && attackerHP > 0 && defenderHP > 0)
            {
                var (damage, isCritical) = CalculateDamage(defender.TotalPower, defender.TotalCriticalChance, rng);
                attackerHP = Math.Max(0, attackerHP - damage);

                events.Add(new CombatEvent
                {
                    Type = "Attack",
                    Attacker = "Defender",
                    Defender = "Attacker",
                    Damage = damage,
                    IsCritical = isCritical,
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });

                events.Add(new CombatEvent
                {
                    Type = "HPUpdate",
                    Character = "Attacker",
                    HP = attackerHP,
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });

                if (attackerHP <= 0)
                {
                    events.Add(new CombatEvent
                    {
                        Type = "KO",
                        Character = "Attacker",
                        SimTime = currentTime,
                        Timestamp = eventIndex++
                    });

                    events.Add(new CombatEvent
                    {
                        Type = "Victory",
                        Winner = "Defender",
                        SimTime = currentTime,
                        Timestamp = eventIndex++
                    });
                    break;
                }

                // Reset defender timer
                defenderTimer = defenderActionTimeMs;
            }
        }

        // Handle timeout - determine winner by HP
        if (currentTime >= MaxBattleTime && attackerHP > 0 && defenderHP > 0)
        {
            if (attackerHP > defenderHP)
            {
                events.Add(new CombatEvent
                {
                    Type = "Victory",
                    Winner = "Attacker",
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });
            }
            else if (defenderHP > attackerHP)
            {
                events.Add(new CombatEvent
                {
                    Type = "Victory",
                    Winner = "Defender",
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });
            }
            else
            {
                events.Add(new CombatEvent
                {
                    Type = "Draw",
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });
            }
        }

        // Determine outcome
        var outcome = DetermineOutcome(attackerHP, defenderHP);

        return new CombatResult
        {
            Outcome = outcome,
            Events = events,
            AttackerFinalHP = attackerHP,
            DefenderFinalHP = defenderHP
        };
    }

    /// <summary>
    /// Simulates a battle between one player and multiple enemies using time-based combat
    /// Player focuses one enemy at a time until defeated
    /// All characters have independent action timers
    /// </summary>
    public CombatResult SimulateMultiEnemy(Character player, List<Character> enemies, int seed)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));
        if (enemies == null || !enemies.Any())
            throw new ArgumentException("Must have at least one enemy", nameof(enemies));

        var rng = new SeededRandom(seed);
        var events = new List<CombatEvent>();
        var eventIndex = 0;

        // Initialize player HP and action time
        var playerHP = player.CurrentHP ?? player.TotalHP;
        var playerMaxHP = player.TotalHP;
        var playerActionTimeMs = player.ActionTime * 1000;
        var playerTimer = playerActionTimeMs;

        // Initialize all enemy states with HP and action timers
        var enemyStates = enemies.Select((enemy, index) => new EnemyState
        {
            Enemy = enemy,
            Index = index,
            HP = enemy.CurrentHP ?? enemy.TotalHP,
            MaxHP = enemy.TotalHP,
            Name = enemy.User?.UserName ?? $"Enemy {index + 1}",
            ActionTimeMs = enemy.ActionTime * 1000,
            Timer = enemy.ActionTime * 1000
        }).ToList();

        // Emit initial HP and action time for player
        events.Add(new CombatEvent
        {
            Type = "HPUpdate",
            Character = "Player",
            HP = playerHP,
            MaxHP = playerMaxHP,
            ActionTime = player.ActionTime,
            SimTime = 0,
            Timestamp = eventIndex++
        });

        // Emit initial HP and action time for all enemies
        foreach (var enemyState in enemyStates)
        {
            events.Add(new CombatEvent
            {
                Type = "HPUpdate",
                Character = $"Enemy{enemyState.Index}",
                HP = enemyState.HP,
                MaxHP = enemyState.MaxHP,
                ActionTime = enemyState.Enemy.ActionTime,
                SimTime = 0,
                Timestamp = eventIndex++
            });
        }

        // Emit battle start
        events.Add(new CombatEvent
        {
            Type = "BattleStart",
            SimTime = 0,
            Timestamp = eventIndex++
        });

        // Track current target (player focuses one enemy at a time)
        int currentTargetIndex = 0;

        // Battle loop - time-based simulation
        double currentTime = 0;

        while (currentTime < MaxBattleTime && playerHP > 0)
        {
            // Check if any enemies are alive
            var aliveEnemies = enemyStates.Where(e => e.HP > 0).ToList();
            if (!aliveEnemies.Any())
            {
                events.Add(new CombatEvent
                {
                    Type = "Victory",
                    Winner = "Player",
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });
                break;
            }

            // Update current target if it's defeated
            while (currentTargetIndex < enemyStates.Count && enemyStates[currentTargetIndex].HP <= 0)
            {
                currentTargetIndex++;
            }

            if (currentTargetIndex >= enemyStates.Count)
            {
                events.Add(new CombatEvent
                {
                    Type = "Victory",
                    Winner = "Player",
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });
                break;
            }

            // Find the minimum time until next action
            var timesToAction = new List<double> { playerTimer };
            timesToAction.AddRange(aliveEnemies.Select(e => e.Timer));
            var timeStep = timesToAction.Min();

            // Advance time
            currentTime += timeStep;
            playerTimer -= timeStep;
            foreach (var enemy in aliveEnemies)
            {
                enemy.Timer -= timeStep;
            }

            // Process player action if timer reached 0
            if (playerTimer <= 0 && playerHP > 0 && currentTargetIndex < enemyStates.Count)
            {
                var target = enemyStates[currentTargetIndex];
                if (target.HP > 0)
                {
                    var (damage, isCritical) = CalculateDamage(player.TotalPower, player.TotalCriticalChance, rng);
                    target.HP = Math.Max(0, target.HP - damage);

                    events.Add(new CombatEvent
                    {
                        Type = "Attack",
                        Attacker = "Player",
                        Defender = $"Enemy{target.Index}",
                        Damage = damage,
                        IsCritical = isCritical,
                        SimTime = currentTime,
                        Timestamp = eventIndex++
                    });

                    events.Add(new CombatEvent
                    {
                        Type = "HPUpdate",
                        Character = $"Enemy{target.Index}",
                        HP = target.HP,
                        SimTime = currentTime,
                        Timestamp = eventIndex++
                    });

                    if (target.HP <= 0)
                    {
                        events.Add(new CombatEvent
                        {
                            Type = "KO",
                            Character = $"Enemy{target.Index}",
                            SimTime = currentTime,
                            Timestamp = eventIndex++
                        });
                    }
                }

                // Reset player timer
                playerTimer = playerActionTimeMs;
            }

            // Process enemy actions for all enemies whose timer reached 0
            foreach (var enemy in aliveEnemies.Where(e => e.Timer <= 0 && e.HP > 0))
            {
                if (playerHP <= 0) break;

                var (damage, isCritical) = CalculateDamage(enemy.Enemy.TotalPower, enemy.Enemy.TotalCriticalChance, rng);
                playerHP = Math.Max(0, playerHP - damage);

                events.Add(new CombatEvent
                {
                    Type = "Attack",
                    Attacker = $"Enemy{enemy.Index}",
                    Defender = "Player",
                    Damage = damage,
                    IsCritical = isCritical,
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });

                events.Add(new CombatEvent
                {
                    Type = "HPUpdate",
                    Character = "Player",
                    HP = playerHP,
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });

                if (playerHP <= 0)
                {
                    events.Add(new CombatEvent
                    {
                        Type = "KO",
                        Character = "Player",
                        SimTime = currentTime,
                        Timestamp = eventIndex++
                    });

                    events.Add(new CombatEvent
                    {
                        Type = "Victory",
                        Winner = "Enemies",
                        SimTime = currentTime,
                        Timestamp = eventIndex++
                    });
                    break;
                }

                // Reset enemy timer
                enemy.Timer = enemy.ActionTimeMs;
            }
        }

        // Handle timeout - determine winner by HP
        if (currentTime >= MaxBattleTime && playerHP > 0 && enemyStates.Any(e => e.HP > 0))
        {
            var totalEnemyHP = enemyStates.Sum(e => e.HP);
            
            if (playerHP > totalEnemyHP)
            {
                events.Add(new CombatEvent
                {
                    Type = "Victory",
                    Winner = "Player",
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });
            }
            else
            {
                events.Add(new CombatEvent
                {
                    Type = "Victory",
                    Winner = "Enemies",
                    SimTime = currentTime,
                    Timestamp = eventIndex++
                });
            }
        }

        // Determine outcome
        var outcome = playerHP > 0 ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon;

        return new CombatResult
        {
            Outcome = outcome,
            Events = events,
            AttackerFinalHP = playerHP,
            DefenderFinalHP = enemyStates.Sum(e => e.HP)
        };
    }

    /// <summary>
    /// Helper class to track enemy state during multi-enemy combat
    /// </summary>
    private class EnemyState
    {
        public Character Enemy { get; set; } = null!;
        public int Index { get; set; }
        public int HP { get; set; }
        public int MaxHP { get; set; }
        public string Name { get; set; } = string.Empty;
        public double ActionTimeMs { get; set; }
        public double Timer { get; set; }
    }

    /// <summary>
    /// Calculates damage with variance
    /// Formula: BaseDamage = Power, FinalDamage = Power * Random(0.8, 1.2)
    /// </summary>
    private static (int damage, bool isCritical) CalculateDamage(int power, double criticalChance, SeededRandom rng)
    {
        var variance = rng.Next(DamageVarianceMin, DamageVarianceMax);
        var damage = power * variance;
        var isCritical = rng.NextDouble() < criticalChance;
        if (isCritical)
        {
            damage *= 2;
        }
        return ((int)Math.Round(damage, MidpointRounding.AwayFromZero), isCritical);
    }

    /// <summary>
    /// Determines the battle outcome based on final HP values
    /// </summary>
    private static BattleOutcome DetermineOutcome(int attackerHP, int defenderHP)
    {
        if (attackerHP > 0 && defenderHP <= 0)
            return BattleOutcome.AttackerWon;
        if (defenderHP > 0 && attackerHP <= 0)
            return BattleOutcome.DefenderWon;
        if (attackerHP == defenderHP)
            return BattleOutcome.Draw;

        // If both are alive (shouldn't happen, but handle it)
        return attackerHP > defenderHP ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon;
    }
}
