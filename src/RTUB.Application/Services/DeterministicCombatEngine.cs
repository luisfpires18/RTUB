using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Configuration;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Deterministic combat engine implementation
/// Simulates time-based battles using seeded RNG for reproducibility
/// Characters attack when their action timer reaches 0, based on their ActionTime stat
/// Defense reduces incoming damage using diminishing returns formula
/// </summary>
public class DeterministicCombatEngine : ICombatEngine
{
    private const double MaxBattleTime = 300000; // 5 minutes max battle time in ms

    /// <summary>
    /// Simulates a battle between two characters using time-based combat
    /// </summary>
    public CombatResult Simulate(Character attacker, Character defender, int seed)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);

        var rng = new SeededRandom(seed);
        var events = new List<CombatEvent>();
        var eventIndex = 0;

        // Initialize HP - use CurrentHP if available (persistent HP system), otherwise use TotalHP
        var attackerHP = attacker.CurrentHP ?? attacker.TotalHP;
        var defenderHP = defender.CurrentHP ?? defender.TotalHP;

        // Initialize consumable buff flags
        var hasCigarroDodge = attacker.CigarroShieldHitsRemaining > 0;
        var hasPenaltyLifesteal = attacker.HasPenaltyBuff;

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
                var (damage, isCritical) = CombatMath.CalculateDamage(attacker.TotalPower, attacker.TotalCriticalChance, defender.TotalDefense, rng);

                defenderHP = Math.Max(0, defenderHP - damage);

                // Penalty lifesteal: heal attacker for 0.5% of max HP per hit
                if (hasPenaltyLifesteal && attackerHP > 0)
                {
                    var healAmount = (long)Math.Max(1, Math.Round(attacker.TotalHP * MyTunoScaling.PenaltyLifestealPercent));
                    attackerHP = Math.Min(attacker.TotalHP, attackerHP + healAmount);
                }

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
                var isDodged = false;
                var (damage, isCritical) = CombatMath.CalculateDamage(defender.TotalPower, defender.TotalCriticalChance, attacker.TotalDefense, rng);

                // Cigarro dodge — 10% chance to dodge incoming attack
                if (hasCigarroDodge && rng.NextDouble() < MyTunoScaling.CigarroDodgeChance)
                {
                    damage = 0;
                    isDodged = true;
                }

                attackerHP = Math.Max(0, attackerHP - damage);

                events.Add(new CombatEvent
                {
                    Type = "Attack",
                    Attacker = "Defender",
                    Defender = "Attacker",
                    Damage = damage,
                    IsCritical = isCritical,
                    IsDodged = isDodged ? true : null,
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
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(enemies);
        if (enemies.Count == 0)
            throw new ArgumentException("Must have at least one enemy", nameof(enemies));

        var rng = new SeededRandom(seed);
        var events = new List<CombatEvent>();
        var eventIndex = 0;

        // Initialize player HP and action time
        var playerHP = player.CurrentHP ?? player.TotalHP;
        var playerMaxHP = player.TotalHP;
        var playerActionTimeMs = player.ActionTime * 1000;
        var playerTimer = playerActionTimeMs;

        // Initialize consumable buff flags
        var hasCigarroDodge = player.CigarroShieldHitsRemaining > 0;
        var hasCanhaoBuff = player.HasCanhaoBuff;
        var hasPenaltyLifesteal = player.HasPenaltyBuff;

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
            if (aliveEnemies.Count == 0)
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
                // Canhão AOE: attack ALL alive enemies; otherwise attack current target only
                var targets = hasCanhaoBuff
                    ? enemyStates.Where(e => e.HP > 0).ToList()
                    : new List<EnemyState> { enemyStates[currentTargetIndex] };

                foreach (var target in targets)
                {
                    if (target.HP <= 0) continue;

                    var (damage, isCritical) = CombatMath.CalculateDamage(player.TotalPower, player.TotalCriticalChance, target.Enemy.TotalDefense, rng);

                    target.HP = Math.Max(0, target.HP - damage);

                    events.Add(new CombatEvent
                    {
                        Type = "Attack",
                        Attacker = "Player",
                        Defender = $"Enemy{target.Index}",
                        Damage = damage,
                        IsCritical = isCritical,
                        IsAoe = hasCanhaoBuff ? true : null,
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

                // Penalty lifesteal: heal player for 0.5% of max HP per attack action
                if (hasPenaltyLifesteal && playerHP > 0)
                {
                    var healAmount = (long)Math.Max(1, Math.Round(playerMaxHP * MyTunoScaling.PenaltyLifestealPercent));
                    playerHP = Math.Min(playerMaxHP, playerHP + healAmount);
                }

                // Reset player timer
                playerTimer = playerActionTimeMs;
            }

            // Process enemy actions for all enemies whose timer reached 0
            foreach (var enemy in aliveEnemies.Where(e => e.Timer <= 0 && e.HP > 0))
            {
                if (playerHP <= 0) break;

                var isDodged = false;
                var (damage, isCritical) = CombatMath.CalculateDamage(enemy.Enemy.TotalPower, enemy.Enemy.TotalCriticalChance, player.TotalDefense, rng);

                // Cigarro dodge — 10% chance to dodge incoming attack
                if (hasCigarroDodge && rng.NextDouble() < MyTunoScaling.CigarroDodgeChance)
                {
                    damage = 0;
                    isDodged = true;
                }

                playerHP = Math.Max(0, playerHP - damage);

                events.Add(new CombatEvent
                {
                    Type = "Attack",
                    Attacker = $"Enemy{enemy.Index}",
                    Defender = "Player",
                    Damage = damage,
                    IsCritical = isCritical,
                    IsDodged = isDodged ? true : null,
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
        // When time runs out, player wins if they have more HP than all enemies combined.
        // Otherwise it's a defeat (enemies won by outlasting the player).
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
                // Player didn't outdamage the enemies — treat as defeat
                playerHP = 0;
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

    // EnemyState class moved to EnemyState.cs

    /// <summary>
    /// Determines the battle outcome based on final HP values
    /// </summary>
    private static BattleOutcome DetermineOutcome(long attackerHP, long defenderHP)
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
