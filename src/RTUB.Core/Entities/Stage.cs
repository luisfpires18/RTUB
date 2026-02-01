using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a stage in the My-Tuno Stage Mode
/// Each stage has progressive difficulty and rewards
/// </summary>
public class Stage : BaseEntity
{
    /// <summary>
    /// Stage number (1-12 for the 12 instruments)
    /// </summary>
    [Required]
    public int StageNumber { get; set; }
    
    /// <summary>
    /// Stage name (e.g., "Guitar Challenge")
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Stage description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Minimum character level required to attempt this stage
    /// </summary>
    [Required]
    public int RequiredLevel { get; set; }
    
    /// <summary>
    /// Enemy configuration key for this stage (used in scaling config)
    /// </summary>
    [Required]
    public string EnemyConfigKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Instrument type rewarded on first completion
    /// </summary>
    [Required]
    public InventoryItemType RewardInstrument { get; set; }
    
    /// <summary>
    /// Base Fidelis reward for completing this stage
    /// </summary>
    [Required]
    public decimal FidelisReward { get; set; }
    
    /// <summary>
    /// Beer drop chance (0.0 to 1.0)
    /// </summary>
    [Required]
    public double BeerDropChance { get; set; }
    
    /// <summary>
    /// Shot drop chance (0.0 to 1.0)
    /// </summary>
    [Required]
    public double ShotDropChance { get; set; }
    
    /// <summary>
    /// Is this stage currently active/available?
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    // Private constructor for EF Core
    private Stage() { }

    /// <summary>
    /// Factory method to create a new stage
    /// </summary>
    public static Stage Create(
        int stageNumber,
        string name,
        int requiredLevel,
        string enemyConfigKey,
        InventoryItemType rewardInstrument,
        decimal fidelisReward,
        double beerDropChance = 0.2,
        double shotDropChance = 0.1)
    {
        if (stageNumber <= 0)
            throw new ArgumentException("Stage number must be greater than 0", nameof(stageNumber));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stage name is required", nameof(name));
        if (requiredLevel < 0)
            throw new ArgumentException("Required level cannot be negative", nameof(requiredLevel));
        if (string.IsNullOrWhiteSpace(enemyConfigKey))
            throw new ArgumentException("Enemy config key is required", nameof(enemyConfigKey));
        if (fidelisReward < 0)
            throw new ArgumentException("Fidelis reward cannot be negative", nameof(fidelisReward));
        if (beerDropChance < 0 || beerDropChance > 1)
            throw new ArgumentException("Beer drop chance must be between 0 and 1", nameof(beerDropChance));
        if (shotDropChance < 0 || shotDropChance > 1)
            throw new ArgumentException("Shot drop chance must be between 0 and 1", nameof(shotDropChance));

        return new Stage
        {
            StageNumber = stageNumber,
            Name = name,
            RequiredLevel = requiredLevel,
            EnemyConfigKey = enemyConfigKey,
            RewardInstrument = rewardInstrument,
            FidelisReward = fidelisReward,
            BeerDropChance = beerDropChance,
            ShotDropChance = shotDropChance,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
