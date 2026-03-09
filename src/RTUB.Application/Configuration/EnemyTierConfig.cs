namespace RTUB.Application.Configuration;

/// <summary>
/// Configuration for an enemy tier (Fat Lady variant)
/// </summary>
public class EnemyTierConfig
{
    /// <summary>
    /// Enemy tier name (e.g., Chubby, Plus, Heavy, Mega)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Damage dealt to player on contact
    /// </summary>
    public int Damage { get; set; }

    /// <summary>
    /// Movement speed of this enemy tier
    /// </summary>
    public double Speed { get; set; }

    /// <summary>
    /// Spawn weight (higher = more likely to spawn)
    /// </summary>
    public int SpawnWeight { get; set; }

    /// <summary>
    /// Path to sprite image
    /// </summary>
    public string SpritePath { get; set; } = string.Empty;
}
