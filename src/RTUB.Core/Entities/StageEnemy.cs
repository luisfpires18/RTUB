using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an enemy template in Stage Mode
/// Defines base stats for enemy mobs, mini-bosses, and bosses
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
    /// Type of enemy (Normal, MiniBoss, Boss)
    /// </summary>
    public EnemyType Type { get; set; } = EnemyType.Normal;

    /// <summary>
    /// Region where this enemy appears
    /// </summary>
    public RegionType Region { get; set; } = RegionType.Forest;

    /// <summary>
    /// Base HP of the enemy
    /// </summary>
    public int BaseHP { get; set; } = 50;

    /// <summary>
    /// Base power/attack of the enemy
    /// </summary>
    public int BasePower { get; set; } = 8;

    /// <summary>
    /// Base speed of the enemy
    /// </summary>
    public int BaseSpeed { get; set; } = 5;

    /// <summary>
    /// Base defense of the enemy
    /// </summary>
    public int BaseDefense { get; set; } = 3;

    /// <summary>
    /// Base critical chance of the enemy
    /// </summary>
    public double BaseCriticalChance { get; set; } = 0.05;

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
    /// Base Fidelis drop amount
    /// </summary>
    public decimal BaseFidelisDrop { get; set; } = 1.0m;

    /// <summary>
    /// Chance to drop beer (0.0 to 1.0)
    /// </summary>
    public double BeerDropChance { get; set; } = 0.1;

    /// <summary>
    /// Chance to drop shot (0.0 to 1.0)
    /// </summary>
    public double ShotDropChance { get; set; } = 0.05;

    // Private constructor for EF Core
    private StageEnemy() { }

    /// <summary>
    /// Factory method to create a new stage enemy
    /// </summary>
    public static StageEnemy Create(
        string name,
        EnemyType type,
        RegionType region,
        int baseHP,
        int basePower,
        int baseSpeed,
        int baseDefense = 3,
        double baseCriticalChance = 0.05,
        decimal baseFidelisDrop = 1.0m,
        double beerDropChance = 0.1,
        double shotDropChance = 0.05,
        string? spritePath = null,
        int? bossStageNumber = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        return new StageEnemy
        {
            Name = name,
            Type = type,
            Region = region,
            BaseHP = baseHP,
            BasePower = basePower,
            BaseSpeed = baseSpeed,
            BaseDefense = baseDefense,
            BaseCriticalChance = baseCriticalChance,
            BaseFidelisDrop = baseFidelisDrop,
            BeerDropChance = beerDropChance,
            ShotDropChance = shotDropChance,
            SpritePath = spritePath,
            BossStageNumber = bossStageNumber
        };
    }

    /// <summary>
    /// Calculates scaled HP based on stage number
    /// Stats scale by 5% per stage
    /// </summary>
    public int GetScaledHP(int stageNumber)
    {
        var scaleFactor = 1.0 + (stageNumber - 1) * 0.05;
        return (int)(BaseHP * scaleFactor);
    }

    /// <summary>
    /// Calculates scaled power based on stage number
    /// </summary>
    public int GetScaledPower(int stageNumber)
    {
        var scaleFactor = 1.0 + (stageNumber - 1) * 0.05;
        return (int)(BasePower * scaleFactor);
    }

    /// <summary>
    /// Calculates scaled speed based on stage number
    /// </summary>
    public int GetScaledSpeed(int stageNumber)
    {
        var scaleFactor = 1.0 + (stageNumber - 1) * 0.03; // Slower scaling for speed
        return (int)(BaseSpeed * scaleFactor);
    }

    /// <summary>
    /// Calculates scaled defense based on stage number
    /// </summary>
    public int GetScaledDefense(int stageNumber)
    {
        var scaleFactor = 1.0 + (stageNumber - 1) * 0.04; // 4% per stage
        return (int)(BaseDefense * scaleFactor);
    }

    /// <summary>
    /// Calculates scaled Fidelis drop based on stage number
    /// </summary>
    public decimal GetScaledFidelisDrop(int stageNumber)
    {
        var scaleFactor = 1.0m + (stageNumber - 1) * 0.02m;
        return Math.Round(BaseFidelisDrop * scaleFactor, 2);
    }
}
