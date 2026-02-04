namespace RTUB.Core.Enums;

/// <summary>
/// Represents the type of stat that can be upgraded for a character
/// </summary>
public enum StatType
{
    /// <summary>
    /// Health Points stat
    /// </summary>
    HP = 0,

    /// <summary>
    /// Power stat (damage)
    /// </summary>
    Power = 1,

    /// <summary>
    /// Speed stat (turn order/frequency)
    /// </summary>
    Speed = 2,

    /// <summary>
    /// Critical chance stat (chance to deal double damage)
    /// </summary>
    CriticalChance = 3,

    /// <summary>
    /// Defense stat (damage reduction)
    /// </summary>
    Defense = 4
}
