using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Result of a survive mode level attempt.
/// Contains the reward data and game state to be processed after each level.
/// </summary>
public class SurviveModeLevelResult
{
    /// <summary>
    /// Unique identifier for this level attempt.
    /// </summary>
    public Guid AttemptId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The player's character ID.
    /// </summary>
    public int CharacterId { get; set; }

    /// <summary>
    /// The survive level number (1 = Forest, 2 = Swamp, etc.)
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// The biome/region for this level.
    /// </summary>
    public RegionType Region { get; set; }

    /// <summary>
    /// The biome name (e.g., "Forest", "Swamp").
    /// </summary>
    public string BiomeName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the player survived the full timer.
    /// </summary>
    public bool Survived { get; set; }

    /// <summary>
    /// How long the player survived in seconds.
    /// </summary>
    public double SurvivalTimeSeconds { get; set; }

    /// <summary>
    /// The required survival time in seconds to beat this level.
    /// </summary>
    public double RequiredTimeSeconds { get; set; }

    /// <summary>
    /// Number of enemies killed during this level.
    /// </summary>
    public int EnemiesKilled { get; set; }

    /// <summary>
    /// XP reward earned for this level.
    /// </summary>
    public int XPReward { get; set; }

    /// <summary>
    /// Fidelis reward earned for this level.
    /// </summary>
    public decimal FidelisReward { get; set; }

    /// <summary>
    /// Number of finos dropped.
    /// </summary>
    public int FinosDropped { get; set; }

    /// <summary>
    /// Number of canecas dropped.
    /// </summary>
    public int CanecasDropped { get; set; }

    /// <summary>
    /// Number of cigarros dropped.
    /// </summary>
    public int CigarrosDropped { get; set; }

    /// <summary>
    /// Number of canhões dropped.
    /// </summary>
    public int CanhaosDropped { get; set; }

    /// <summary>
    /// Number of shots dropped.
    /// </summary>
    public int ShotsDropped { get; set; }

    /// <summary>
    /// Number of penalties dropped.
    /// </summary>
    public int PenaltiesDropped { get; set; }

    /// <summary>
    /// Instrument parts dropped.
    /// </summary>
    public List<InventoryItemType> InstrumentPartsDropped { get; set; } = new();

    /// <summary>
    /// FITAB tokens dropped.
    /// </summary>
    public int FitabDropped { get; set; }

    /// <summary>
    /// Number of leitões dropped.
    /// </summary>
    public int LeitaoDropped { get; set; }
}
