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
/// Stage battles are NOT persisted - only progress is tracked
/// </summary>
public class StageService : IStageService
{
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IStageEnemyRepository _stageEnemyRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<StageService> _logger;
    private readonly MyTunoScalingConfiguration _myTunoScalingConfig;
    private readonly IStageBiomeService _biomeService;

    public StageService(
        IStageProgressRepository stageProgressRepository,
        IStageEnemyRepository stageEnemyRepository,
        ICharacterRepository characterRepository,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<StageService> logger,
        IOptions<MyTunoScalingConfiguration> myTunoScalingConfig,
        IStageBiomeService biomeService)
    {
        _stageProgressRepository = stageProgressRepository;
        _stageEnemyRepository = stageEnemyRepository;
        _characterRepository = characterRepository;
        _combatEngine = combatEngine;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _myTunoScalingConfig = myTunoScalingConfig.Value;
        _biomeService = biomeService;
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
    public async Task<StageBattleResult> ExecuteStageBattleAsync(int characterId)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new Core.Exceptions.EntityNotFoundException(nameof(Character), characterId);

        if (!character.IsAlive())
            throw new InvalidOperationException("Personagem derrotado. Precisa de reviver antes de lutar.");

        var stageProgress = await GetOrCreateStageProgressAsync(character.UserId);
        
        // Check if shot buff is active - in stage mode, buff lasts until death
        var hasShotBuff = character.ShotBuffBattlesRemaining > 0;
        var combatCharacter = hasShotBuff 
            ? Character.CreateShotBuffedCopy(character) 
            : character;
        
        var user = await _userManager.FindByIdAsync(character.UserId);
        _logger.LogInformation("Stage mode started by {UserName}{BuffStatus}", 
            user?.UserName ?? character.UserId,
            hasShotBuff ? " (with shot buff)" : "");
        var stageNumber = stageProgress.CurrentStage;
        var enemyType = StageProgress.GetEnemyTypeForStage(stageNumber);
        var region = stageProgress.CurrentRegion;

        // Get enemy count for this stage (e.g., stage 1 = 1 enemy, stage 9 = 5 enemies)
        var enemyCount = _biomeService.GetEnemyCountForStage(stageNumber);
        
        // Get biome name for sprite selection
        var biomeName = _biomeService.GetBiomeForStage(stageNumber);
        
        // Create multiple enemy characters based on stage rules
        var enemies = new List<Character>();
        var enemyTemplateIds = new List<int?>();
        var enemySpritePaths = new List<string>();
        
        // Get all enemy sprites at once
        List<string> spritePaths;
        if (enemyType == EnemyType.Boss)
        {
            var bossSprite = await _biomeService.GetBossSpriteAsync(stageNumber);
            spritePaths = Enumerable.Repeat(bossSprite, enemyCount).ToList();
        }
        else
        {
            spritePaths = await _biomeService.GetRandomEnemySpritesAsync(stageNumber, enemyCount);
        }
        
        for (int i = 0; i < enemyCount; i++)
        {
            // Get random enemy template for variety
            var enemyTemplate = await _stageEnemyRepository.GetRandomEnemyAsync(enemyType, region);
            enemyTemplateIds.Add(enemyTemplate?.Id);
            
            // Use the sprite path from the biome service
            enemySpritePaths.Add(spritePaths[i]);
            
            // Create temporary enemy character with scaled stats
            var enemy = CreateTemporaryEnemyCharacter(enemyTemplate, stageNumber, enemyType);
            enemy.User = new ApplicationUser { UserName = $"{biomeName} #{i + 1}" };
            enemies.Add(enemy);
        }

        // Generate seed for deterministic combat
        var seed = GenerateSeed();

        // Run multi-enemy combat simulation (using buffed character if shot buff active)
        CombatResult combatResult;
        if (enemies.Count == 1)
        {
            // Single enemy - use standard combat
            combatResult = _combatEngine.Simulate(combatCharacter, enemies[0], seed);
        }
        else
        {
            // Multiple enemies - use multi-enemy combat
            combatResult = _combatEngine.SimulateMultiEnemy(combatCharacter, enemies, seed);
        }

        var enemyName = enemies.Count == 1 
            ? enemies[0].User?.UserName ?? $"Stage {stageNumber} Enemy" 
            : $"{enemies.Count} Enemies";

        // Serialize replay events with enemy sprite paths and stats
        var battleData = new
        {
            Events = combatResult.Events,
            EnemyCount = enemies.Count,
            EnemySprites = enemySpritePaths,
            BiomeName = biomeName,
            EnemyStats = enemies.Select(e => new
            {
                HP = e.TotalHP,
                Power = e.TotalPower,
                Defense = e.TotalDefense,
                Speed = e.TotalSpeed
            }).ToList()
        };
        
        var replayJson = JsonSerializer.Serialize(battleData, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // Calculate and apply rewards (multiply by enemy count)
        var (xpReward, fidelisReward, beersDropped, shotsDropped) =
            await CalculateAndApplyRewardsAsync(character, stageProgress, combatResult, enemies.FirstOrDefault(), stageNumber, enemyCount);

        // Update character HP and stage progress (entire stage complete after beating all enemies)
        await UpdateCharacterAndProgressAsync(character, stageProgress, combatResult, enemyType, enemyCount, hasShotBuff);

        // Return battle result DTO (not persisted)
        return new StageBattleResult
        {
            BattleId = Guid.NewGuid(),
            CharacterId = characterId,
            StageNumber = stageNumber,
            EnemyType = enemyType,
            Region = region,
            EnemyName = enemyName,
            Seed = seed,
            Outcome = combatResult.Outcome,
            XPReward = xpReward,
            FidelisReward = fidelisReward,
            BeersDropped = beersDropped,
            ShotsDropped = shotsDropped,
            ReplayJson = replayJson,
            PlayerFinalHP = combatResult.AttackerFinalHP
        };
    }

    /// <summary>
    /// Gets the number of enemies remaining in the current stage
    /// </summary>
    public async Task<int> GetRemainingEnemiesInStageAsync(string userId)
    {
        var stageProgress = await GetOrCreateStageProgressAsync(userId);
        var totalEnemies = _biomeService.GetEnemyCountForStage(stageProgress.CurrentStage);
        var defeated = stageProgress.EnemiesDefeatedInCurrentStage;
        return Math.Max(0, totalEnemies - defeated);
    }

    /// <summary>
    /// Checks if the current stage is complete
    /// </summary>
    public async Task<bool> IsStageCompleteAsync(string userId)
    {
        var remaining = await GetRemainingEnemiesInStageAsync(userId);
        return remaining == 0;
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

        return stageProgress;
    }

    /// <summary>
    /// Cancels a stage run in progress.
    /// Restores the character's HP to the specified value and resets stage progress.
    /// Used when user exits mid-run without completing it.
    /// </summary>
    public async Task<bool> CancelRunAsync(int characterId, int restoreHp, int restoreStage)
    {
        try
        {
            var character = await _characterRepository.GetByIdAsync(characterId);
            if (character == null)
            {
                _logger.LogWarning("CancelRunAsync: Character {CharacterId} not found", characterId);
                return false;
            }

            var stageProgress = await _stageProgressRepository.GetByUserIdAsync(character.UserId);
            if (stageProgress == null)
            {
                _logger.LogWarning("CancelRunAsync: Stage progress for user {UserId} not found", character.UserId);
                return false;
            }

            // Restore character HP
            character.CurrentHP = restoreHp;
            await _characterRepository.UpdateAsync(character);

            // Reset stage progress to the restore point
            if (stageProgress.CurrentStage != restoreStage)
            {
                stageProgress.CurrentStage = restoreStage;
                stageProgress.EnemiesDefeatedInCurrentStage = 0;
                await _stageProgressRepository.UpdateAsync(stageProgress);
            }

            _logger.LogInformation("CancelRunAsync: Restored character {CharacterId} HP to {HP} and stage to {Stage}",
                characterId, restoreHp, restoreStage);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling run for character {CharacterId}", characterId);
            return false;
        }
    }

    /// <summary>
    /// Creates a temporary enemy character for combat simulation
    /// Uses biome service for stat scaling
    /// </summary>
    private Character CreateTemporaryEnemyCharacter(StageEnemy? template, int stageNumber, EnemyType type)
    {
        var stageConfig = _myTunoScalingConfig.StageMode;
        var baseStats = stageConfig.BaseEnemyStats;
        var isBoss = _biomeService.IsBossStage(stageNumber);

        // Default stats if no template found
        int baseHP, basePower, baseSpeed, baseDefense;
        double baseCriticalChance;
        string enemyName;

        if (template != null)
        {
            baseHP = template.GetScaledHP(stageNumber);
            basePower = template.GetScaledPower(stageNumber);
            baseSpeed = template.GetScaledSpeed(stageNumber);
            baseDefense = template.GetScaledDefense(stageNumber);
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

            // Use biome service for stat scaling
            var (scaledHP, scaledPower) = _biomeService.CalculateScaledStats(
                stageNumber, 
                typeStats.Hp, 
                typeStats.Power, 
                isBoss);

            baseHP = scaledHP;
            basePower = scaledPower;
            
            // Speed scaling (using simple formula for now)
            var scaling = stageConfig.EnemyScaling;
            var speedScaleFactor = 1.0 + (stageNumber - 1) * scaling.SpeedPerStage;
            baseSpeed = (int)(typeStats.Speed * speedScaleFactor);

            // Defense scaling
            var defenseScaleFactor = 1.0 + (stageNumber - 1) * scaling.DefensePerStage;
            baseDefense = (int)(typeStats.Defense * defenseScaleFactor);
            
            var critBonus = (stageNumber - 1) * scaling.CriticalChancePerStage;
            baseCriticalChance = Math.Min(typeStats.CriticalChance + critBonus, 0.5); // Cap at 50%

            var biomeName = _biomeService.GetBiomeForStage(stageNumber);
            enemyName = type switch
            {
                EnemyType.Boss => $"{biomeName} Boss (Stage {stageNumber})",
                EnemyType.MiniBoss => $"{biomeName} Mini-Boss (Stage {stageNumber})",
                _ => $"{biomeName} Enemy (Stage {stageNumber})"
            };
        }

        return Character.CreateStageEnemy(baseHP, basePower, baseSpeed, baseDefense, baseCriticalChance, enemyName);
    }

    /// <summary>
    /// Calculates and applies rewards for a stage battle
    /// Rewards scale based on config values
    /// </summary>
    private async Task<(int xp, decimal fidelis, int beers, int shots)> CalculateAndApplyRewardsAsync(
        Character character,
        StageProgress stageProgress,
        CombatResult combatResult,
        Character? enemyTemplate,
        int stageNumber,
        int enemyCount = 1)
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

        // Player won - full rewards using config values (multiplied by enemy count)
        var enemyType = StageProgress.GetEnemyTypeForStage(stageNumber);
        var xpMultiplier = enemyType switch
        {
            EnemyType.Boss => stageConfig.BossXPMultiplier,
            EnemyType.MiniBoss => stageConfig.MiniBossXPMultiplier,
            _ => 1
        };

        // Stage scaling: +5% per stage number (stage 1 = 1.05x, stage 10 = 1.50x, stage 100 = 6x)
        var stageScaling = 1.0 + (stageNumber * 0.05);

        var xpReward = (int)Math.Round(stageConfig.BaseStageXP * xpMultiplier * enemyCount * stageScaling);

        // Fidelis reward from config based on enemy type (multiplied by enemy count and stage scaling)
        var baseFidelis = enemyType switch
        {
            EnemyType.Boss => fidelisRewardsConfig.BossWin,
            EnemyType.MiniBoss => fidelisRewardsConfig.MiniBossWin,
            _ => fidelisRewardsConfig.NormalWin
        };
        var fidelisReward = Math.Round(baseFidelis * enemyCount * (decimal)stageScaling, 2);

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

        // Roll for drops using config drop rates (each enemy can drop)
        var beerChance = dropRates.BeerDropChance;
        var shotChance = dropRates.ShotDropChance;

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

        // Each enemy has a chance to drop items
        for (int i = 0; i < enemyCount; i++)
        {
            if (random.NextDouble() < beerChance)
            {
                beersDropped++;
            }

            if (random.NextDouble() < shotChance)
            {
                shotsDropped++;
            }
        }

        // Add all dropped items to inventory
        if (beersDropped > 0)
        {
            await _inventoryRepository.AddItemAsync(character.UserId, InventoryItemType.Beer, beersDropped);
        }

        if (shotsDropped > 0)
        {
            await _inventoryRepository.AddItemAsync(character.UserId, InventoryItemType.Shot, shotsDropped);
        }

        return (xpReward, fidelisReward, beersDropped, shotsDropped);
    }

    /// <summary>
    /// Updates character HP and stage progress after battle
    /// Only persists StageProgress when there's a new record:
    /// - HighestStage increased (player beat their previous best)
    /// - EndlessModeUnlocked changed (beat stage 10000)
    /// No writes when player dies at a stage below their record.
    /// </summary>
    private async Task UpdateCharacterAndProgressAsync(
        Character character,
        StageProgress stageProgress,
        CombatResult combatResult,
        EnemyType enemyType,
        int enemyCount = 1,
        bool hasShotBuff = false)
    {
        // Track if we need to persist (only when there's a new record)
        var previousHighestStage = stageProgress.HighestStage;
        var previousEndlessModeUnlocked = stageProgress.EndlessModeUnlocked;

        // Update character HP from combat result
        character.CurrentHP = combatResult.AttackerFinalHP;
        
        // Handle shot buff - in stage mode, buff lasts until death
        if (hasShotBuff)
        {
            if (combatResult.Outcome == BattleOutcome.DefenderWon)
            {
                // Player died - buff is consumed, scale HP down (will be 0 anyway)
                character.ShotBuffBattlesRemaining = 0;
            }
        }
        
        await _characterRepository.UpdateAsync(character);

        if (combatResult.Outcome == BattleOutcome.AttackerWon)
        {
            // Record all enemies defeated (this is a full stage battle with all enemies at once)
            for (int i = 0; i < enemyCount; i++)
            {
                stageProgress.RecordEnemyDefeat();
            }

            // Record boss/mini-boss defeats
            if (enemyType == EnemyType.Boss)
            {
                stageProgress.RecordBossDefeat();
            }
            else if (enemyType == EnemyType.MiniBoss)
            {
                stageProgress.RecordMiniBossDefeat();
            }

            // Check if all enemies in this stage are defeated (they should all be defeated now)
            var totalEnemies = _biomeService.GetEnemyCountForStage(stageProgress.CurrentStage);
            if (stageProgress.EnemiesDefeatedInCurrentStage >= totalEnemies)
            {
                // All enemies defeated - advance to next stage
                stageProgress.AdvanceStage();
            }
        }

        // Only persist when there's a new record worth saving
        var hasNewRecord = stageProgress.HighestStage > previousHighestStage ||
                           stageProgress.EndlessModeUnlocked != previousEndlessModeUnlocked;

        if (hasNewRecord)
        {
            await _stageProgressRepository.UpdateAsync(stageProgress);
            _logger.LogInformation("StageProgress persisted - new record for user {UserId}: HighestStage={HighestStage}, EndlessModeUnlocked={EndlessModeUnlocked}",
                stageProgress.UserId, stageProgress.HighestStage, stageProgress.EndlessModeUnlocked);
        }
    }

    /// <summary>
    /// Generates a random seed for combat simulation
    /// </summary>
    private static int GenerateSeed()
    {
        return Random.Shared.Next(int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Gets biome information for a stage (for UI display)
    /// </summary>
    public string GetBiomeNameForStage(int stageNumber)
    {
        return _biomeService.GetBiomeForStage(stageNumber);
    }

    /// <summary>
    /// Gets the number of enemies for a stage (for UI display)
    /// </summary>
    public int GetEnemyCountForStage(int stageNumber)
    {
        return _biomeService.GetEnemyCountForStage(stageNumber);
    }

    /// <summary>
    /// Checks if a stage is a boss stage (for UI display)
    /// </summary>
    public bool IsBossStage(int stageNumber)
    {
        return _biomeService.IsBossStage(stageNumber);
    }
}
