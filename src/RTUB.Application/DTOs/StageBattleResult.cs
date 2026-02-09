using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Result of a stage battle including outcome, rewards, and replay data
/// This is returned directly to the client - stage battles are not persisted to database
/// </summary>
public class StageBattleResult
{
    /// <summary>
    /// Unique identifier for this battle
    /// </summary>
    public Guid BattleId { get; set; }

    /// <summary>
    /// The player's character ID
    /// </summary>
    public int CharacterId { get; set; }

    /// <summary>
    /// The stage number where this battle occurred
    /// </summary>
    public int StageNumber { get; set; }

    /// <summary>
    /// Type of enemy fought
    /// </summary>
    public EnemyType EnemyType { get; set; }

    /// <summary>
    /// Region where the battle took place
    /// </summary>
    public RegionType Region { get; set; }

    /// <summary>
    /// Name of the enemy/enemies fought
    /// </summary>
    public string EnemyName { get; set; } = string.Empty;

    /// <summary>
    /// RNG seed for deterministic combat
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// Outcome of the battle
    /// </summary>
    public BattleOutcome Outcome { get; set; }

    /// <summary>
    /// XP reward for the player
    /// </summary>
    public int XPReward { get; set; }

    /// <summary>
    /// Fidelis reward for the player
    /// </summary>
    public decimal FidelisReward { get; set; }

    /// <summary>
    /// Number of beers dropped
    /// </summary>
    public int BeersDropped { get; set; }

    /// <summary>
    /// Number of shots dropped
    /// </summary>
    public int ShotsDropped { get; set; }

    /// <summary>
    /// Instrument parts dropped (rare drops for crafting).
    /// Each entry is the InventoryItemType of the instrument part.
    /// </summary>
    public List<InventoryItemType> InstrumentPartsDropped { get; set; } = new();

    /// <summary>
    /// Equipment pieces dropped (rare drops for crafting).
    /// Each entry is the InventoryItemType of the equipment slot.
    /// </summary>
    public List<InventoryItemType> EquipmentDropped { get; set; } = new();

    /// <summary>
    /// Number of FITAB dropped (very rare currency for Boss Mode entry).
    /// </summary>
    public int FitabDropped { get; set; }

    /// <summary>
    /// Replay JSON data for battle animation
    /// </summary>
    public string ReplayJson { get; set; } = string.Empty;

    /// <summary>
    /// Player's final HP after the battle
    /// </summary>
    public int PlayerFinalHP { get; set; }
}
