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
    /// Mini-boss - appears every 10 stages
    /// Tougher than normal mobs
    /// </summary>
    MiniBoss = 1,

    /// <summary>
    /// Boss - appears every 100 stages
    /// Very tough with lots of HP
    /// </summary>
    Boss = 2
}
