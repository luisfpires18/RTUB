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
    /// Boss - appears at boss stages (configured via bossEveryNStages)
    /// Very tough with lots of HP
    /// </summary>
    Boss = 2
}
