using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Application.Configuration;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for Stage Mode operations
/// </summary>
public interface IStageService
{
    /// <summary>
    /// Get all available stages for a character based on level
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available stages</returns>
    Task<List<Stage>> GetAvailableStagesAsync(int characterId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a character's progress on all stages
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of character stage progress records</returns>
    Task<List<CharacterStageProgress>> GetCharacterProgressAsync(int characterId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Start a battle against a stage
    /// Creates a battle using the existing BattleService and combat engine
    /// </summary>
    /// <param name="characterId">The character ID</param>
    /// <param name="stageNumber">The stage number (1-12)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created battle</returns>
    Task<Battle> StartStageBattleAsync(int characterId, int stageNumber, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Complete a stage battle and distribute rewards
    /// Handles: Fidelis, XP, instrument drop (first time), beer/shot drops
    /// </summary>
    /// <param name="battleId">The battle ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CompleteStageBattleAsync(int battleId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get instrument stats bonus from configuration
    /// </summary>
    /// <param name="instrumentType">The instrument type</param>
    /// <returns>The instrument stat bonus if found, otherwise null</returns>
    InstrumentStatBonus? GetInstrumentStats(InventoryItemType instrumentType);
}
