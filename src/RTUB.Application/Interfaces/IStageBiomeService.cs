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
    /// Gets boss sprite path and placement for a given boss stage
    /// </summary>
    /// <param name="stageNumber">The boss stage number</param>
    /// <returns>Tuple of (sprite path, placement type: 0=Terrestrial, 1=Aerial)</returns>
    Task<(string SpritePath, int Placement)> GetBossSpriteWithPlacementAsync(int stageNumber);

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

    /// <summary>
    /// Gets the difficulty multiplier for a given stage number based on its biome.
    /// Forest = 1.0 (default), subsequent biomes are progressively harder.
    /// </summary>
    /// <param name="stageNumber">The stage number</param>
    /// <returns>The difficulty multiplier (1.0+)</returns>
    double GetDifficultyMultiplier(int stageNumber);
}
