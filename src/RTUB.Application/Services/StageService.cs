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

        var enemyType = GetEnemyTypeForStageFromConfig(stageProgress.CurrentStage);
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
        
        var stageNumber = stageProgress.CurrentStage;
        var enemyType = GetEnemyTypeForStageFromConfig(stageNumber);
        var region = stageProgress.CurrentRegion;

        // Get enemy count for this stage (e.g., stage 1 = 1 enemy, stage 9 = 5 enemies)
        var enemyCount = _biomeService.GetEnemyCountForStage(stageNumber);
        
        // Get biome name for sprite selection
        var biomeName = _biomeService.GetBiomeForStage(stageNumber);
        
        // Create multiple enemy characters based on stage rules
        var enemies = new List<Character>();
        var enemyTemplateIds = new List<int?>();
        var enemySpritePaths = new List<string>();
        var enemyPlacements = new List<int>();
        
        // Get all enemy sprites with placements at once
        if (enemyType == EnemyType.Boss)
        {
            var (bossSprite, bossPlacement) = await _biomeService.GetBossSpriteWithPlacementAsync(stageNumber);
            for (int i = 0; i < enemyCount; i++)
            {
                enemySpritePaths.Add(bossSprite);
                enemyPlacements.Add(bossPlacement);
            }
        }
        else
        {
            var spritesWithPlacements = await _biomeService.GetRandomEnemySpritesWithPlacementAsync(stageNumber, enemyCount);
            foreach (var (sprite, placement) in spritesWithPlacements)
            {
                enemySpritePaths.Add(sprite);
                enemyPlacements.Add(placement);
            }
        }
        
        for (int i = 0; i < enemyCount; i++)
        {
            // Get random enemy template for variety
            var enemyTemplate = await _stageEnemyRepository.GetRandomEnemyAsync(enemyType, region);
            enemyTemplateIds.Add(enemyTemplate?.Id);
            
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

        // Serialize replay events with enemy sprite paths, placements, and stats
        var battleData = new
        {
            Events = combatResult.Events,
            EnemyCount = enemies.Count,
            EnemySprites = enemySpritePaths,
            EnemyPlacements = enemyPlacements, // 0 = Terrestrial, 1 = Aerial
            BiomeName = biomeName,
            EnemyStats = enemies.Select(e => new
            {
                Name = e.User?.UserName ?? "Enemy",
                HP = e.TotalHP,
                Power = e.TotalPower,
                Defense = e.TotalDefense,
                Speed = e.TotalSpeed,
                ActionTime = Math.Round(e.ActionTime, 1)
            }).ToList()
        };
        
        var replayJson = JsonSerializer.Serialize(battleData, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // Calculate rewards (deferred - not applied until run ends)
        var (xpReward, fidelisReward, beersDropped, shotsDropped) =
            CalculateRewardsForBattle(combatResult, stageNumber, enemyCount);

        // Update character HP and stage progress (entire stage complete after beating all enemies)
        await UpdateCharacterAndProgressAsync(character, stageProgress, combatResult, enemyType, enemyCount);

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
    public async Task<bool> CancelRunAsync(int characterId, int restoreHp, int restoreStage, int restoreShotBuffBattles = 0)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
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

                // Restore character HP and shot buff state
                character.CurrentHP = restoreHp;
                character.ShotBuffBattlesRemaining = restoreShotBuffBattles;
                await _characterRepository.UpdateAsync(character);

                // Reset stage progress to the restore point
                if (stageProgress.CurrentStage != restoreStage)
                {
                    stageProgress.CurrentStage = restoreStage;
                    stageProgress.EnemiesDefeatedInCurrentStage = 0;
                    await _stageProgressRepository.UpdateAsync(stageProgress);
                }

                return true;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        ex,
                        "CancelRunAsync: Concurrency conflict for character {CharacterId}, retrying (attempt {Attempt}/{MaxRetries})...",
                        characterId,
                        attempt + 1,
                        maxRetries);
                    // Brief delay to let the concurrent operation finish
                    await Task.Delay(100 * (attempt + 1));
                    continue;
                }

                _logger.LogError(ex, "CancelRunAsync: Failed after {MaxRetries} retries for character {CharacterId}", maxRetries, characterId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling run for character {CharacterId}", characterId);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a temporary enemy character for combat simulation
    /// Uses biome service for stat scaling and biome difficulty multiplier
    /// </summary>
    private Character CreateTemporaryEnemyCharacter(StageEnemy? template, int stageNumber, EnemyType type)
    {
        var stageConfig = _myTunoScalingConfig.StageMode;
        var baseStats = stageConfig.BaseEnemyStats;
        var isBoss = _biomeService.IsBossStage(stageNumber);
        var difficultyMultiplier = _biomeService.GetDifficultyMultiplier(stageNumber);

        // Default stats if no template found
        int baseHP, basePower, baseSpeed, baseDefense;
        double baseCriticalChance;
        string enemyName;

        if (template != null)
        {
            baseHP = (int)(template.GetScaledHP(stageNumber) * difficultyMultiplier);
            basePower = (int)(template.GetScaledPower(stageNumber) * difficultyMultiplier);
            baseSpeed = (int)(template.GetScaledSpeed(stageNumber) * difficultyMultiplier);
            baseDefense = (int)(template.GetScaledDefense(stageNumber) * difficultyMultiplier);
            baseCriticalChance = template.BaseCriticalChance;
            enemyName = template.Name;
        }
        else
        {
            // Get base stats from config based on enemy type
            var typeStats = type switch
            {
                EnemyType.Boss => baseStats.Boss,
                _ => baseStats.Normal
            };

            // Use biome service for stat scaling
            var (scaledHP, scaledPower) = _biomeService.CalculateScaledStats(
                stageNumber, 
                typeStats.Hp, 
                typeStats.Power, 
                isBoss);

            baseHP = (int)(scaledHP * difficultyMultiplier);
            basePower = (int)(scaledPower * difficultyMultiplier);
            
            // Speed scaling (using simple formula for now)
            var scaling = stageConfig.EnemyScaling;
            var speedScaleFactor = 1.0 + (stageNumber - 1) * scaling.SpeedPerStage;
            baseSpeed = (int)(typeStats.Speed * speedScaleFactor * difficultyMultiplier);

            // Defense scaling
            var defenseScaleFactor = 1.0 + (stageNumber - 1) * scaling.DefensePerStage;
            baseDefense = (int)(typeStats.Defense * defenseScaleFactor * difficultyMultiplier);
            
            var critBonus = (stageNumber - 1) * scaling.CriticalChancePerStage;
            baseCriticalChance = Math.Min(typeStats.CriticalChance + critBonus, _myTunoScalingConfig.Combat.CriticalChanceCap); // Cap from config

            var biomeName = _biomeService.GetBiomeForStage(stageNumber);
            enemyName = type switch
            {
                EnemyType.Boss => $"{biomeName} Boss (Stage {stageNumber})",
                _ => $"{biomeName} Enemy (Stage {stageNumber})"
            };
        }

        return Character.CreateStageEnemy(baseHP, basePower, baseSpeed, baseDefense, baseCriticalChance, enemyName);
    }

    /// <summary>
    /// Calculates rewards for a stage battle and applies XP to character (in-memory only, no save).
    /// Also applies Fidelis to user and adds item drops to inventory.
    /// Character changes are saved later by ApplyCharacterAndProgressUpdatesAsync to avoid double-save concurrency issues.
    /// </summary>
    /// <summary>
    /// Pure calculation of rewards for a stage battle (no side effects).
    /// Rewards are deferred and only applied when the run ends via ApplyRunRewardsAsync.
    /// </summary>
    private (int xp, decimal fidelis, int beers, int shots) CalculateRewardsForBattle(
        CombatResult combatResult,
        int stageNumber,
        int enemyCount = 1)
    {
        if (combatResult.Outcome != BattleOutcome.AttackerWon)
        {
            return (0, 0m, 0, 0);
        }

        var random = Random.Shared;
        var beersDropped = 0;
        var shotsDropped = 0;
        var stageConfig = _myTunoScalingConfig.StageMode;
        var dropRates = stageConfig.DropRates;
        var fidelisRewardsConfig = stageConfig.FidelisRewards;

        var enemyType = GetEnemyTypeForStageFromConfig(stageNumber);
        var xpMultiplier = enemyType switch
        {
            EnemyType.Boss => stageConfig.BossXPMultiplier,
            _ => 1
        };

        var stageScaling = 1.0 + (stageNumber * stageConfig.StageRewardScalingFactor);
        var xpReward = (int)Math.Round(stageConfig.BaseStageXP * xpMultiplier * enemyCount * stageScaling);

        var baseFidelis = enemyType switch
        {
            EnemyType.Boss => fidelisRewardsConfig.BossWin,
            _ => fidelisRewardsConfig.NormalWin
        };
        var fidelisReward = Math.Round(baseFidelis * enemyCount * (decimal)stageScaling, 2);

        var beerChance = dropRates.BeerDropChance;
        var shotChance = dropRates.ShotDropChance;
        if (enemyType == EnemyType.Boss)
        {
            beerChance *= dropRates.BossDropMultiplier;
            shotChance *= dropRates.BossDropMultiplier;
        }

        for (int i = 0; i < enemyCount; i++)
        {
            if (random.NextDouble() < beerChance) beersDropped++;
            if (random.NextDouble() < shotChance) shotsDropped++;
        }

        return (xpReward, fidelisReward, beersDropped, shotsDropped);
    }

    /// <summary>
    /// Applies accumulated run rewards (XP, Fidelis, item drops) when a stage run ends.
    /// Called after defeat to commit all rewards earned during the run.
    /// Not called on cancel/back — rewards are forfeited.
    /// </summary>
    public async Task ApplyRunRewardsAsync(int characterId, int xp, decimal fidelis, int beers, int shots)
    {
        if (xp <= 0 && fidelis <= 0 && beers <= 0 && shots <= 0)
            return;

        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
        {
            _logger.LogWarning("ApplyRunRewardsAsync: Character {CharacterId} not found", characterId);
            return;
        }

        // Apply XP
        if (xp > 0)
        {
            character.AddXP(xp);
            await _characterRepository.UpdateAsync(character);
        }

        // Apply Fidelis
        if (fidelis > 0)
        {
            var user = await _userManager.FindByIdAsync(character.UserId);
            if (user != null)
            {
                user.FidelisBalance += fidelis;
                await _userManager.UpdateAsync(user);
            }
        }

        // Apply item drops
        if (beers > 0)
        {
            await _inventoryRepository.AddItemAsync(character.UserId, InventoryItemType.Beer, beers);
        }
        if (shots > 0)
        {
            await _inventoryRepository.AddItemAsync(character.UserId, InventoryItemType.Shot, shots);
        }

        _logger.LogInformation(
            "Applied run rewards for character {CharacterId}: +{XP} XP, +{Fidelis} Fidelis, +{Beers} beers, +{Shots} shots",
            characterId, xp, fidelis, beers, shots);
    }

    /// <summary>
    /// Updates character HP, XP and stage progress after battle.
    /// Only persists StageProgress when there's a new record:
    /// - HighestStage increased (player beat their previous best)
    /// - EndlessModeUnlocked changed (beat stage 10000)
    /// No writes when player dies at a stage below their record.
    /// Handles concurrency exceptions by reloading entities and retrying.
    /// </summary>
    private async Task UpdateCharacterAndProgressAsync(
        Character character,
        StageProgress stageProgress,
        CombatResult combatResult,
        EnemyType enemyType,
        int enemyCount = 1)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await ApplyCharacterAndProgressUpdatesAsync(
                    character,
                    stageProgress,
                    combatResult,
                    enemyType,
                    enemyCount);
                return;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        ex,
                        "Concurrency conflict updating character/progress for character {CharacterId}, reloading and retrying (attempt {Attempt}/{MaxRetries})...",
                        character.Id,
                        attempt + 1,
                        maxRetries);

                    // Reload entities fresh from database to get current values
                    try
                    {
                        var freshCharacter = await _characterRepository.GetByIdAsync(character.Id);
                        if (freshCharacter != null)
                        {
                            // Re-apply the in-memory changes on top of fresh DB values
                            character.CurrentHP = freshCharacter.CurrentHP;
                            character.XP = freshCharacter.XP;
                            character.Level = freshCharacter.Level;
                        }

                        var freshProgress = await _stageProgressRepository.GetByUserIdAsync(character.UserId);
                        if (freshProgress != null)
                        {
                            stageProgress.HighestStage = freshProgress.HighestStage;
                            stageProgress.CurrentStage = freshProgress.CurrentStage;
                            stageProgress.EnemiesDefeatedInCurrentStage = freshProgress.EnemiesDefeatedInCurrentStage;
                            stageProgress.EndlessModeUnlocked = freshProgress.EndlessModeUnlocked;
                        }
                    }
                    catch (Exception reloadEx)
                    {
                        _logger.LogWarning(reloadEx, "Failed to reload entities for retry");
                    }

                    // Brief delay before retry to let concurrent operation finish
                    await Task.Delay(50 * (attempt + 1));
                    continue;
                }

                // Final retry failed - log error but don't fail the entire stage attempt
                // The combat result is still valid, just progress tracking failed
                _logger.LogError(
                    ex,
                    "Failed to update character progress for character {CharacterId} after {MaxRetries} retries",
                    character.Id,
                    maxRetries);
                return; // Don't rethrow - allow combat to complete even if progress tracking fails
            }
        }
    }

    /// <summary>
    /// Internal method that applies character and progress updates
    /// Separated to allow retry logic in UpdateCharacterAndProgressAsync
    /// </summary>
    private async Task ApplyCharacterAndProgressUpdatesAsync(
        Character character,
        StageProgress stageProgress,
        CombatResult combatResult,
        EnemyType enemyType,
        int enemyCount)
    {
        // Track if we need to persist (only when there's a new record)
        var previousHighestStage = stageProgress.HighestStage;
        var previousEndlessModeUnlocked = stageProgress.EndlessModeUnlocked;

        // Update character HP from combat result
        character.CurrentHP = combatResult.AttackerFinalHP;
        
        // Decrement shot buff on stage death (costs 1 charge per death)
        if (combatResult.Outcome == BattleOutcome.DefenderWon && character.ShotBuffBattlesRemaining > 0)
        {
            character.ShotBuffBattlesRemaining--;
        }
        
        await _characterRepository.UpdateAsync(character);

        if (combatResult.Outcome == BattleOutcome.AttackerWon)
        {
            // Record all enemies defeated (this is a full stage battle with all enemies at once)
            for (int i = 0; i < enemyCount; i++)
            {
                stageProgress.RecordEnemyDefeat();
            }

            // Record boss defeats
            if (enemyType == EnemyType.Boss)
            {
                stageProgress.RecordBossDefeat();
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

    /// <summary>
    /// Gets the enemy type for a stage using biome config.
    /// Uses BossEveryNStages from config instead of hardcoded values.
    /// </summary>
    private EnemyType GetEnemyTypeForStageFromConfig(int stageNumber)
    {
        // Use biome service's config-driven boss determination
        if (_biomeService.IsBossStage(stageNumber))
            return EnemyType.Boss;
        
        return EnemyType.Normal;
    }
}
