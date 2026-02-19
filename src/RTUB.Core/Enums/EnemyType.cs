namespace RTUB.Core.Enums;

/// <summary>
/// Represents the type of enemy in Stage Mode
/// </summary>
public enum EnemyType
{
    /// <summary>
    /// Standard enemy mob
    /// </summary>
    Normal = 0,

    /// <summary>
    /// MiniBoss - appears every 10 stages (boosted normal enemy stats)
    /// </summary>
    MiniBoss = 1,

    /// <summary>
    /// Boss - appears every 100 stages (configured via bossEveryNStages)
    /// Very tough with lots of HP, uses boss_N_X sprites
    /// </summary>
    Boss = 2
}
