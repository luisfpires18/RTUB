using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a battle in Stage Mode between a player and a stage enemy
/// </summary>
public class StageBattle : BaseEntity
{
    /// <summary>
    /// The player's character ID
    /// </summary>
    [Required]
    public int CharacterId { get; set; }

    /// <summary>
    /// The stage number where this battle occurred
    /// </summary>
    [Required]
    public int StageNumber { get; set; }

    /// <summary>
    /// The enemy template ID used for this battle
    /// </summary>
    public int? StageEnemyId { get; set; }

    /// <summary>
    /// Type of enemy fought
    /// </summary>
    public EnemyType EnemyType { get; set; } = EnemyType.Normal;

    /// <summary>
    /// Region where the battle took place
    /// </summary>
    public RegionType Region { get; set; } = RegionType.Forest;

    /// <summary>
    /// Name of the enemy fought
    /// </summary>
    [Required]
    [StringLength(100)]
    public string EnemyName { get; set; } = string.Empty;

    /// <summary>
    /// RNG seed for deterministic combat
    /// </summary>
    [Required]
    public int Seed { get; set; }

    /// <summary>
    /// Outcome of the battle
    /// </summary>
    [Required]
    public BattleOutcome Outcome { get; set; }

    /// <summary>
    /// XP reward for the player
    /// </summary>
    public int XPReward { get; set; } = 0;

    /// <summary>
    /// Fidelis reward for the player
    /// </summary>
    public decimal FidelisReward { get; set; } = 0m;

    /// <summary>
    /// Number of beers dropped
    /// </summary>
    public int BeersDropped { get; set; } = 0;

    /// <summary>
    /// Number of shots dropped
    /// </summary>
    public int ShotsDropped { get; set; } = 0;

    /// <summary>
    /// Replay JSON data
    /// </summary>
    public string ReplayJson { get; set; } = string.Empty;

    // Navigation
    public virtual Character Character { get; set; } = null!;
    public virtual StageEnemy? StageEnemy { get; set; }

    // Private constructor for EF Core
    private StageBattle() { }

    /// <summary>
    /// Factory method to create a new stage battle record
    /// </summary>
    public static StageBattle Create(
        int characterId,
        int stageNumber,
        int? stageEnemyId,
        EnemyType enemyType,
        RegionType region,
        string enemyName,
        int seed,
        BattleOutcome outcome)
    {
        if (characterId <= 0)
            throw new ArgumentException("Character ID must be greater than 0", nameof(characterId));
        if (stageNumber <= 0)
            throw new ArgumentException("Stage number must be greater than 0", nameof(stageNumber));
        if (string.IsNullOrWhiteSpace(enemyName))
            throw new ArgumentException("Enemy name is required", nameof(enemyName));

        return new StageBattle
        {
            CharacterId = characterId,
            StageNumber = stageNumber,
            StageEnemyId = stageEnemyId,
            EnemyType = enemyType,
            Region = region,
            EnemyName = enemyName,
            Seed = seed,
            Outcome = outcome,
            XPReward = 0,
            FidelisReward = 0m,
            BeersDropped = 0,
            ShotsDropped = 0,
            ReplayJson = string.Empty
        };
    }

    /// <summary>
    /// Sets the rewards for the battle
    /// </summary>
    public void SetRewards(int xp, decimal fidelis, int beers = 0, int shots = 0)
    {
        if (xp < 0)
            throw new ArgumentException("XP cannot be negative", nameof(xp));
        if (fidelis < 0)
            throw new ArgumentException("Fidelis cannot be negative", nameof(fidelis));

        XPReward = xp;
        FidelisReward = fidelis;
        BeersDropped = beers;
        ShotsDropped = shots;
    }

    /// <summary>
    /// Sets the replay JSON data
    /// </summary>
    public void SetReplay(string replayJson)
    {
        if (string.IsNullOrWhiteSpace(replayJson))
            throw new ArgumentException("Replay JSON cannot be null or empty", nameof(replayJson));

        ReplayJson = replayJson;
    }
}
