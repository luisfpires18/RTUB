using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// One accepted PvP attack, written once in the attack's transaction and never changed. Everything
/// a report shows is stored here (with its <see cref="Rounds"/> and <see cref="Cargo"/>): nothing is
/// recalculated or rerolled to display it. Also the same-target history for the 24-hour rule.
/// </summary>
public class PvpBattle : BaseEntity
{
    public int GameCycleId { get; set; }

    /// <summary>The attacker's receipt for this attack: the idempotency link (one battle per receipt).</summary>
    public int ReceiptId { get; set; }
    public PlayerActionReceipt? Receipt { get; set; }

    public int AttackerStateId { get; set; }
    public int DefenderStateId { get; set; }
    public string AttackerUserId { get; set; } = string.Empty;
    public string DefenderUserId { get; set; } = string.Empty;
    public DateTime AcceptedAtUtc { get; set; }

    public PvpTactic AttackerTactic { get; set; }
    public RiskStance RiskStance { get; set; }
    public PvpTactic DefenderTactic { get; set; }

    /// <summary>False when the defender had never saved a defence and fought with the default.</summary>
    public bool DefenderUsedSavedDefence { get; set; }

    public string? AttackerWeaponKey { get; set; }
    public int AttackerWeaponTier { get; set; }
    public string? AttackerOutfitKey { get; set; }
    public int AttackerOutfitTier { get; set; }
    public string? AttackerVehicleToolKey { get; set; }
    public int AttackerVehicleToolTier { get; set; }
    public string? DefenderWeaponKey { get; set; }
    public int DefenderWeaponTier { get; set; }
    public string? DefenderOutfitKey { get; set; }
    public int DefenderOutfitTier { get; set; }
    public string? DefenderVehicleToolKey { get; set; }
    public int DefenderVehicleToolTier { get; set; }

    public int AttackerEffectivePower { get; set; }
    public int DefenderEffectivePower { get; set; }
    public int AttackerLoadoutPower { get; set; }
    public int DefenderLoadoutPower { get; set; }
    public int AttackerSpecialisation { get; set; }
    public int DefenderSpecialisation { get; set; }
    public int AttackerMatchupBonus { get; set; }
    public int DefenderMatchupBonus { get; set; }
    public int AttackerRiskModifier { get; set; }

    /// <summary>Total damage dealt by each side over the three rounds; the higher total wins.</summary>
    public int AttackerTotalDamage { get; set; }
    public int DefenderTotalDamage { get; set; }

    /// <summary>The 1–100 roll used only when total damage tied; attacker wins on ≤ 50.</summary>
    public int? TieBreakRoll { get; set; }
    public bool AttackerWon { get; set; }

    public decimal LootMultiplier { get; set; }
    public long WalletStolen { get; set; }

    public DateTime AttackerCooldownUntilUtc { get; set; }
    public DateTime? AttackerRecoveryUntilUtc { get; set; }
    public DateTime? DefenderRecoveryUntilUtc { get; set; }
    public DateTime? DefenderProtectedUntilUtc { get; set; }

    /// <summary>This was the attacker's first accepted attack, which ended their new-player protection.</summary>
    public bool EndedAttackerNewPlayerProtection { get; set; }

    public List<PvpBattleRound> Rounds { get; set; } = [];
    public List<PvpBattleCargo> Cargo { get; set; } = [];
}

/// <summary>One of the three rounds, with the random factors that produced it.</summary>
public class PvpBattleRound
{
    public int Id { get; set; }
    public int PvpBattleId { get; set; }
    public int Round { get; set; }
    public int AttackerRandom { get; set; }
    public int DefenderRandom { get; set; }
    public int AttackerScore { get; set; }
    public int DefenderScore { get; set; }

    /// <summary>Damage dealt this round BY the attacker (to the defender) and BY the defender.</summary>
    public int AttackerDamage { get; set; }
    public int DefenderDamage { get; set; }
}

/// <summary>Cargo moved from defender to attacker by one battle.</summary>
public class PvpBattleCargo
{
    public int Id { get; set; }
    public int PvpBattleId { get; set; }
    public CargoType CargoType { get; set; }
    public int Quantity { get; set; }
}
