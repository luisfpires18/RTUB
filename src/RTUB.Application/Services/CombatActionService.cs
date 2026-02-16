using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Configuration;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Processes individual combat actions during interactive (real-time) battle.
/// Blade Crafter model: auto-attacks fire automatically, spells are manual clicks.
///
/// All damage calculations reuse the same formulas as DeterministicCombatEngine:
///   rawDamage = power × variance(0.8–1.2) × crit(2x)
///   finalDamage = max(MinDamage, floor(rawDamage × K/(K+defense)))
/// </summary>
public class CombatActionService : ICombatActionService
{
    private const double DamageVarianceMin = 0.8;
    private const double DamageVarianceMax = 1.2;
    private const double CanhaoDamageMultiplier = 1.30; // +30% damage

    /// <inheritdoc />
    public CombatSession CreateSession(
        Character player,
        List<Character> enemies,
        int seed,
        List<SpecialAttack>? equippedSpells = null,
        string mode = "stage")
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(enemies);
        if (enemies.Count == 0)
            throw new ArgumentException("Must have at least one enemy", nameof(enemies));

        var session = new CombatSession
        {
            Seed = seed,
            Rng = new SeededRandom(seed),
            Mode = mode,
            Player = new CombatantState
            {
                Identifier = mode == "arena" ? "Attacker" : "Player",
                Name = player.User?.UserName ?? "Player",
                CurrentHP = Math.Min(player.CurrentHP ?? player.TotalHP, player.TotalHP),
                MaxHP = player.TotalHP,
                Power = player.TotalPower,
                Defense = player.TotalDefense,
                CriticalChance = player.TotalCriticalChance,
                ActionTimeSeconds = player.ActionTime
            },
            Enemies = enemies.Select((e, i) => new CombatantState
            {
                Identifier = mode == "arena" ? "Defender" : $"Enemy{i}",
                Name = e.User?.UserName ?? $"Enemy {i + 1}",
                CurrentHP = e.CurrentHP ?? e.TotalHP,
                MaxHP = e.TotalHP,
                Power = e.TotalPower,
                Defense = e.TotalDefense,
                CriticalChance = e.TotalCriticalChance,
                ActionTimeSeconds = e.ActionTime
            }).ToList(),
            CurrentTargetIndex = 0,
            CigarroShieldRemaining = player.CigarroShieldHitsRemaining,
            CanhaoBoostRemaining = player.CanhaoDamageBoostHitsRemaining,
            HasShotBuff = player.ShotBuffBattlesRemaining > 0,
            HasPenaltyBuff = player.PenaltyBuffActive > 0,
            HeavyAttackDamageBonus = player.HeavyAttackDamageBonus,
            SpecialAttackDamageBonus = player.SpecialAttackDamageBonus,
            EquippedSpells = equippedSpells ?? new List<SpecialAttack>(),
            BattleStartedAt = DateTime.UtcNow,
            LastPlayerActionAt = DateTime.UtcNow,
            LastEnemyActionAt = new DateTime[enemies.Count],
            EventTimestamp = 0
        };

        // Initialize spell cooldowns (all ready at battle start)
        foreach (var spell in session.EquippedSpells)
        {
            session.SpellCooldowns[spell.AttackId] = 0;
        }

        // Initialize enemy action timestamps
        for (var i = 0; i < enemies.Count; i++)
        {
            session.LastEnemyActionAt[i] = DateTime.UtcNow;
        }

        // Emit initial HP/ActionTime events for player
        session.RecordedEvents.Add(new CombatEvent
        {
            Type = "HPUpdate",
            Character = session.Player.Identifier,
            HP = session.Player.CurrentHP,
            MaxHP = session.Player.MaxHP,
            ActionTime = session.Player.ActionTimeSeconds,
            SimTime = 0,
            Timestamp = session.EventTimestamp++
        });

        // Emit initial HP/ActionTime events for all enemies
        foreach (var enemy in session.Enemies)
        {
            session.RecordedEvents.Add(new CombatEvent
            {
                Type = "HPUpdate",
                Character = enemy.Identifier,
                HP = enemy.CurrentHP,
                MaxHP = enemy.MaxHP,
                ActionTime = enemy.ActionTimeSeconds,
                SimTime = 0,
                Timestamp = session.EventTimestamp++
            });
        }

        // Emit BattleStart
        session.RecordedEvents.Add(new CombatEvent
        {
            Type = "BattleStart",
            SimTime = 0,
            Timestamp = session.EventTimestamp++
        });

        return session;
    }

    /// <inheritdoc />
    public CombatSession CreateSession(
        Character player,
        List<CombatantState> enemyStates,
        int seed,
        List<SpecialAttack>? equippedSpells = null,
        string mode = "stage")
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(enemyStates);
        if (enemyStates.Count == 0)
            throw new ArgumentException("Must have at least one enemy", nameof(enemyStates));

        var session = new CombatSession
        {
            Seed = seed,
            Rng = new SeededRandom(seed),
            Mode = mode,
            Player = new CombatantState
            {
                Identifier = mode == "arena" ? "Attacker" : "Player",
                Name = player.User?.UserName ?? "Player",
                CurrentHP = Math.Min(player.CurrentHP ?? player.TotalHP, player.TotalHP),
                MaxHP = player.TotalHP,
                Power = player.TotalPower,
                Defense = player.TotalDefense,
                CriticalChance = player.TotalCriticalChance,
                ActionTimeSeconds = player.ActionTime
            },
            Enemies = enemyStates.Select((e, i) =>
            {
                // Assign identifiers if not already set
                if (string.IsNullOrEmpty(e.Identifier))
                    e.Identifier = mode == "arena" ? "Defender" : $"Enemy{i}";
                return e;
            }).ToList(),
            CurrentTargetIndex = 0,
            CigarroShieldRemaining = player.CigarroShieldHitsRemaining,
            CanhaoBoostRemaining = player.CanhaoDamageBoostHitsRemaining,
            HasShotBuff = player.ShotBuffBattlesRemaining > 0,
            HasPenaltyBuff = player.PenaltyBuffActive > 0,
            HeavyAttackDamageBonus = player.HeavyAttackDamageBonus,
            SpecialAttackDamageBonus = player.SpecialAttackDamageBonus,
            EquippedSpells = equippedSpells ?? new List<SpecialAttack>(),
            BattleStartedAt = DateTime.UtcNow,
            LastPlayerActionAt = DateTime.UtcNow,
            LastEnemyActionAt = new DateTime[enemyStates.Count],
            EventTimestamp = 0
        };

        // Initialize spell cooldowns (all ready at battle start)
        foreach (var spell in session.EquippedSpells)
        {
            session.SpellCooldowns[spell.AttackId] = 0;
        }

        // Initialize enemy action timestamps
        for (var i = 0; i < enemyStates.Count; i++)
        {
            session.LastEnemyActionAt[i] = DateTime.UtcNow;
        }

        // Emit initial HP/ActionTime events for player
        session.RecordedEvents.Add(new CombatEvent
        {
            Type = "HPUpdate",
            Character = session.Player.Identifier,
            HP = session.Player.CurrentHP,
            MaxHP = session.Player.MaxHP,
            ActionTime = session.Player.ActionTimeSeconds,
            SimTime = 0,
            Timestamp = session.EventTimestamp++
        });

        // Emit initial HP/ActionTime events for all enemies
        foreach (var enemy in session.Enemies)
        {
            session.RecordedEvents.Add(new CombatEvent
            {
                Type = "HPUpdate",
                Character = enemy.Identifier,
                HP = enemy.CurrentHP,
                MaxHP = enemy.MaxHP,
                ActionTime = enemy.ActionTimeSeconds,
                SimTime = 0,
                Timestamp = session.EventTimestamp++
            });
        }

        // Emit BattleStart
        session.RecordedEvents.Add(new CombatEvent
        {
            Type = "BattleStart",
            SimTime = 0,
            Timestamp = session.EventTimestamp++
        });

        return session;
    }

    /// <inheritdoc />
    public CombatActionResult ProcessPlayerAutoAttack(CombatSession session)
    {
        if (session.IsComplete) return new CombatActionResult { BattleOver = true, Outcome = session.Winner == session.Player.Identifier ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon };

        // Advance target to next alive enemy
        AdvanceTarget(session);
        if (session.CurrentTargetIndex >= session.Enemies.Count)
            return CompleteBattle(session, session.Player.Identifier);

        var target = session.Enemies[session.CurrentTargetIndex];
        var result = new CombatActionResult();
        var simTime = (DateTime.UtcNow - session.BattleStartedAt).TotalMilliseconds;

        // Calculate base damage
        var (damage, isCritical) = CalculateDamage(
            session.Player.Power, session.Player.CriticalChance, GetEffectiveDefense(session, target), session.Rng);

        // Apply Canhão boost
        bool isBoosted = false;
        if (session.CanhaoBoostRemaining > 0)
        {
            damage = (int)Math.Round(damage * CanhaoDamageMultiplier);
            session.CanhaoBoostRemaining--;
            isBoosted = true;
        }

        // Apply power boost (from Acordeão, Percussão, etc.)
        if (session.PlayerPowerBoost.HitsRemaining > 0)
        {
            damage = (int)Math.Round(damage * (1 + session.PlayerPowerBoost.BoostFraction));
            session.PlayerPowerBoost = (session.PlayerPowerBoost.HitsRemaining - 1, session.PlayerPowerBoost.BoostFraction);
        }

        // Apply vulnerable on target (double damage)
        if (session.EnemyVulnerableStacks.TryGetValue(target.Identifier, out var vulnStacks) && vulnStacks > 0)
        {
            damage *= 2;
            session.EnemyVulnerableStacks[target.Identifier] = vulnStacks - 1;
        }

        // Apply damage
        target.CurrentHP = Math.Max(0, target.CurrentHP - damage);

        // Emit Attack event
        var attackEvt = new CombatEvent
        {
            Type = "Attack",
            Attacker = session.Player.Identifier,
            Defender = target.Identifier,
            Damage = damage,
            IsCritical = isCritical,
            IsBoosted = isBoosted ? true : null,
            SimTime = simTime,
            Timestamp = session.EventTimestamp++
        };
        result.Events.Add(attackEvt);
        session.RecordedEvents.Add(attackEvt);

        // Emit HPUpdate
        var hpEvt = new CombatEvent
        {
            Type = "HPUpdate",
            Character = target.Identifier,
            HP = target.CurrentHP,
            SimTime = simTime,
            Timestamp = session.EventTimestamp++
        };
        result.Events.Add(hpEvt);
        session.RecordedEvents.Add(hpEvt);

        // Consume haste stack
        if (session.PlayerHaste.HitsRemaining > 0)
        {
            session.PlayerHaste = (session.PlayerHaste.HitsRemaining - 1, session.PlayerHaste.SpeedFraction);
        }

        // Process bleed ticks on all enemies
        ProcessBleedTicks(session, result, simTime);

        // Check KO
        if (target.CurrentHP <= 0)
        {
            EmitKO(session, result, target.Identifier, simTime);
            ClearEnemyEffects(session, target.Identifier);

            // Check if all enemies dead → victory
            if (session.Enemies.All(e => e.CurrentHP <= 0))
            {
                return EmitVictory(session, result, session.Player.Identifier, simTime);
            }
        }

        session.LastPlayerActionAt = DateTime.UtcNow;
        result.SpellCooldowns = new Dictionary<string, double>(session.SpellCooldowns);
        return result;
    }

    /// <inheritdoc />
    public CombatActionResult ProcessPlayerSpell(CombatSession session, string attackId)
    {
        if (session.IsComplete) return new CombatActionResult { BattleOver = true, Outcome = session.Winner == session.Player.Identifier ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon };

        var spell = session.EquippedSpells.FirstOrDefault(s => s.AttackId == attackId);
        if (spell == null)
            return ProcessPlayerAutoAttack(session);

        // Check cooldown
        if (session.SpellCooldowns.TryGetValue(attackId, out var remaining) && remaining > 0)
            return ProcessPlayerAutoAttack(session);

        var result = new CombatActionResult();
        var simTime = (DateTime.UtcNow - session.BattleStartedAt).TotalMilliseconds;
        var vfxTypeInt = (int)spell.VfxType;
        var effectName = spell.EffectType != SpellEffectType.None ? spell.EffectType.ToString().ToLowerInvariant() : null;

        // ── Heavy Attack (single target, big damage) ──
        if (attackId == "heavy_attack")
        {
            AdvanceTarget(session);
            if (session.CurrentTargetIndex >= session.Enemies.Count)
                return CompleteBattle(session, session.Player.Identifier);

            var target = session.Enemies[session.CurrentTargetIndex];
            var rawDamage = CalculateSpellDamage(session.Player.Power, spell, session.Rng);
            // Apply Powers heavy attack damage bonus
            if (session.HeavyAttackDamageBonus > 0)
                rawDamage = (int)Math.Round(rawDamage * (1.0 + session.HeavyAttackDamageBonus));
            rawDamage = ApplyDefenseMitigation(rawDamage, GetEffectiveDefense(session, target));

            // Apply vulnerable
            if (session.EnemyVulnerableStacks.TryGetValue(target.Identifier, out var vulnStacks) && vulnStacks > 0)
            {
                rawDamage *= 2;
                session.EnemyVulnerableStacks[target.Identifier] = vulnStacks - 1;
            }

            target.CurrentHP = Math.Max(0, target.CurrentHP - rawDamage);

            EmitSpellAttack(session, result, spell, session.Player.Identifier, target.Identifier, rawDamage, true, null, null, simTime);
            EmitHPUpdate(session, result, target.Identifier, target.CurrentHP, simTime);

            if (target.CurrentHP <= 0)
            {
                EmitKO(session, result, target.Identifier, simTime);
                ClearEnemyEffects(session, target.Identifier);
            }
        }
        // ── Self-targeting abilities ──
        else if (spell.Target == SpellTargetType.Self)
        {
            switch (spell.EffectType)
            {
                case SpellEffectType.Regen: // Bandolim Serenade
                    session.PlayerRegen = (spell.EffectDuration, spell.DamageMultiplier);
                    EmitSpellAttack(session, result, spell, session.Player.Identifier, session.Player.Identifier, 0, false, effectName, spell.EffectDuration, simTime);
                    break;

                case SpellEffectType.Haste: // Cavaquinho Frenzy
                    session.PlayerHaste = (spell.EffectDuration, spell.EffectMagnitude);
                    EmitSpellAttack(session, result, spell, session.Player.Identifier, session.Player.Identifier, 0, false, effectName, spell.EffectDuration, simTime);
                    break;

                case SpellEffectType.PowerBoost: // Acordeão War Cry
                    session.PlayerPowerBoost = (spell.EffectDuration, spell.EffectMagnitude);
                    EmitSpellAttack(session, result, spell, session.Player.Identifier, session.Player.Identifier, 0, false, effectName, spell.EffectDuration, simTime);
                    break;

                case SpellEffectType.Shield: // Percussão War Drums (shield + power boost)
                    session.InstrumentShieldHits = spell.EffectDuration;
                    if (spell.EffectMagnitude > 0)
                        session.PlayerPowerBoost = (spell.EffectDuration + 2, spell.EffectMagnitude);
                    EmitSpellAttack(session, result, spell, session.Player.Identifier, session.Player.Identifier, 0, false, effectName, spell.EffectDuration, simTime);
                    break;

                case SpellEffectType.DefenseBoost: // Estandarte Rally (defense boost + small heal)
                    session.PlayerDefenseBoost = (spell.EffectDuration, spell.EffectMagnitude);
                    if (spell.DamageMultiplier > 0)
                    {
                        var healAmount = (int)(session.Player.MaxHP * spell.DamageMultiplier);
                        session.Player.CurrentHP = Math.Min(session.Player.MaxHP, session.Player.CurrentHP + healAmount);
                        EmitSpellAttack(session, result, spell, session.Player.Identifier, session.Player.Identifier, -healAmount, false, effectName, spell.EffectDuration, simTime);
                        EmitHPUpdate(session, result, session.Player.Identifier, session.Player.CurrentHP, simTime);
                    }
                    else
                    {
                        EmitSpellAttack(session, result, spell, session.Player.Identifier, session.Player.Identifier, 0, false, effectName, spell.EffectDuration, simTime);
                    }
                    break;

                default:
                    // Generic self-buff (fallback)
                    EmitSpellAttack(session, result, spell, session.Player.Identifier, session.Player.Identifier, 0, false, effectName, spell.EffectDuration, simTime);
                    break;
            }
        }
        // ── AoE abilities ──
        else if (spell.Target == SpellTargetType.AllEnemies)
        {
            var aliveEnemies = session.Enemies.Where(e => e.CurrentHP > 0).ToList();
            var targets = new List<string>();
            var damages = new List<int>();

            foreach (var enemy in aliveEnemies)
            {
                var dmg = 0;
                if (spell.DamageMultiplier > 0)
                {
                    dmg = CalculateSpellDamage(session.Player.Power, spell, session.Rng);
                    // Apply Powers special attack damage bonus
                    if (session.SpecialAttackDamageBonus > 0)
                        dmg = (int)Math.Round(dmg * (1.0 + session.SpecialAttackDamageBonus));
                    dmg = ApplyDefenseMitigation(dmg, GetEffectiveDefense(session, enemy));
                    enemy.CurrentHP = Math.Max(0, enemy.CurrentHP - dmg);
                }

                targets.Add(enemy.Identifier);
                damages.Add(dmg);

                // Apply status effects per-enemy
                switch (spell.EffectType)
                {
                    case SpellEffectType.Sleep:
                        session.EnemySleepTurns[enemy.Identifier] = spell.EffectDuration;
                        break;
                    case SpellEffectType.DefenseBreak:
                        session.EnemyDefenseBreak[enemy.Identifier] = (spell.EffectDuration, spell.EffectMagnitude);
                        break;
                    case SpellEffectType.Slow:
                        session.EnemySlow[enemy.Identifier] = (spell.EffectDuration, spell.EffectMagnitude);
                        break;
                    case SpellEffectType.Vulnerable:
                        // Saxofone — reduce enemy power
                        session.EnemyPowerReduction[enemy.Identifier] = (spell.EffectDuration, spell.EffectMagnitude);
                        break;
                }
            }

            // Emit single AoE attack event
            var atkEvt = new CombatEvent
            {
                Type = "Attack",
                Attacker = session.Player.Identifier,
                Defender = targets.FirstOrDefault(),
                Damage = damages.Sum(),
                AttackId = spell.AttackId,
                AbilityName = spell.Name,
                VfxType = ((int)spell.VfxType).ToString(),
                VfxColor = spell.VfxColor,
                VisualHint = spell.ScreenShake ? "screenShake" : null,
                Targets = targets,
                TargetDamages = damages,
                EffectName = effectName,
                EffectDuration = spell.EffectDuration > 0 ? spell.EffectDuration : null,
                SimTime = simTime,
                Timestamp = session.EventTimestamp++
            };
            result.Events.Add(atkEvt);
            session.RecordedEvents.Add(atkEvt);

            // HPUpdate + KO per target
            foreach (var enemy in aliveEnemies)
            {
                EmitHPUpdate(session, result, enemy.Identifier, enemy.CurrentHP, simTime);
                if (enemy.CurrentHP <= 0)
                {
                    EmitKO(session, result, enemy.Identifier, simTime);
                    ClearEnemyEffects(session, enemy.Identifier);
                }
            }
        }
        // ── Single-target abilities ──
        else
        {
            AdvanceTarget(session);
            if (session.CurrentTargetIndex >= session.Enemies.Count)
                return CompleteBattle(session, session.Player.Identifier);

            var target = session.Enemies[session.CurrentTargetIndex];
            var rawDamage = 0;
            if (spell.DamageMultiplier > 0)
            {
                rawDamage = CalculateSpellDamage(session.Player.Power, spell, session.Rng);
                // Apply Powers special attack damage bonus
                if (session.SpecialAttackDamageBonus > 0)
                    rawDamage = (int)Math.Round(rawDamage * (1.0 + session.SpecialAttackDamageBonus));
                rawDamage = ApplyDefenseMitigation(rawDamage, GetEffectiveDefense(session, target));
                target.CurrentHP = Math.Max(0, target.CurrentHP - rawDamage);
            }

            // Apply single-target effects
            switch (spell.EffectType)
            {
                case SpellEffectType.Vulnerable:
                    session.EnemyVulnerableStacks[target.Identifier] = spell.EffectDuration;
                    break;
                case SpellEffectType.Bleed:
                    var bleedPerTick = (int)(rawDamage * spell.EffectMagnitude);
                    session.EnemyBleed[target.Identifier] = (spell.EffectDuration, Math.Max(1, bleedPerTick));
                    break;
            }

            EmitSpellAttack(session, result, spell, session.Player.Identifier, target.Identifier, rawDamage, spell.CanCrit, effectName, spell.EffectDuration, simTime);
            EmitHPUpdate(session, result, target.Identifier, target.CurrentHP, simTime);

            if (target.CurrentHP <= 0)
            {
                EmitKO(session, result, target.Identifier, simTime);
                ClearEnemyEffects(session, target.Identifier);
            }
        }

        // Check if all enemies dead → victory
        if (session.Enemies.All(e => e.CurrentHP <= 0))
        {
            return EmitVictory(session, result, session.Player.Identifier, simTime);
        }

        // Set cooldown
        session.SpellCooldowns[attackId] = spell.CooldownSeconds;

        session.LastPlayerActionAt = DateTime.UtcNow;
        result.SpellCooldowns = new Dictionary<string, double>(session.SpellCooldowns);
        return result;
    }

    /// <inheritdoc />
    public CombatActionResult ProcessEnemyAttack(CombatSession session, int enemyIndex)
    {
        if (session.IsComplete) return new CombatActionResult { BattleOver = true, Outcome = session.Winner == session.Player.Identifier ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon };
        if (enemyIndex < 0 || enemyIndex >= session.Enemies.Count)
            return new CombatActionResult();

        var enemy = session.Enemies[enemyIndex];
        if (enemy.CurrentHP <= 0 || session.Player.CurrentHP <= 0)
            return new CombatActionResult();

        var result = new CombatActionResult();
        var simTime = (DateTime.UtcNow - session.BattleStartedAt).TotalMilliseconds;

        // ── Sleep check: enemy skips turn ──
        if (session.EnemySleepTurns.TryGetValue(enemy.Identifier, out var sleepTurns) && sleepTurns > 0)
        {
            session.EnemySleepTurns[enemy.Identifier] = sleepTurns - 1;
            var sleepEvt = new CombatEvent
            {
                Type = "StatusEffect",
                Character = enemy.Identifier,
                EffectName = "sleep",
                EffectDuration = sleepTurns - 1,
                SimTime = simTime,
                Timestamp = session.EventTimestamp++
            };
            result.Events.Add(sleepEvt);
            session.RecordedEvents.Add(sleepEvt);

            if (enemyIndex < session.LastEnemyActionAt.Length)
                session.LastEnemyActionAt[enemyIndex] = DateTime.UtcNow;
            return result;
        }

        // ── Calculate effective enemy power (apply power reduction from Saxofone) ──
        var effectivePower = enemy.Power;
        if (session.EnemyPowerReduction.TryGetValue(enemy.Identifier, out var powerRed) && powerRed.HitsRemaining > 0)
        {
            effectivePower = (int)(enemy.Power * (1.0 - powerRed.ReductionFraction));
            session.EnemyPowerReduction[enemy.Identifier] = (powerRed.HitsRemaining - 1, powerRed.ReductionFraction);
        }

        // ── Calculate effective player defense (apply defense boost from Estandarte) ──
        var effectivePlayerDef = session.Player.Defense;
        if (session.PlayerDefenseBoost.HitsRemaining > 0)
        {
            effectivePlayerDef = (int)(session.Player.Defense * (1.0 + session.PlayerDefenseBoost.BoostFraction));
            session.PlayerDefenseBoost = (session.PlayerDefenseBoost.HitsRemaining - 1, session.PlayerDefenseBoost.BoostFraction);
        }

        var (damage, isCritical) = CalculateDamage(
            effectivePower, enemy.CriticalChance, effectivePlayerDef, session.Rng);

        // ── Instrument Shield (Percussão) — absorb hit ──
        bool isBlocked = false;
        if (session.InstrumentShieldHits > 0)
        {
            damage = 0;
            session.InstrumentShieldHits--;
            isBlocked = true;
        }
        // ── Cigarro shield (legacy) ──
        else if (session.CigarroShieldRemaining > 0)
        {
            damage = 0;
            session.CigarroShieldRemaining--;
            isBlocked = true;
        }

        session.Player.CurrentHP = Math.Max(0, session.Player.CurrentHP - damage);

        var atkEvt = new CombatEvent
        {
            Type = "Attack",
            Attacker = enemy.Identifier,
            Defender = session.Player.Identifier,
            Damage = damage,
            IsCritical = isCritical,
            IsBlocked = isBlocked ? true : null,
            SimTime = simTime,
            Timestamp = session.EventTimestamp++
        };
        result.Events.Add(atkEvt);
        session.RecordedEvents.Add(atkEvt);

        EmitHPUpdate(session, result, session.Player.Identifier, session.Player.CurrentHP, simTime);

        if (session.Player.CurrentHP <= 0)
        {
            EmitKO(session, result, session.Player.Identifier, simTime);

            var victoryEvt = new CombatEvent
            {
                Type = "Victory",
                Winner = "Enemies",
                SimTime = simTime,
                Timestamp = session.EventTimestamp++
            };
            result.Events.Add(victoryEvt);
            session.RecordedEvents.Add(victoryEvt);

            result.BattleOver = true;
            result.Outcome = BattleOutcome.DefenderWon;
            session.IsComplete = true;
            session.Winner = "Enemies";
        }

        // ── Slow: emit slow factor so JS can reduce speed bar rate ──
        if (session.EnemySlow.TryGetValue(enemy.Identifier, out var slow) && slow.HitsRemaining > 0)
        {
            session.EnemySlow[enemy.Identifier] = (slow.HitsRemaining - 1, slow.SlowFraction);
        }

        if (enemyIndex < session.LastEnemyActionAt.Length)
            session.LastEnemyActionAt[enemyIndex] = DateTime.UtcNow;

        return result;
    }

    /// <inheritdoc />
    public Dictionary<string, double> TickCooldowns(CombatSession session, double elapsedSeconds)
    {
        foreach (var key in session.SpellCooldowns.Keys.ToList())
        {
            session.SpellCooldowns[key] = Math.Max(0, session.SpellCooldowns[key] - elapsedSeconds);
        }

        // ── Consumable cooldowns (Fino 1min, Caneca 2min) ──
        foreach (var key in session.ConsumableCooldowns.Keys.ToList())
        {
            session.ConsumableCooldowns[key] = Math.Max(0, session.ConsumableCooldowns[key] - elapsedSeconds);
        }

        // ── Regen tick (Bandolim Serenade) ──
        if (session.PlayerRegen.TicksRemaining > 0)
        {
            var healAmount = (int)(session.Player.MaxHP * session.PlayerRegen.HealFraction);
            session.Player.CurrentHP = Math.Min(session.Player.MaxHP, session.Player.CurrentHP + healAmount);
            session.PlayerRegen = (session.PlayerRegen.TicksRemaining - 1, session.PlayerRegen.HealFraction);
        }

        return new Dictionary<string, double>(session.SpellCooldowns);
    }

    // ── Private helpers (same formulas as DeterministicCombatEngine) ──

    private static (int damage, bool isCritical) CalculateDamage(
        int power, double criticalChance, int targetDefense, SeededRandom rng)
    {
        var variance = rng.Next(DamageVarianceMin, DamageVarianceMax);
        var damage = power * variance;
        var isCritical = rng.NextDouble() < criticalChance;
        if (isCritical) damage *= 2;

        var rawDamage = (int)Math.Round(damage, MidpointRounding.AwayFromZero);
        var finalDamage = ApplyDefenseMitigation(rawDamage, targetDefense);
        return (finalDamage, isCritical);
    }

    private static int CalculateSpellDamage(int power, SpecialAttack spell, SeededRandom rng)
    {
        var variance = rng.Next(DamageVarianceMin, DamageVarianceMax);
        var damage = power * variance * spell.DamageMultiplier;

        if (spell.CanCrit)
        {
            // Spells that can crit always crit (premium feel)
            damage *= 2;
        }

        return (int)Math.Round(damage, MidpointRounding.AwayFromZero);
    }

    private static int ApplyDefenseMitigation(int rawDamage, int defense)
    {
        var k = MyTunoScaling.DefenseK;
        var minDamage = MyTunoScaling.MinDamage;
        var multiplier = k / (k + defense);
        var mitigatedDamage = (int)Math.Floor(rawDamage * multiplier);
        return Math.Max(minDamage, mitigatedDamage);
    }

    private static void AdvanceTarget(CombatSession session)
    {
        while (session.CurrentTargetIndex < session.Enemies.Count &&
               session.Enemies[session.CurrentTargetIndex].CurrentHP <= 0)
        {
            session.CurrentTargetIndex++;
        }
    }

    private static CombatActionResult CompleteBattle(CombatSession session, string winner)
    {
        var simTime = (DateTime.UtcNow - session.BattleStartedAt).TotalMilliseconds;
        var victoryEvt = new CombatEvent
        {
            Type = "Victory",
            Winner = winner,
            SimTime = simTime,
            Timestamp = session.EventTimestamp++
        };
        session.RecordedEvents.Add(victoryEvt);
        session.IsComplete = true;
        session.Winner = winner;

        return new CombatActionResult
        {
            Events = new List<CombatEvent> { victoryEvt },
            BattleOver = true,
            Outcome = (winner != "Enemies") ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon
        };
    }

    // ── Effect-aware helpers ──

    private static int GetEffectiveDefense(CombatSession session, CombatantState target)
    {
        var defense = target.Defense;
        if (session.EnemyDefenseBreak.TryGetValue(target.Identifier, out var db) && db.HitsRemaining > 0)
        {
            defense = (int)(defense * (1.0 - db.ReductionFraction));
        }
        return defense;
    }

    private static void ProcessBleedTicks(CombatSession session, CombatActionResult result, double simTime)
    {
        foreach (var key in session.EnemyBleed.Keys.ToList())
        {
            var (ticks, dmgPerTick) = session.EnemyBleed[key];
            if (ticks <= 0) continue;

            var enemy = session.Enemies.FirstOrDefault(e => e.Identifier == key);
            if (enemy == null || enemy.CurrentHP <= 0) continue;

            enemy.CurrentHP = Math.Max(0, enemy.CurrentHP - dmgPerTick);
            session.EnemyBleed[key] = (ticks - 1, dmgPerTick);

            var bleedEvt = new CombatEvent
            {
                Type = "StatusEffect",
                Character = key,
                Damage = dmgPerTick,
                EffectName = "bleed",
                EffectDuration = ticks - 1,
                SimTime = simTime,
                Timestamp = session.EventTimestamp++
            };
            result.Events.Add(bleedEvt);
            session.RecordedEvents.Add(bleedEvt);

            EmitHPUpdate(session, result, key, enemy.CurrentHP, simTime);

            if (enemy.CurrentHP <= 0)
            {
                EmitKO(session, result, key, simTime);
                ClearEnemyEffects(session, key);
            }
        }
    }

    private static void EmitKO(CombatSession session, CombatActionResult result, string identifier, double simTime)
    {
        var koEvt = new CombatEvent
        {
            Type = "KO",
            Character = identifier,
            SimTime = simTime,
            Timestamp = session.EventTimestamp++
        };
        result.Events.Add(koEvt);
        session.RecordedEvents.Add(koEvt);
    }

    private static CombatActionResult EmitVictory(CombatSession session, CombatActionResult result, string winner, double simTime)
    {
        var victoryEvt = new CombatEvent
        {
            Type = "Victory",
            Winner = winner,
            SimTime = simTime,
            Timestamp = session.EventTimestamp++
        };
        result.Events.Add(victoryEvt);
        session.RecordedEvents.Add(victoryEvt);
        result.BattleOver = true;
        result.Outcome = (winner != "Enemies") ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon;
        session.IsComplete = true;
        session.Winner = winner;
        return result;
    }

    private static void ClearEnemyEffects(CombatSession session, string identifier)
    {
        session.EnemySleepTurns.Remove(identifier);
        session.EnemyVulnerableStacks.Remove(identifier);
        session.EnemyBleed.Remove(identifier);
        session.EnemySlow.Remove(identifier);
        session.EnemyPowerReduction.Remove(identifier);
        session.EnemyDefenseBreak.Remove(identifier);
    }

    private static void EmitHPUpdate(CombatSession session, CombatActionResult result, string identifier, int hp, double simTime)
    {
        var hpEvt = new CombatEvent
        {
            Type = "HPUpdate",
            Character = identifier,
            HP = hp,
            SimTime = simTime,
            Timestamp = session.EventTimestamp++
        };
        result.Events.Add(hpEvt);
        session.RecordedEvents.Add(hpEvt);
    }

    private static void EmitSpellAttack(CombatSession session, CombatActionResult result,
        SpecialAttack spell, string attacker, string defender, int damage, bool isCrit,
        string? effectName, int? effectDuration, double simTime)
    {
        var atkEvt = new CombatEvent
        {
            Type = "Attack",
            Attacker = attacker,
            Defender = defender,
            Damage = damage,
            AttackId = spell.AttackId,
            AbilityName = spell.Name,
            VfxType = ((int)spell.VfxType).ToString(),
            VfxColor = spell.VfxColor,
            VisualHint = spell.ScreenShake ? "screenShake" : null,
            EffectName = effectName,
            EffectDuration = effectDuration,
            SimTime = simTime,
            Timestamp = session.EventTimestamp++
        };
        result.Events.Add(atkEvt);
        session.RecordedEvents.Add(atkEvt);
    }
}
