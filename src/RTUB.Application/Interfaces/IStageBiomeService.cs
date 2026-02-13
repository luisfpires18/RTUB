using RTUB.Application.Configuration;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing stage biomes and enemy sprite selection
/// </summary>
public interface IStageBiomeService
{
    /// <summary>
    /// Gets the biome for a given stage number
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>The biome name (e.g., "Forest", "Desert")</returns>
    string GetBiomeForStage(int stageNumber);

    /// <summary>
    /// Gets the number of enemies for a given stage
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>Number of enemies (1-5)</returns>
    int GetEnemyCountForStage(int stageNumber);

    /// <summary>
    /// Determines if a stage is a boss stage
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>True if boss stage, false otherwise</returns>
    bool IsBossStage(int stageNumber);

    /// <summary>
    /// Gets random enemy sprite paths for a given stage
    /// Excludes boss sprites and ensures no duplicates per encounter
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <param name="count">Number of unique enemy sprites to retrieve</param>
    /// <returns>List of enemy sprite paths (relative to wwwroot)</returns>
    Task<List<string>> GetRandomEnemySpritesAsync(int stageNumber, int count);

    /// <summary>
    /// Gets random enemy sprite paths with placement info for a given stage
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <param name="count">Number of unique enemy sprites to retrieve</param>
    /// <returns>List of tuples with sprite path and placement type (0=Terrestrial, 1=Aerial)</returns>
    Task<List<(string SpritePath, int Placement)>> GetRandomEnemySpritesWithPlacementAsync(int stageNumber, int count);

    /// <summary>
    /// Gets boss sprite path for a given boss stage
    /// </summary>
    /// <param name="stageNumber">The boss stage number</param>
    /// <returns>Boss sprite path (relative to wwwroot)</returns>
    Task<string> GetBossSpriteAsync(int stageNumber);

    /// <summary>
    /// Gets boss sprite path with placement info for a given boss stage
    /// </summary>
    /// <param name="stageNumber">The boss stage number</param>
    /// <returns>Tuple of boss sprite path and placement type (0=Terrestrial, 1=Aerial)</returns>
    Task<(string SpritePath, int Placement)> GetBossSpriteWithPlacementAsync(int stageNumber);

    /// <summary>
    /// Gets the difficulty multiplier for regular enemies at a given stage based on its biome
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>Enemies difficulty multiplier (higher for later biomes)</returns>
    double GetEnemiesDifficultyMultiplier(int stageNumber);

    /// <summary>
    /// Gets the difficulty multiplier for bosses at a given stage based on its biome
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>Bosses difficulty multiplier (higher for later biomes)</returns>
    double GetBossesDifficultyMultiplier(int stageNumber);

    /// <summary>
    /// Gets the biome reward multiplier for a given stage (unified scaling).
    /// Used to scale Fidelis rewards based on biome difficulty.
    /// </summary>
    double GetRewardMultiplierForStage(int stageNumber);

    /// <summary>
    /// Computes the unified difficulty curve value for a given stage.
    /// Formula: 1 + scalingRate × (stage - 1) ^ growthExponent.
    /// </summary>
    double GetUnifiedDifficultyCurve(int stageNumber);

    /// <summary>
    /// Computes the unified reward curve value for a given stage.
    /// Formula: 1 + scalingRate × (stage - 1) ^ growthExponent.
    /// </summary>
    double GetUnifiedRewardCurve(int stageNumber);

    /// <summary>
    /// Gets the background image path for a given stage number
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>Background image path (relative to wwwroot)</returns>
    string GetBackgroundForStage(int stageNumber);

    /// <summary>
    /// Calculates scaled enemy stats for a given stage
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <param name="baseHp">Base HP value</param>
    /// <param name="baseDamage">Base damage value</param>
    /// <param name="isBoss">Whether this is a boss enemy</param>
    /// <returns>Tuple of (scaledHp, scaledDamage)</returns>
    (int hp, int damage) CalculateScaledStats(int stageNumber, int baseHp, int baseDamage, bool isBoss);
}
