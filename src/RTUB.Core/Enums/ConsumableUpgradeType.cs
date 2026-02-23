namespace RTUB.Core.Enums;

/// <summary>
/// Types of consumable upgrades that improve consumable item effects.
/// Each type has its own rank progression, cost schedule, and effect scaling.
/// </summary>
public enum ConsumableUpgradeType
{
    /// <summary>Increases Cigarro dodge chance per rank</summary>
    CigarroDodge = 0,

    /// <summary>Increases Shot stats buff multiplier per rank</summary>
    ShotBuff = 1,

    /// <summary>Increases Canhão AOE buff duration per rank</summary>
    CanhaoTimer = 2,

    /// <summary>Increases Penalty lifesteal buff duration and lifesteal percentage per rank</summary>
    PenaltyTimer = 3
}
