using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>One side of a battle as the rounds see it.</summary>
public sealed record PvpSide(PvpTactic Tactic, int LoadoutPower, int Specialisation, int MatchupBonus, int RiskModifier);

/// <summary>The resolved rounds and winner of a battle.</summary>
public sealed record PvpFight(IReadOnlyList<PvpBattleRound> Rounds, int AttackerTotalDamage, int DefenderTotalDamage, int? TieBreakRoll, bool AttackerWon);

/// <summary>
/// PvP rules. Game Manual v2: open PvP, 72-hour new-player protection, 6-hour protection for a
/// beaten defender, 24-hour same-target limit, effective power, loot formulas and deterministic cargo
/// selection, three-round tactical battles. <b>AH-006 defaults</b> (the manual gives no numbers) are
/// marked as such below; tune them here only.
/// </summary>
public static class PvpRules
{
    // Game Manual v2
    public static readonly TimeSpan NewPlayerProtection = TimeSpan.FromHours(72);
    public static readonly TimeSpan DefeatedDefenderProtection = TimeSpan.FromHours(6);
    public static readonly TimeSpan SameTargetWindow = TimeSpan.FromHours(24);
    public const int Rounds = 3;

    // AH-006 defaults
    public const int AttackEnergy = 20;
    public static readonly TimeSpan AttackCooldown = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DefeatedDefenderRecovery = TimeSpan.FromMinutes(15);
    public const int MatchupBonus = 4;
    public const int DamageBase = 10;
    public const int DamageCap = 30;
    public const int TieDamage = 1;
    public const PvpTactic DefaultDefenceTactic = PvpTactic.Counterattack;

    // ------------------------------------------------------------------ power

    private static int Skills(PlayerCycleState s) => s.Toughness + s.Stealth + s.Smarts + s.Charisma;

    /// <summary>
    /// Manual: skills + 2·weapon + outfit + vehicle/tool, using the best tier <b>owned</b> in each slot.
    /// Equipping or unequipping never changes it. Used for strength comparison and loot scaling.
    /// </summary>
    public static int EffectivePower(PlayerCycleState state)
    {
        var best = GearCatalogue.BestOwnedTiers(state.Gear);
        return Skills(state) + 2 * best.Weapon + best.Outfit + best.VehicleTool;
    }

    /// <summary>The same formula over the gear actually taken into this battle.</summary>
    public static int LoadoutPower(PlayerCycleState state, GearTiers selected) =>
        Skills(state) + 2 * selected.Weapon + selected.Outfit + selected.VehicleTool;

    // ------------------------------------------------------------------ tactics (AH-006 defaults)

    /// <summary>AH-006 default tactic specialisation.</summary>
    public static int Specialisation(PvpTactic tactic, PlayerCycleState s, GearTiers selected)
    {
        var gear = selected.Weapon + selected.Outfit + selected.VehicleTool;
        return tactic switch
        {
            PvpTactic.Ambush => (s.Stealth - 4) + (s.Toughness - 4),
            PvpTactic.Negotiation => (s.Charisma - 4) + (s.Smarts - 4),
            PvpTactic.Counterattack => (s.Toughness - 4) + gear,
            PvpTactic.Setup => (s.Smarts - 4) + gear,
            _ => throw new ArgumentOutOfRangeException(nameof(tactic), tactic, "Unknown tactic")
        };
    }

    /// <summary>Manual matchups: Ambush > Negotiation > Setup > Counterattack > Ambush; the rest are neutral.</summary>
    public static bool Beats(PvpTactic tactic, PvpTactic other) => (tactic, other) switch
    {
        (PvpTactic.Ambush, PvpTactic.Negotiation) => true,
        (PvpTactic.Negotiation, PvpTactic.Setup) => true,
        (PvpTactic.Setup, PvpTactic.Counterattack) => true,
        (PvpTactic.Counterattack, PvpTactic.Ambush) => true,
        _ => false
    };

    public static int Matchup(PvpTactic tactic, PvpTactic other) => Beats(tactic, other) ? MatchupBonus : 0;

    /// <summary>AH-006 default: Cautious −2, Standard 0, Reckless +2 to every attacker round score.</summary>
    public static int RiskModifier(RiskStance stance) => stance switch
    {
        RiskStance.Cautious => -2,
        RiskStance.Standard => 0,
        RiskStance.Reckless => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(stance), stance, "Unknown stance")
    };

    /// <summary>AH-006 default attacker recovery after losing: Cautious 10, Standard 15, Reckless 30 minutes.</summary>
    public static TimeSpan AttackerRecovery(RiskStance stance) => stance switch
    {
        RiskStance.Cautious => TimeSpan.FromMinutes(10),
        RiskStance.Standard => TimeSpan.FromMinutes(15),
        RiskStance.Reckless => TimeSpan.FromMinutes(30),
        _ => throw new ArgumentOutOfRangeException(nameof(stance), stance, "Unknown stance")
    };

    // ------------------------------------------------------------------ battle

    /// <summary>
    /// The round's random factor, −2..+2, from a 1..100 dice roll: <c>(roll − 1) mod 5 − 2</c>
    /// (uniform, since 5 divides 100).
    /// </summary>
    public static int RandomFactor(int rollPercent) => (rollPercent - 1) % 5 - 2;

    /// <summary>
    /// Three rounds. Each side scores loadout power + specialisation + matchup (+ risk for the attacker)
    /// + a −2..+2 factor. The higher score deals 10 + the difference, capped at 30; a tie deals 1 each.
    /// The higher total damage wins; equal totals go to one 50/50 roll (attacker on ≤ 50).
    /// Rolls in order: attacker then defender each round, then the tie-break if needed.
    /// </summary>
    public static PvpFight Fight(PvpSide attacker, PvpSide defender, Func<int> rollPercent)
    {
        var rounds = new List<PvpBattleRound>(Rounds);
        for (var round = 1; round <= Rounds; round++)
        {
            var attackerRandom = RandomFactor(rollPercent());
            var defenderRandom = RandomFactor(rollPercent());
            var attackerScore = attacker.LoadoutPower + attacker.Specialisation + attacker.MatchupBonus + attacker.RiskModifier + attackerRandom;
            var defenderScore = defender.LoadoutPower + defender.Specialisation + defender.MatchupBonus + defenderRandom;
            var damage = Math.Min(DamageCap, DamageBase + Math.Abs(attackerScore - defenderScore));

            rounds.Add(new PvpBattleRound
            {
                Round = round,
                AttackerRandom = attackerRandom,
                DefenderRandom = defenderRandom,
                AttackerScore = attackerScore,
                DefenderScore = defenderScore,
                AttackerDamage = attackerScore > defenderScore ? damage : attackerScore == defenderScore ? TieDamage : 0,
                DefenderDamage = defenderScore > attackerScore ? damage : attackerScore == defenderScore ? TieDamage : 0
            });
        }

        var attackerTotal = rounds.Sum(r => r.AttackerDamage);
        var defenderTotal = rounds.Sum(r => r.DefenderDamage);
        if (attackerTotal != defenderTotal)
            return new PvpFight(rounds, attackerTotal, defenderTotal, null, attackerTotal > defenderTotal);

        var tieBreak = rollPercent();
        return new PvpFight(rounds, attackerTotal, defenderTotal, tieBreak, tieBreak <= 50);
    }

    // ------------------------------------------------------------------ loot (Game Manual v2)

    /// <summary>
    /// ratio = defender power / attacker power; multiplier = min(1, ratio × ratio), in decimal and
    /// not rounded (the battle stores the full decimal). No lower bound.
    /// </summary>
    public static decimal LootMultiplier(int defenderEffectivePower, int attackerEffectivePower)
    {
        var ratio = (decimal)defenderEffectivePower / attackerEffectivePower;
        return Math.Min(1m, ratio * ratio);
    }

    /// <summary>
    /// ⌊min(wallet × 0.10, 500) × multiplier⌋: only the final amount is floored. Wallet only; the bank
    /// is never touched.
    /// </summary>
    public static long WalletLoot(long defenderWallet, decimal multiplier) =>
        (long)decimal.Floor(Math.Min(defenderWallet * 0.10m, 500m) * multiplier);

    /// <summary>⌊min(cargo base value × 0.20, 250) × multiplier⌋: only the final budget is floored.</summary>
    public static long CargoBudget(IEnumerable<PlayerCargo> defenderCargo, decimal multiplier)
    {
        var value = defenderCargo.Sum(c => c.Quantity * CargoCatalogue.FencePrice(c.CargoType));
        return (long)decimal.Floor(Math.Min(value * 0.20m, 250m) * multiplier);
    }

    /// <summary>
    /// Deterministic cargo selection: highest base price first, equal prices by <see cref="CargoType"/>
    /// order; an item is taken only while its full price fits in the remaining budget.
    /// </summary>
    public static IReadOnlyList<(CargoType Cargo, int Quantity)> SelectCargo(IEnumerable<PlayerCargo> defenderCargo, long budget)
    {
        var taken = new List<(CargoType, int)>();
        foreach (var row in defenderCargo.Where(c => c.Quantity > 0)
                     .OrderByDescending(c => CargoCatalogue.FencePrice(c.CargoType)).ThenBy(c => c.CargoType))
        {
            var price = CargoCatalogue.FencePrice(row.CargoType);
            var quantity = (int)Math.Min(row.Quantity, budget / price);
            if (quantity == 0) continue;
            taken.Add((row.CargoType, quantity));
            budget -= quantity * price;
        }
        return taken;
    }

    // ------------------------------------------------------------------ restrictions

    /// <summary>New-player protection: until 72 h after the state was created, unless the player has already attacked.</summary>
    public static bool HasNewPlayerProtection(PlayerCycleState state, DateTime utcNow) =>
        state.PvpInitiatedAtUtc is null && utcNow < state.CreatedAt + NewPlayerProtection;

    public static bool IsProtected(PlayerCycleState state, DateTime utcNow) =>
        HasNewPlayerProtection(state, utcNow) || state.PvpProtectedUntilUtc > utcNow;

    /// <summary>Why this player cannot start an attack now, or null. Expects a reconciled state.</summary>
    public static string? AttackerBlockReason(PlayerCycleState attacker, DateTime utcNow)
    {
        if (attacker.IsJailedAt(utcNow)) return "You are in jail.";
        if (attacker.PvpRecoveryUntilUtc > utcNow) return "You are still recovering from a beating.";
        if (attacker.PvpCooldownUntilUtc > utcNow) return "You just fought. Wait for your cooldown.";
        if (attacker.Energy < AttackEnergy) return "Not enough energy.";
        return null;
    }

    /// <summary>Why this target cannot be attacked now, or null.</summary>
    public static string? TargetBlockReason(PlayerCycleState attacker, PlayerCycleState target, DateTime? lastAttackOnTargetUtc, DateTime utcNow)
    {
        if (target.Id == attacker.Id || target.UserId == attacker.UserId) return "You can't attack yourself.";
        if (target.GameCycleId != attacker.GameCycleId) return "That player is not in this cycle.";
        if (HasNewPlayerProtection(target, utcNow)) return "That player is new and still protected.";
        if (target.PvpProtectedUntilUtc > utcNow) return "That player was beaten recently and is protected.";
        if (lastAttackOnTargetUtc is { } last && utcNow < last + SameTargetWindow)
            return "You already attacked that player in the last 24 hours.";
        return null;
    }
}
