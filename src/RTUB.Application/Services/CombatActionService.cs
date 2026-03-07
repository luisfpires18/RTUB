using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Configuration;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Processes individual combat actions during interactive (real-time) battle.
/// Auto-attack model: attacks fire automatically based on speed bar.
///
/// All damage calculations reuse the same formulas as DeterministicCombatEngine:
///   rawDamage = power × variance(0.8–1.2) × crit(2x)
///   finalDamage = max(MinDamage, floor(rawDamage × K/(K+defense)))
/// </summary>
public class CombatActionService(IInventoryRepository inventoryRepository) : ICombatActionService
{
    private readonly IInventoryRepository _inventoryRepository = inventoryRepository;

    /// <inheritdoc />
    public CombatSession CreateSession(
        Character player,
        List<Character> enemies,
        int seed,
        string mode = "stage")
    {
        ArgumentNullException.ThrowIfNull(enemies);
        if (enemies.Count == 0)
            throw new ArgumentException("Must have at least one enemy", nameof(enemies));

        // Convert Character list to CombatantState list and delegate to the canonical overload
        var enemyStates = enemies.Select((e, i) => new CombatantState
        {
            Identifier = mode == "arena" ? "Defender" : $"Enemy{i}",
            Name = e.User?.UserName ?? $"Enemy {i + 1}",
            CurrentHP = e.CurrentHP ?? e.TotalHP,
            MaxHP = e.TotalHP,
            Power = e.TotalPower,
            Defense = e.TotalDefense,
            CriticalChance = e.TotalCriticalChance,
            ActionTimeSeconds = e.ActionTime
        }).ToList();

        return CreateSession(player, enemyStates, seed, mode);
    }

    /// <inheritdoc />
    public CombatSession CreateSession(
        Character player,
        List<CombatantState> enemyStates,
        int seed,
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
            HasShotBuff = mode != "arena" && player.ShotBuffBattlesRemaining > 0,
            HasCigarroBuff = mode != "arena" && player.CigarroShieldHitsRemaining > 0,
            HasCanhaoBuff = mode == "stage" && player.HasCanhaoBuff, // AOE only in stage mode
            HasPenaltyBuff = mode != "arena" && player.HasPenaltyBuff,
            EffectiveCigarroDodge = player.EffectiveCigarroDodgeChance,
            EffectivePenaltyLifesteal = player.EffectivePenaltyLifesteal,
            BattleStartedAt = DateTime.UtcNow,
            LastPlayerActionAt = DateTime.UtcNow,
            LastEnemyActionAt = new DateTime[enemyStates.Count],
            EventTimestamp = 0
        };

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

    /// <summary>
    /// Base fraction of ActionTime that must elapse between auto-attacks at 1x speed.
    /// At higher battle speeds, this is divided by <see cref="CombatSession.BattleSpeed"/>
    /// so that fast-forward modes are not bottlenecked by the rate limiter.
    /// The minimum effective fraction is clamped to 0.003 (~333x cap) to prevent exploits.
    /// </summary>
    private const double ActionTimeToleranceFraction = 0.3;

    /// <summary>
    /// Returns the effective tolerance fraction adjusted for the session's battle speed.
    /// </summary>
    private static double GetEffectiveTolerance(CombatSession session)
    {
        var speed = Math.Max(1.0, session.BattleSpeed);
        return Math.Max(0.003, ActionTimeToleranceFraction / speed);
    }

    /// <inheritdoc />
    public CombatActionResult ProcessPlayerAutoAttack(CombatSession session)
    {
        if (session.IsComplete) return new CombatActionResult { BattleOver = true, Outcome = session.Winner == session.Player.Identifier ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon };

        // Anti-exploit: reject if not enough real time has elapsed since last player action
        var now = DateTime.UtcNow;
        var elapsed = (now - session.LastPlayerActionAt).TotalSeconds;
        var minRequired = session.Player.ActionTimeSeconds * GetEffectiveTolerance(session);
        if (elapsed < minRequired)
            return new CombatActionResult(); // silently ignore — speed bar hasn't truly filled

        // Advance target to next alive enemy
        AdvanceTarget(session);
        if (session.CurrentTargetIndex >= session.Enemies.Count)
            return CompleteBattle(session, session.Player.Identifier);

        var result = new CombatActionResult();
        var simTime = (DateTime.UtcNow - session.BattleStartedAt).TotalMilliseconds;

        // Determine targets: Canhão AOE hits ALL alive enemies, otherwise single target
        var targets = session.HasCanhaoBuff
            ? session.Enemies.Where(e => e.CurrentHP > 0).ToList()
            : [session.Enemies[session.CurrentTargetIndex]];

        long totalDamageDealt = 0;

        foreach (var target in targets)
        {
            // Calculate base damage per target (separate crit roll per target)
            var (damage, isCritical) = CombatMath.CalculateDamage(
                session.Player.Power, session.Player.CriticalChance, target.Defense, session.Rng);

            // Apply damage
            target.CurrentHP = Math.Max(0, target.CurrentHP - damage);
            totalDamageDealt += damage;

            // Emit Attack event
            var attackEvt = new CombatEvent
            {
                Type = "Attack",
                Attacker = session.Player.Identifier,
                Defender = target.Identifier,
                Damage = damage,
                IsCritical = isCritical,
                IsAoe = session.HasCanhaoBuff ? true : null,
                SimTime = simTime,
                Timestamp = session.EventTimestamp++
            };
            result.Events.Add(attackEvt);
            session.RecordedEvents.Add(attackEvt);

            // Emit HPUpdate
            EmitHPUpdate(session, result, target.Identifier, target.CurrentHP, simTime);
        }

        // Penalty lifesteal: heal player for % of max HP per attack action (scaled by upgrades)
        if (session.HasPenaltyBuff && session.Player.CurrentHP > 0 && totalDamageDealt > 0)
        {
            var healAmount = (long)Math.Max(1, Math.Round(session.Player.MaxHP * session.EffectivePenaltyLifesteal));
            session.Player.CurrentHP = Math.Min(session.Player.MaxHP, session.Player.CurrentHP + healAmount);
            EmitHPUpdate(session, result, session.Player.Identifier, session.Player.CurrentHP, simTime);
        }

        // Check KOs for all targets hit
        foreach (var target in targets)
        {
            if (target.CurrentHP <= 0)
            {
                EmitKO(session, result, target.Identifier, simTime);
            }
        }

        // Check if all enemies dead → victory
        if (session.Enemies.All(e => e.CurrentHP <= 0))
        {
            return EmitVictory(session, result, session.Player.Identifier, simTime);
        }

        session.LastPlayerActionAt = DateTime.UtcNow;
        return result;
    }

    /// <inheritdoc />
    public CombatActionResult ProcessEnemyAttack(CombatSession session, int enemyIndex)
    {
        if (session.IsComplete) return new CombatActionResult { BattleOver = true, Outcome = session.Winner == session.Player.Identifier ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon };
        if (enemyIndex < 0 || enemyIndex >= session.Enemies.Count)
            return new CombatActionResult();

        // Anti-exploit: reject if not enough real time has elapsed since last enemy action
        var now = DateTime.UtcNow;
        if (enemyIndex < session.LastEnemyActionAt.Length)
        {
            var enemyElapsed = (now - session.LastEnemyActionAt[enemyIndex]).TotalSeconds;
            var enemy2 = session.Enemies[enemyIndex];
            var minEnemyRequired = enemy2.ActionTimeSeconds * GetEffectiveTolerance(session);
            if (enemyElapsed < minEnemyRequired)
                return new CombatActionResult(); // silently ignore
        }

        var enemy = session.Enemies[enemyIndex];
        if (enemy.CurrentHP <= 0 || session.Player.CurrentHP <= 0)
            return new CombatActionResult();

        var result = new CombatActionResult();
        var simTime = (DateTime.UtcNow - session.BattleStartedAt).TotalMilliseconds;

        var (damage, isCritical) = CombatMath.CalculateDamage(
            enemy.Power, enemy.CriticalChance, session.Player.Defense, session.Rng);

        // ── Cigarro dodge buff — chance to dodge incoming attack (scaled by upgrades) ──
        bool isDodged = false;
        if (session.HasCigarroBuff && session.Rng.NextDouble() < session.EffectiveCigarroDodge)
        {
            damage = 0;
            isDodged = true;
        }

        session.Player.CurrentHP = Math.Max(0, session.Player.CurrentHP - damage);

        var atkEvt = new CombatEvent
        {
            Type = "Attack",
            Attacker = enemy.Identifier,
            Defender = session.Player.Identifier,
            Damage = damage,
            IsCritical = isCritical,
            IsDodged = isDodged ? true : null,
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

        if (enemyIndex < session.LastEnemyActionAt.Length)
            session.LastEnemyActionAt[enemyIndex] = DateTime.UtcNow;

        return result;
    }

    /// <inheritdoc />
    public Dictionary<string, double> TickConsumableCooldowns(CombatSession session, double realElapsedSeconds)
    {
        foreach (var key in session.ConsumableCooldowns.Keys.ToList())
        {
            session.ConsumableCooldowns[key] = Math.Max(0, session.ConsumableCooldowns[key] - realElapsedSeconds);
        }
        return new Dictionary<string, double>(session.ConsumableCooldowns);
    }

    // ── Private helpers ──

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
            Events = [victoryEvt],
            BattleOver = true,
            Outcome = (winner != "Enemies") ? BattleOutcome.AttackerWon : BattleOutcome.DefenderWon
        };
    }

    // ── Effect-aware helpers ──

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

    private static void EmitHPUpdate(CombatSession session, CombatActionResult result, string identifier, long hp, double simTime)
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

    // ── Consumable Application ──

    /// <inheritdoc />
    public async Task<ConsumableResult> ApplyConsumableAsync(
        CombatSession? session,
        string userId,
        string type,
        Character character,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        // Arena mode: no consumables allowed at all — buffs should never be wasted here
        if (session?.Mode == "arena")
            return ConsumableResult.Fail("Consumíveis não são permitidos na Arena.");

        switch (type.ToLowerInvariant())
        {
            case "fino":
            {
                if (session == null) return ConsumableResult.Fail("Sessão inválida.");
                if (session.ConsumableCooldowns.TryGetValue("fino", out var cd) && cd > 0)
                    return ConsumableResult.Fail($"Fino em cooldown ({cd:F0}s).");
                if (session.Player.CurrentHP >= session.Player.MaxHP)
                    return ConsumableResult.Fail("HP já está cheio.");

                var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Fino, 1, cancellationToken);
                if (!consumed) return ConsumableResult.Fail("Sem Fino disponível.");

                var maxHp = session.Player.MaxHP;
                var healAmount = (long)(maxHp * MyTunoScaling.FinoHealPercent);
                session.Player.CurrentHP = Math.Min(session.Player.CurrentHP + healAmount, maxHp);
                character.CurrentHP = session.Player.CurrentHP;
                session.ConsumableCooldowns["fino"] = MyTunoScaling.FinoCooldownSeconds;

                return new ConsumableResult
                {
                    Success = true, Type = type, HealAmount = healAmount,
                    PlayerHP = session.Player.CurrentHP, PlayerMaxHP = maxHp,
                    CooldownSeconds = MyTunoScaling.FinoCooldownSeconds
                };
            }
            case "caneca":
            {
                if (session == null) return ConsumableResult.Fail("Sessão inválida.");
                if (session.ConsumableCooldowns.TryGetValue("caneca", out var cd) && cd > 0)
                    return ConsumableResult.Fail($"Caneca em cooldown ({cd:F0}s).");
                if (session.Player.CurrentHP >= session.Player.MaxHP)
                    return ConsumableResult.Fail("HP já está cheio.");

                var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Caneca, 1, cancellationToken);
                if (!consumed) return ConsumableResult.Fail("Sem Caneca disponível.");

                var maxHp = session.Player.MaxHP;
                var healAmount = (long)(maxHp * MyTunoScaling.CanecaHealPercent);
                session.Player.CurrentHP = Math.Min(session.Player.CurrentHP + healAmount, maxHp);
                character.CurrentHP = session.Player.CurrentHP;
                session.ConsumableCooldowns["caneca"] = MyTunoScaling.CanecaCooldownSeconds;

                return new ConsumableResult
                {
                    Success = true, Type = type, HealAmount = healAmount,
                    PlayerHP = session.Player.CurrentHP, PlayerMaxHP = maxHp,
                    CooldownSeconds = MyTunoScaling.CanecaCooldownSeconds
                };
            }
            case "cigarro":
            {
                if (session == null) return ConsumableResult.Fail("Sessão inválida.");
                if (character.CigarroShieldHitsRemaining > 0)
                    return ConsumableResult.Fail("Cigarro já ativo!");

                var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Cigarro, 1, cancellationToken);
                if (!consumed) return ConsumableResult.Fail("Sem Cigarro disponível.");

                character.CigarroShieldHitsRemaining = MyTunoScaling.CigarroBuffRuns;
                session.HasCigarroBuff = true;
                session.EffectiveCigarroDodge = character.EffectiveCigarroDodgeChance;

                return new ConsumableResult
                {
                    Success = true, Type = type,
                    BuffMessage = $"🚬 +{(int)(character.EffectiveCigarroDodgeChance * 100)}% DODGE x{MyTunoScaling.CigarroBuffRuns} runs", BuffActive = true
                };
            }
            case "canhao":
            {
                if (session == null) return ConsumableResult.Fail("Sessão inválida.");
                if (session.Mode != "stage")
                    return ConsumableResult.Fail("Canhão só funciona em Stage Mode.");
                if (character.HasCanhaoBuff)
                    return ConsumableResult.Fail("Canhão já ativo!");

                var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Canhao, 1, cancellationToken);
                if (!consumed) return ConsumableResult.Fail("Sem Canhão disponível.");

                character.CanhaoBuffExpiresAt = DateTime.UtcNow.AddMinutes(character.EffectiveCanhaoMinutes);
                character.CanhaoBuffRemainingMs = 0; // Active (not paused)
                session.HasCanhaoBuff = true;

                return new ConsumableResult
                {
                    Success = true, Type = type,
                    BuffMessage = $"💣 AOE {character.EffectiveCanhaoMinutes}min", BuffActive = true
                };
            }
            case "shot":
            {
                if (character.ShotBuffBattlesRemaining > 0)
                    return ConsumableResult.Fail("Shot já ativo!");

                var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Shot, 1, cancellationToken);
                if (!consumed) return ConsumableResult.Fail("Sem Shot disponível.");

                character.ShotBuffBattlesRemaining = MyTunoScaling.ShotBuffRuns;
                if (session != null)
                {
                    var mult = character.EffectiveShotBuffMultiplier;
                    session.Player.MaxHP = CombatMath.ClampToLong(session.Player.MaxHP * mult);
                    session.Player.CurrentHP = CombatMath.ClampToLong(session.Player.CurrentHP * mult);
                    session.Player.Power = CombatMath.ClampToLong(session.Player.Power * mult);
                    session.Player.Defense = CombatMath.ClampToLong(session.Player.Defense * mult);
                    session.HasShotBuff = true;
                }

                return new ConsumableResult
                {
                    Success = true, Type = type,
                    BuffMessage = $"🥃 SHOT +{(int)((character.EffectiveShotBuffMultiplier - 1) * 100)}% x{MyTunoScaling.ShotBuffRuns}", BuffActive = true
                };
            }
            case "penalty":
            {
                if (character.HasPenaltyBuff)
                    return ConsumableResult.Fail("Penalty já ativo!");

                var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Penalty, 1, cancellationToken);
                if (!consumed) return ConsumableResult.Fail("Sem Penalty disponível.");

                character.PenaltyBuffExpiresAt = DateTime.UtcNow.AddMinutes(character.EffectivePenaltyMinutes);
                character.PenaltyBuffRemainingMs = 0; // Active (not paused)
                if (session != null)
                {
                    session.HasPenaltyBuff = true;
                    session.EffectivePenaltyLifesteal = character.EffectivePenaltyLifesteal;
                }

                return new ConsumableResult
                {
                    Success = true, Type = type,
                    BuffMessage = $"⚡ LIFESTEAL {character.EffectivePenaltyMinutes}min", BuffActive = true
                };
            }
            default:
                return ConsumableResult.Fail("Consumível desconhecido.");
        }
    }
}
