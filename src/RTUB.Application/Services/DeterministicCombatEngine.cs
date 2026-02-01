using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Deterministic combat engine implementation
/// Simulates turn-based battles using seeded RNG for reproducibility
/// </summary>
public class DeterministicCombatEngine : ICombatEngine
{
    private const int MaxRounds = 50; // Maximum rounds to prevent infinite fights
    private const double DamageVarianceMin = 0.8;
    private const double DamageVarianceMax = 1.2;

    /// <summary>
    /// Simulates a battle between two characters
    /// </summary>
    public CombatResult Simulate(Character attacker, Character defender, int seed)
    {
        if (attacker == null)
            throw new ArgumentNullException(nameof(attacker));
        if (defender == null)
            throw new ArgumentNullException(nameof(defender));

        var rng = new SeededRandom(seed);
        var events = new List<CombatEvent>();
        var timestamp = 0;

        // Initialize HP - use CurrentHP if available (persistent HP system), otherwise use TotalHP
        var attackerHP = attacker.CurrentHP ?? attacker.TotalHP;
        var defenderHP = defender.CurrentHP ?? defender.TotalHP;

        // Determine initial turn order (higher Speed attacks first)
        var attackerSpeed = attacker.TotalSpeed;
        var defenderSpeed = defender.TotalSpeed;
        var attackerGoesFirst = attackerSpeed >= defenderSpeed;

        // Emit initial HP values for both characters
        events.Add(new CombatEvent
        {
            Type = "HPUpdate",
            Character = "Attacker",
            HP = attackerHP,
            Timestamp = timestamp++
        });

        events.Add(new CombatEvent
        {
            Type = "HPUpdate",
            Character = "Defender",
            HP = defenderHP,
            Timestamp = timestamp++
        });

        // Emit initial round start
        events.Add(new CombatEvent
        {
            Type = "RoundStart",
            Round = 1,
            Timestamp = timestamp++
        });

        // Battle loop
        for (int round = 1; round <= MaxRounds; round++)
        {
            // Determine turn order for this round based on Speed
            // Higher Speed character gets more turns (simplified: if Speed difference is significant, faster gets 2 turns)
            var speedDifference = Math.Abs(attackerSpeed - defenderSpeed);
            var fasterCharacter = attackerSpeed >= defenderSpeed ? "Attacker" : "Defender";
            var slowerCharacter = fasterCharacter == "Attacker" ? "Defender" : "Attacker";

            // Process turns in this round
            // Simplified: Each character gets one turn per round, order determined by Speed
            if (attackerGoesFirst)
            {
                // Attacker's turn
                if (attackerHP > 0 && defenderHP > 0)
                {
                    var damage = CalculateDamage(attacker.TotalPower, attacker.TotalCriticalChance, rng);
                    defenderHP = Math.Max(0, defenderHP - damage);

                    events.Add(new CombatEvent
                    {
                        Type = "Attack",
                        Attacker = "Attacker",
                        Defender = "Defender",
                        Damage = damage,
                        Timestamp = timestamp++
                    });

                    events.Add(new CombatEvent
                    {
                        Type = "HPUpdate",
                        Character = "Defender",
                        HP = defenderHP,
                        Timestamp = timestamp++
                    });

                    if (defenderHP <= 0)
                    {
                        events.Add(new CombatEvent
                        {
                            Type = "KO",
                            Character = "Defender",
                            Timestamp = timestamp++
                        });

                        events.Add(new CombatEvent
                        {
                            Type = "Victory",
                            Winner = "Attacker",
                            Timestamp = timestamp++
                        });

                        break;
                    }
                }

                // Defender's turn
                if (attackerHP > 0 && defenderHP > 0)
                {
                    var damage = CalculateDamage(defender.TotalPower, defender.TotalCriticalChance, rng);
                    attackerHP = Math.Max(0, attackerHP - damage);

                    events.Add(new CombatEvent
                    {
                        Type = "Attack",
                        Attacker = "Defender",
                        Defender = "Attacker",
                        Damage = damage,
                        Timestamp = timestamp++
                    });

                    events.Add(new CombatEvent
                    {
                        Type = "HPUpdate",
                        Character = "Attacker",
                        HP = attackerHP,
                        Timestamp = timestamp++
                    });

                    if (attackerHP <= 0)
                    {
                        events.Add(new CombatEvent
                        {
                            Type = "KO",
                            Character = "Attacker",
                            Timestamp = timestamp++
                        });

                        events.Add(new CombatEvent
                        {
                            Type = "Victory",
                            Winner = "Defender",
                            Timestamp = timestamp++
                        });

                        break;
                    }
                }
            }
            else
            {
                // Defender goes first
                if (attackerHP > 0 && defenderHP > 0)
                {
                    var damage = CalculateDamage(defender.TotalPower, defender.TotalCriticalChance, rng);
                    attackerHP = Math.Max(0, attackerHP - damage);

                    events.Add(new CombatEvent
                    {
                        Type = "Attack",
                        Attacker = "Defender",
                        Defender = "Attacker",
                        Damage = damage,
                        Timestamp = timestamp++
                    });

                    events.Add(new CombatEvent
                    {
                        Type = "HPUpdate",
                        Character = "Attacker",
                        HP = attackerHP,
                        Timestamp = timestamp++
                    });

                    if (attackerHP <= 0)
                    {
                        events.Add(new CombatEvent
                        {
                            Type = "KO",
                            Character = "Attacker",
                            Timestamp = timestamp++
                        });

                        events.Add(new CombatEvent
                        {
                            Type = "Victory",
                            Winner = "Defender",
                            Timestamp = timestamp++
                        });

                        break;
                    }
                }

                // Attacker's turn
                if (attackerHP > 0 && defenderHP > 0)
                {
                    var damage = CalculateDamage(attacker.TotalPower, attacker.TotalCriticalChance, rng);
                    defenderHP = Math.Max(0, defenderHP - damage);

                    events.Add(new CombatEvent
                    {
                        Type = "Attack",
                        Attacker = "Attacker",
                        Defender = "Defender",
                        Damage = damage,
                        Timestamp = timestamp++
                    });

                    events.Add(new CombatEvent
                    {
                        Type = "HPUpdate",
                        Character = "Defender",
                        HP = defenderHP,
                        Timestamp = timestamp++
                    });

                    if (defenderHP <= 0)
                    {
                        events.Add(new CombatEvent
                        {
                            Type = "KO",
                            Character = "Defender",
                            Timestamp = timestamp++
                        });

                        events.Add(new CombatEvent
                        {
                            Type = "Victory",
                            Winner = "Attacker",
                            Timestamp = timestamp++
                        });

                        break;
                    }
                }
            }

            // End of round
            events.Add(new CombatEvent
            {
                Type = "RoundEnd",
                Round = round,
                Timestamp = timestamp++
            });

            // If both characters are still alive and we've reached max rounds, determine winner by HP
            if (round == MaxRounds && attackerHP > 0 && defenderHP > 0)
            {
                if (attackerHP > defenderHP)
                {
                    events.Add(new CombatEvent
                    {
                        Type = "Victory",
                        Winner = "Attacker",
                        Timestamp = timestamp++
                    });
                }
                else if (defenderHP > attackerHP)
                {
                    events.Add(new CombatEvent
                    {
                        Type = "Victory",
                        Winner = "Defender",
                        Timestamp = timestamp++
                    });
                }
                else
                {
                    // Draw - equal HP
                    events.Add(new CombatEvent
                    {
                        Type = "Draw",
                        Timestamp = timestamp++
                    });
                }
                break;
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
    /// Calculates damage with variance
    /// Formula: BaseDamage = Power, FinalDamage = Power * Random(0.8, 1.2)
    /// </summary>
    private static int CalculateDamage(int power, double criticalChance, SeededRandom rng)
    {
        var variance = rng.Next(DamageVarianceMin, DamageVarianceMax);
        var damage = power * variance;
        if (rng.NextDouble() < criticalChance)
        {
            damage *= 2;
        }
        return (int)Math.Round(damage, MidpointRounding.AwayFromZero);
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
