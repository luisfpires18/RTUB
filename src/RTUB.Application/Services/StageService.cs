using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing Stage Mode
/// Handles stage progression, battles, and rewards
/// </summary>
public class StageService : IStageService
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IStageBattleRepository _stageBattleRepository;
    private readonly IStageEnemyRepository _stageEnemyRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<StageService> _logger;
    private readonly MyTunoScalingConfiguration _myTunoScalingConfig;

    public StageService(
        IStageProgressRepository stageProgressRepository,
        IStageBattleRepository stageBattleRepository,
        IStageEnemyRepository stageEnemyRepository,
        ICharacterRepository characterRepository,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<StageService> logger,
        IOptions<MyTunoScalingConfiguration> myTunoScalingConfig)
    {
        _stageProgressRepository = stageProgressRepository;
        _stageBattleRepository = stageBattleRepository;
        _stageEnemyRepository = stageEnemyRepository;
        _characterRepository = characterRepository;
        _combatEngine = combatEngine;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _myTunoScalingConfig = myTunoScalingConfig.Value;
    }

    /// <summary>
    /// Gets or creates stage progress for a user
    /// </summary>
    public async Task<StageProgress> GetOrCreateStageProgressAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var progress = await _stageProgressRepository.GetByUserIdAsync(userId);
        if (progress != null)
        {
            return progress;
        }

        _logger.LogInformation("Creating new stage progress for user {UserId}", userId);
        progress = StageProgress.Create(userId);
        await _stageProgressRepository.AddAsync(progress);

        return progress;
    }

    /// <summary>
    /// Gets stage progress for a user
    /// </summary>
    public async Task<StageProgress?> GetStageProgressAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return await _stageProgressRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Gets the enemy for the current stage
    /// </summary>
    public async Task<StageEnemy?> GetCurrentStageEnemyAsync(StageProgress stageProgress)
    {
        if (stageProgress == null)
            throw new ArgumentNullException(nameof(stageProgress));

        var enemyType = StageProgress.GetEnemyTypeForStage(stageProgress.CurrentStage);
        var region = stageProgress.CurrentRegion;

        return await _stageEnemyRepository.GetRandomEnemyAsync(enemyType, region);
    }

    /// <summary>
    /// Executes a battle on the current stage
    /// </summary>
    public async Task<StageBattle> ExecuteStageBattleAsync(int characterId)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new Core.Exceptions.EntityNotFoundException(nameof(Character), characterId);

        if (!character.IsAlive())
            throw new InvalidOperationException("Personagem derrotado. Precisa de reviver antes de lutar.");

        var stageProgress = await GetOrCreateStageProgressAsync(character.UserId);
        var stageNumber = stageProgress.CurrentStage;
        var enemyType = StageProgress.GetEnemyTypeForStage(stageNumber);
        var region = stageProgress.CurrentRegion;

        // Get enemy template
        var enemyTemplate = await _stageEnemyRepository.GetRandomEnemyAsync(enemyType, region);

        // Create a temporary enemy character for combat simulation
        var stageEnemy = CreateTemporaryEnemyCharacter(enemyTemplate, stageNumber, enemyType);

        // Generate seed for deterministic combat
        var seed = GenerateSeed();

        // Run combat simulation
        var combatResult = _combatEngine.Simulate(character, stageEnemy, seed);

        // Create stage battle record
        var stageBattle = StageBattle.Create(
            characterId,
            stageNumber,
            enemyTemplate?.Id,
            enemyType,
            region,
            stageEnemy.User?.UserName ?? $"Stage {stageNumber} Enemy",
            seed,
            combatResult.Outcome);

        // Serialize replay events
        var replayJson = JsonSerializer.Serialize(combatResult.Events, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        stageBattle.SetReplay(replayJson);

        // Calculate and apply rewards
        var (xpReward, fidelisReward, beersDropped, shotsDropped) =
            await CalculateAndApplyRewardsAsync(character, stageProgress, combatResult, enemyTemplate, stageNumber);

        stageBattle.SetRewards(xpReward, fidelisReward, beersDropped, shotsDropped);

        // Persist stage battle
        await _stageBattleRepository.AddAsync(stageBattle);

        // Load the StageEnemy navigation property so it's available for sprite rendering
        if (enemyTemplate != null)
        {
            stageBattle.StageEnemy = enemyTemplate;
        }

        // Update character HP and stage progress
        await UpdateCharacterAndProgressAsync(character, stageProgress, combatResult, enemyType);

        _logger.LogInformation(
            "Stage battle completed: Character {CharacterId} on Stage {Stage}, Outcome: {Outcome}, XP: {XP}, Fidelis: {Fidelis}",
            characterId, stageNumber, combatResult.Outcome, xpReward, fidelisReward);

        return stageBattle;
    }

    /// <summary>
    /// Gets recent stage battle history
    /// </summary>
    public async Task<List<StageBattle>> GetRecentBattlesAsync(int characterId, int count = 10)
    {
        return await _stageBattleRepository.GetRecentByCharacterIdAsync(characterId, count);
    }

    /// <summary>
    /// Returns the player to their last checkpoint after defeat
    /// </summary>
    public async Task<StageProgress> ReturnToCheckpointAsync(string userId)
    {
        var stageProgress = await _stageProgressRepository.GetByUserIdAsync(userId);
        if (stageProgress == null)
            throw new Core.Exceptions.EntityNotFoundException(nameof(StageProgress), userId);

        stageProgress.ReturnToCheckpoint();
        await _stageProgressRepository.UpdateAsync(stageProgress);

        _logger.LogInformation(
            "Player {UserId} returned to checkpoint at stage {Stage}",
            userId, stageProgress.CurrentStage);

        return stageProgress;
    }

    /// <summary>
    /// Creates a temporary enemy character for combat simulation
    /// Uses scaling from config file
    /// </summary>
    private Character CreateTemporaryEnemyCharacter(StageEnemy? template, int stageNumber, EnemyType type)
    {
        var stageConfig = _myTunoScalingConfig.StageMode;
        var scaling = stageConfig.EnemyScaling;
        var baseStats = stageConfig.BaseEnemyStats;

        // Default stats if no template found
        int baseHP, basePower, baseSpeed;
        double baseCriticalChance;
        string enemyName;

        if (template != null)
        {
            baseHP = template.GetScaledHP(stageNumber);
            basePower = template.GetScaledPower(stageNumber);
            baseSpeed = template.GetScaledSpeed(stageNumber);
            baseCriticalChance = template.BaseCriticalChance;
            enemyName = template.Name;
        }
        else
        {
            // Get base stats from config based on enemy type
            var typeStats = type switch
            {
                EnemyType.Boss => baseStats.Boss,
                EnemyType.MiniBoss => baseStats.MiniBoss,
                _ => baseStats.Normal
            };

            // Scale stats based on stage number using config values
            var hpScaleFactor = 1.0 + (stageNumber - 1) * scaling.HpPerStage;
            var powerScaleFactor = 1.0 + (stageNumber - 1) * scaling.PowerPerStage;
            var speedScaleFactor = 1.0 + (stageNumber - 1) * scaling.SpeedPerStage;
            var critBonus = (stageNumber - 1) * scaling.CriticalChancePerStage;

            baseHP = (int)(typeStats.Hp * hpScaleFactor);
            basePower = (int)(typeStats.Power * powerScaleFactor);
            baseSpeed = (int)(typeStats.Speed * speedScaleFactor);
            baseCriticalChance = Math.Min(typeStats.CriticalChance + critBonus, 0.5); // Cap at 50%

            enemyName = type switch
            {
                EnemyType.Boss => $"Boss (Stage {stageNumber})",
                EnemyType.MiniBoss => $"Mini-Boss (Stage {stageNumber})",
                _ => $"Enemy (Stage {stageNumber})"
            };
        }

        return Character.CreateStageEnemy(baseHP, basePower, baseSpeed, baseCriticalChance, enemyName);
    }

    /// <summary>
    /// Calculates and applies rewards for a stage battle
    /// Rewards scale based on config values
    /// </summary>
    private async Task<(int xp, decimal fidelis, int beers, int shots)> CalculateAndApplyRewardsAsync(
        Character character,
        StageProgress stageProgress,
        CombatResult combatResult,
        StageEnemy? enemyTemplate,
        int stageNumber)
    {
        var random = Random.Shared;
        var beersDropped = 0;
        var shotsDropped = 0;
        var stageConfig = _myTunoScalingConfig.StageMode;
        var dropRates = stageConfig.DropRates;
        var fidelisRewardsConfig = stageConfig.FidelisRewards;

        if (combatResult.Outcome != BattleOutcome.AttackerWon)
        {
            // Player lost - no rewards on defeat
            return (0, 0m, 0, 0);
        }

        // Player won - full rewards using config values
        var enemyType = StageProgress.GetEnemyTypeForStage(stageNumber);
        var xpMultiplier = enemyType switch
        {
            EnemyType.Boss => stageConfig.BossXPMultiplier,
            EnemyType.MiniBoss => stageConfig.MiniBossXPMultiplier,
            _ => 1
        };

        var xpReward = stageConfig.BaseStageXP * xpMultiplier;

        // Fidelis reward from config based on enemy type
        var fidelisReward = enemyTemplate?.GetScaledFidelisDrop(stageNumber) ?? enemyType switch
        {
            EnemyType.Boss => fidelisRewardsConfig.BossWin,
            EnemyType.MiniBoss => fidelisRewardsConfig.MiniBossWin,
            _ => fidelisRewardsConfig.NormalWin
        };

        // Apply XP to character
        character.AddXP(xpReward);
        await _characterRepository.UpdateAsync(character);

        // Apply Fidelis to user
        var playerUser = await _userManager.FindByIdAsync(character.UserId);
        if (playerUser != null)
        {
            playerUser.FidelisBalance += fidelisReward;
            await _userManager.UpdateAsync(playerUser);
        }

        // Roll for drops using config drop rates
        var beerChance = enemyTemplate?.BeerDropChance ?? dropRates.BeerDropChance;
        var shotChance = enemyTemplate?.ShotDropChance ?? dropRates.ShotDropChance;

        // Bosses have higher drop rates from config multipliers
        if (enemyType == EnemyType.Boss)
        {
            beerChance *= dropRates.BossDropMultiplier;
            shotChance *= dropRates.BossDropMultiplier;
        }
        else if (enemyType == EnemyType.MiniBoss)
        {
            beerChance *= dropRates.MiniBossDropMultiplier;
            shotChance *= dropRates.MiniBossDropMultiplier;
        }

        if (random.NextDouble() < beerChance)
        {
            beersDropped = 1;
            await _inventoryRepository.AddItemAsync(character.UserId, InventoryItemType.Beer, 1);
            _logger.LogInformation("Beer dropped for user {UserId} on stage {Stage}", character.UserId, stageNumber);
        }

        if (random.NextDouble() < shotChance)
        {
            shotsDropped = 1;
            await _inventoryRepository.AddItemAsync(character.UserId, InventoryItemType.Shot, 1);
            _logger.LogInformation("Shot dropped for user {UserId} on stage {Stage}", character.UserId, stageNumber);
        }

        return (xpReward, fidelisReward, beersDropped, shotsDropped);
    }

    /// <summary>
    /// Updates character HP and stage progress after battle
    /// </summary>
    private async Task UpdateCharacterAndProgressAsync(
        Character character,
        StageProgress stageProgress,
        CombatResult combatResult,
        EnemyType enemyType)
    {
        // Update character HP
        character.CurrentHP = combatResult.AttackerFinalHP;
        await _characterRepository.UpdateAsync(character);

        if (combatResult.Outcome == BattleOutcome.AttackerWon)
        {
            // Record boss/mini-boss defeats
            if (enemyType == EnemyType.Boss)
            {
                stageProgress.RecordBossDefeat();
            }
            else if (enemyType == EnemyType.MiniBoss)
            {
                stageProgress.RecordMiniBossDefeat();
            }

            // Advance to next stage
            stageProgress.AdvanceStage();
        }

        await _stageProgressRepository.UpdateAsync(stageProgress);
    }

    /// <summary>
    /// Generates a random seed for combat simulation
    /// </summary>
    private static int GenerateSeed()
    {
        return Random.Shared.Next(int.MinValue, int.MaxValue);
    }
}
