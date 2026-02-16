namespace RTUB.Core.Enums;

/// <summary>
/// Types of improvements (game-wide upgrades).
/// Unlike stat upgrades, these affect overall game mechanics.
/// </summary>
public enum ImprovementType
{
    /// <summary>Increases maximum energy capacity for the Destilaria</summary>
    EnergyAmount = 0,

    /// <summary>Increases energy regeneration speed</summary>
    EnergyRegen = 1,

    /// <summary>Increases the stat bonus given by the Shot buff</summary>
    ShotBuffBonus = 2,

    /// <summary>Increases Fidelis earned from all sources</summary>
    FidelisEarned = 3
}
