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

    /// <summary>Reduces gathering cast time ("A destilar Cerveja...")</summary>
    CastSpeed = 2,

    /// <summary>Chance to double a drink gathering (max 50%)</summary>
    DoubleGathering = 3
}
