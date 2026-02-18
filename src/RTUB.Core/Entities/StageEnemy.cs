using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an enemy template in Stage Mode.
/// Stats are determined by tier configuration — this entity only stores visual/identity data.
/// </summary>
public class StageEnemy : BaseEntity
{
    /// <summary>
    /// Name of the enemy (e.g., "Wolf", "Bear", "Dragon")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of enemy (Normal, Boss)
    /// </summary>
    public EnemyType Type { get; set; } = EnemyType.Normal;

    /// <summary>
    /// Region where this enemy appears
    /// </summary>
    public RegionType Region { get; set; } = RegionType.Forest;

    /// <summary>
    /// Sprite path for the enemy image
    /// </summary>
    [StringLength(500)]
    public string? SpritePath { get; set; }

    /// <summary>
    /// For bosses only: the specific stage number this boss appears on (10, 20, 30, etc.)
    /// Null for normal enemies which can appear randomly
    /// </summary>
    public int? BossStageNumber { get; set; }

    /// <summary>
    /// How the enemy is positioned in battle (ground or flying)
    /// </summary>
    public PlacementType Placement { get; set; } = PlacementType.Terrestrial;

    // Private constructor for EF Core
    private StageEnemy() { }

    /// <summary>
    /// Factory method to create a new stage enemy
    /// </summary>
    public static StageEnemy Create(
        string name,
        EnemyType type,
        RegionType region,
        string? spritePath = null,
        int? bossStageNumber = null,
        PlacementType placement = PlacementType.Terrestrial)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        return new StageEnemy
        {
            Name = name,
            Type = type,
            Region = region,
            SpritePath = spritePath,
            BossStageNumber = bossStageNumber,
            Placement = placement
        };
    }

}
