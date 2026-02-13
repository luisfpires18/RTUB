using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

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
    public async Task<StageProgress> GetOrCreateStageProgressAsync(string userId, CancellationToken cancellationToken = default)
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
    public async Task<StageProgress?> GetStageProgressAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return await _stageProgressRepository.GetByUserIdAsync(userId);
    }

    /// <summary>
    /// Gets the enemy for the current stage
    /// </summary>
    public async Task<StageEnemy?> GetCurrentStageEnemyAsync(StageProgress stageProgress, CancellationToken cancellationToken = default)
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
    public async Task<StageBattleResult> ExecuteStageBattleAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new Core.Exceptions.EntityNotFoundException(nameof(Character), characterId);

        var stageProgress = await GetOrCreateStageProgressAsync(character.UserId, cancellationToken);
        
        // Check if shot buff is active - in stage mode, buff lasts until death
        var hasShotBuff = character.ShotBuffBattlesRemaining > 0;
        var hasPenaltyBuff = character.PenaltyBuffActive > 0;
        var combatCharacter = hasShotBuff 
            ? Character.CreateShotBuffedCopy(character) 
            : character;
        if (hasPenaltyBuff)
            combatCharacter = Character.CreatePenaltyBuffedCopy(combatCharacter);
        
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
        
        // Get all enemy sprites/templates with placements in a single DB query
        // The returned StageEnemy objects double as stat templates, eliminating N+1 queries
        var enemyTemplates = new List<StageEnemy?>();
        
        if (enemyType == EnemyType.Boss)
        {
            var boss = await _stageEnemyRepository.GetBossForStageAsync(stageNumber);
            for (int i = 0; i < enemyCount; i++)
            {
                enemyTemplates.Add(boss);
                enemySpritePaths.Add(boss?.SpritePath ?? $"/sprites/games/my-tuno/enemies/{biomeName.ToLowerInvariant()}/boss_1.png");
                enemyPlacements.Add(boss != null ? (int)boss.Placement : 0);
            }
        }
        else
        {
            // Single query: GetRandomEnemiesAsync returns full StageEnemy objects
            var randomEnemies = await _stageEnemyRepository.GetRandomEnemiesAsync(enemyType, region, enemyCount);
            foreach (var e in randomEnemies)
            {
                enemyTemplates.Add(e);
                enemySpritePaths.Add(e.SpritePath ?? $"/sprites/games/my-tuno/enemies/{biomeName.ToLowerInvariant()}/wolf.png");
                enemyPlacements.Add((int)e.Placement);
            }
        }
        
        for (int i = 0; i < enemyCount; i++)
        {
            var template = i < enemyTemplates.Count ? enemyTemplates[i] : null;
            enemyTemplateIds.Add(template?.Id);
            
            // Create temporary enemy character with scaled stats
            var enemy = CreateTemporaryEnemyCharacter(template, stageNumber, enemyType);
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
        var (xpReward, fidelisReward, finosDropped, canecasDropped, cigarrosDropped, canhaosDropped, shotsDropped, penaltiesDropped, instrumentPartsDropped, equipmentDropped, fitabDropped) =
            CalculateRewardsForBattle(combatResult, stageNumber, character.Level, enemyCount, stageProgress.HighestStage);

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
            FinosDropped = finosDropped,
            CanecasDropped = canecasDropped,
            CigarrosDropped = cigarrosDropped,
            CanhaosDropped = canhaosDropped,
            ShotsDropped = shotsDropped,
            PenaltiesDropped = penaltiesDropped,
            InstrumentPartsDropped = instrumentPartsDropped,
            EquipmentDropped = equipmentDropped,
            FitabDropped = fitabDropped,
            ReplayJson = replayJson,
            PlayerFinalHP = combatResult.AttackerFinalHP
        };
    }

    /// <summary>
    /// Gets the number of enemies remaining in the current stage
    /// </summary>
    public async Task<int> GetRemainingEnemiesInStageAsync(string userId, CancellationToken cancellationToken = default)
    {
        var stageProgress = await GetOrCreateStageProgressAsync(userId, cancellationToken);
        var totalEnemies = _biomeService.GetEnemyCountForStage(stageProgress.CurrentStage);
        var defeated = stageProgress.EnemiesDefeatedInCurrentStage;
        return Math.Max(0, totalEnemies - defeated);
    }

    /// <summary>
    /// Checks if the current stage is complete
    /// </summary>
    public async Task<bool> IsStageCompleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var remaining = await GetRemainingEnemiesInStageAsync(userId, cancellationToken);
        return remaining == 0;
    }

    /// <summary>
    /// Sets the current stage for a user (checkpoint selection).
    /// Validates that the target stage is within the user's reached range.
    /// </summary>
    public async Task<StageProgress> SetStartStageAsync(string userId, int targetStage, CancellationToken cancellationToken = default)
    {
        var stageProgress = await GetOrCreateStageProgressAsync(userId, cancellationToken);

        // Clamp to valid range: [1, HighestStage]
        var validStage = Math.Clamp(targetStage, 1, stageProgress.HighestStage);

        if (validStage != stageProgress.CurrentStage)
        {
            stageProgress.CurrentStage = validStage;
            stageProgress.CurrentRegion = StageProgress.GetRegionForStage(validStage);
            stageProgress.EnemiesDefeatedInCurrentStage = 0;
            await _stageProgressRepository.UpdateAsync(stageProgress);
        }

        return stageProgress;
    }

    /// <summary>
    /// Returns the player to their last checkpoint after defeat
    /// </summary>
    public async Task<StageProgress> ReturnToCheckpointAsync(string userId, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var stageProgress = await _stageProgressRepository.GetByUserIdAsync(userId);
                if (stageProgress == null)
                    throw new Core.Exceptions.EntityNotFoundException(nameof(StageProgress), userId);

                stageProgress.ReturnToCheckpoint();
                await _stageProgressRepository.UpdateAsync(stageProgress);

                return stageProgress;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        ex,
                        "ReturnToCheckpointAsync: Concurrency conflict for user {UserId}, retrying (attempt {Attempt}/{MaxRetries})...",
                        userId, attempt + 1, maxRetries);
                    await Task.Delay(100 * (attempt + 1), cancellationToken);
                    continue;
                }

                _logger.LogError(ex, "ReturnToCheckpointAsync: Failed after {MaxRetries} retries for user {UserId}", maxRetries, userId);
                throw;
            }
        }

        // Should never reach here, but satisfy compiler
        throw new InvalidOperationException("ReturnToCheckpointAsync exhausted retries");
    }

    /// <summary>
    /// Cancels a stage run in progress.
    /// Restores the character's HP to the specified value and resets stage progress.
    /// Used when user exits mid-run without completing it.
    /// </summary>
    public async Task<bool> CancelRunAsync(int characterId, int restoreHp, int restoreStage, int restoreShotBuffBattles = 0, int restoreCigarroShield = 0, int restoreCanhaoBoost = 0, int restorePenaltyBuff = 0, CancellationToken cancellationToken = default)
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

                // Restore character HP and buff state
                character.CurrentHP = restoreHp;
                character.ShotBuffBattlesRemaining = restoreShotBuffBattles;
                character.CigarroShieldHitsRemaining = restoreCigarroShield;
                character.CanhaoDamageBoostHitsRemaining = restoreCanhaoBoost;
                character.PenaltyBuffActive = restorePenaltyBuff;
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
                    await Task.Delay(100 * (attempt + 1), cancellationToken);
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
    /// Creates a temporary enemy character for combat simulation.
    /// Uses the unified difficulty curve with biome flavor.
    /// </summary>
    private Character CreateTemporaryEnemyCharacter(StageEnemy? template, int stageNumber, EnemyType type)
    {
        var isBoss = _biomeService.IsBossStage(stageNumber);
        return CreateEnemyUnifiedScaling(template, stageNumber, type, isBoss);
    }

    /// <summary>
    /// Unified scaling: ONE difficulty curve for ALL stats.
    /// EnemyStat = baseStat × curve × difficultyMult × bossMult
    /// Action time is determined by stage tier (every 100 stages = 0.5s faster, min 1.0s)
    /// </summary>
    private Character CreateEnemyUnifiedScaling(StageEnemy? template, int stageNumber, EnemyType type, bool isBoss)
    {
        var stageConfig = _myTunoScalingConfig.StageMode;
        var baseStats = stageConfig.BaseEnemyStats;
        var curve = _biomeService.GetUnifiedDifficultyCurve(stageNumber);
        var diffMult = isBoss ? _biomeService.GetBossesDifficultyMultiplier(stageNumber) : _biomeService.GetEnemiesDifficultyMultiplier(stageNumber);
        var bossMult = isBoss ? stageConfig.BossMultiplier : 1.0;

        int baseHP, basePower, baseSpeed, baseDefense;
        double baseCriticalChance;
        string enemyName;

        if (template != null)
        {
            // Template provides base stats; we apply unified curve on top
            baseHP = Math.Max(1, (int)(template.BaseHP * curve * diffMult * bossMult));
            basePower = Math.Max(1, (int)(template.BasePower * curve * diffMult * bossMult));
            baseSpeed = Math.Max(1, (int)(template.BaseSpeed * curve * diffMult * bossMult));
            baseDefense = Math.Max(1, (int)(template.BaseDefense * curve * diffMult * bossMult));
            baseCriticalChance = template.BaseCriticalChance;
            enemyName = template.Name;
        }
        else
        {
            var typeStats = type switch
            {
                EnemyType.Boss => baseStats.Boss,
                _ => baseStats.Normal
            };

            baseHP = Math.Max(1, (int)(typeStats.Hp * curve * diffMult * bossMult));
            basePower = Math.Max(1, (int)(typeStats.Power * curve * diffMult * bossMult));
            baseSpeed = Math.Max(1, (int)(typeStats.Speed * curve * diffMult * bossMult));
            baseDefense = Math.Max(1, (int)(typeStats.Defense * curve * diffMult * bossMult));

            var critGrowth = (stageNumber - 1) * 0.003; // Gentle crit growth
            baseCriticalChance = Math.Min(typeStats.CriticalChance + critGrowth, _myTunoScalingConfig.Combat.CriticalChanceCap);

            var biomeName = _biomeService.GetBiomeForStage(stageNumber);
            enemyName = type switch
            {
                EnemyType.Boss => $"{biomeName} Boss (Stage {stageNumber})",
                _ => $"{biomeName} Enemy (Stage {stageNumber})"
            };
        }

        var actionTime = GetEnemyActionTimeForStage(stageNumber);
        return Character.CreateStageEnemy(baseHP, basePower, baseSpeed, baseDefense, baseCriticalChance, enemyName, actionTime);
    }

    /// <summary>
    /// Returns the enemy action time (in seconds) based on stage tier.
    /// Every 100 stages reduces action time by 0.5s, minimum 1.0s.
    /// Stage 1-100: 5.0s, 101-200: 4.5s, ..., 801+: 1.0s
    /// </summary>
    private static double GetEnemyActionTimeForStage(int stageNumber)
    {
        var tier = (stageNumber - 1) / 100; // 0 for 1-100, 1 for 101-200, etc.
        var actionTime = 5.0 - (tier * 0.5);
        return Math.Max(1.0, actionTime);
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
    private (int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties, List<InventoryItemType> instrumentParts, List<InventoryItemType> equipment, int fitab) CalculateRewardsForBattle(
        CombatResult combatResult,
        int stageNumber,
        int characterLevel,
        int enemyCount = 1,
        int highestStage = 1)
    {
        if (combatResult.Outcome != BattleOutcome.AttackerWon)
        {
            return (0, 0m, 0, 0, 0, 0, 0, 0, new List<InventoryItemType>(), new List<InventoryItemType>(), 0);
        }

        var random = Random.Shared;
        var finosDropped = 0;
        var canecasDropped = 0;
        var cigarrosDropped = 0;
        var canhaosDropped = 0;
        var shotsDropped = 0;
        var penaltiesDropped = 0;
        var fitabDropped = 0;
        var instrumentPartsDropped = new List<InventoryItemType>();
        var equipmentDropped = new List<InventoryItemType>();
        var stageConfig = _myTunoScalingConfig.StageMode;
        var dropRates = stageConfig.DropRates;
        var fidelisRewardsConfig = stageConfig.FidelisRewards;

        var enemyType = GetEnemyTypeForStageFromConfig(stageNumber);
        var xpMultiplier = enemyType switch
        {
            EnemyType.Boss => stageConfig.BossXPMultiplier,
            _ => 1
        };

        // XP: enemy-level-based formula (same for both scaling modes — already clean)
        var enemyLevel = (double)stageNumber;
        var enemyLevelFactor = Math.Pow(enemyLevel, stageConfig.EnemyLevelXPPower);
        var levelDiff = Math.Max(0, characterLevel - stageNumber);
        var levelDiffMult = Math.Max(stageConfig.MinXPLevelMultiplier, 1.0 - levelDiff * stageConfig.XpLevelPenaltyRate);
        var xpReward = (int)Math.Round(stageConfig.XpPerEnemyLevel * enemyLevelFactor * enemyCount * xpMultiplier * levelDiffMult);

        // Fidelis: unified reward curve × biome reward multiplier × level bonus
        var baseFidelis = enemyType switch
        {
            EnemyType.Boss => fidelisRewardsConfig.BossWin,
            _ => fidelisRewardsConfig.NormalWin
        };

        var rewardCurve = _biomeService.GetUnifiedRewardCurve(stageNumber);
        var biomeRewardMult = _biomeService.GetRewardMultiplierForStage(stageNumber);
        var rewardConfig = stageConfig.RewardCurve;
        var rawLevelBonus = 1.0 + (characterLevel - 1) * rewardConfig.LevelBonusPerLevel;
        var levelBonus = Math.Min(rawLevelBonus, rewardConfig.LevelBonusCap);
        var fidelisReward = Math.Round(baseFidelis * enemyCount * (decimal)(rewardCurve * biomeRewardMult * levelBonus), 2);

        // Gate consumable drops behind biome progression
        // Fino=1(Forest), Shot=101(Swamp), Cigarro=301(Snowy), Caneca=501(Caverns), Canhão=701(Volcanic)
        var finoChance = highestStage >= 1 ? dropRates.FinoDropChance : 0;
        var canecaChance = highestStage >= 501 ? dropRates.CanecaDropChance : 0;
        var cigarroChance = highestStage >= 301 ? dropRates.CigarroDropChance : 0;
        var canhaoChance = highestStage >= 701 ? dropRates.CanhaoDropChance : 0;
        var shotChance = highestStage >= 101 ? dropRates.ShotDropChance : 0;
        var penaltyChance = highestStage >= 901 ? dropRates.PenaltyDropChance : 0;
        var instrumentPartChance = dropRates.InstrumentPartDropChance;
        var equipmentChance = dropRates.EquipmentDropChance;
        if (enemyType == EnemyType.Boss)
        {
            finoChance *= dropRates.BossDropMultiplier;
            canecaChance *= dropRates.BossDropMultiplier;
            cigarroChance *= dropRates.BossDropMultiplier;
            canhaoChance *= dropRates.BossDropMultiplier;
            shotChance *= dropRates.BossDropMultiplier;
            penaltyChance *= dropRates.BossDropMultiplier;
            instrumentPartChance *= dropRates.BossDropMultiplier;
            equipmentChance *= dropRates.BossDropMultiplier;
        }

        var instrumentTypes = Enum.GetValues(typeof(InstrumentType))
            .Cast<InstrumentType>()
            .Where(t => t != InstrumentType.Saxofone && t != InstrumentType.Fagote)
            .ToArray();
        var equipmentSlots = Enum.GetValues(typeof(EquipmentSlot));

        for (int i = 0; i < enemyCount; i++)
        {
            if (random.NextDouble() < finoChance) finosDropped++;
            if (random.NextDouble() < canecaChance) canecasDropped++;
            if (random.NextDouble() < cigarroChance) cigarrosDropped++;
            if (random.NextDouble() < canhaoChance) canhaosDropped++;
            if (random.NextDouble() < shotChance) shotsDropped++;
            if (random.NextDouble() < penaltyChance) penaltiesDropped++;

            // Roll for instrument part drop (very rare)
            if (random.NextDouble() < instrumentPartChance)
            {
                // Pick a random instrument type (Saxofone and Fagote excluded)
                var randomInstrument = instrumentTypes[random.Next(instrumentTypes.Length)];
                instrumentPartsDropped.Add(InstrumentTypeHelper.ToInventoryPartType(randomInstrument));
            }

            // Roll for equipment drop (slightly above instrument parts)
            if (random.NextDouble() < equipmentChance)
            {
                var randomSlot = (EquipmentSlot)equipmentSlots.GetValue(random.Next(equipmentSlots.Length))!;
                equipmentDropped.Add(EquipmentDropHelper.ToInventoryItemType(randomSlot));
            }

            // Roll for FITAB drop (very rare — currency for Boss Mode entry)
            var fitabChance = _myTunoScalingConfig.BossMode.FitabDropChanceStage;
            if (enemyType == EnemyType.Boss)
                fitabChance *= dropRates.BossDropMultiplier;
            if (random.NextDouble() < fitabChance)
                fitabDropped++;
        }

        return (xpReward, fidelisReward, finosDropped, canecasDropped, cigarrosDropped, canhaosDropped, shotsDropped, penaltiesDropped, instrumentPartsDropped, equipmentDropped, fitabDropped);
    }

    /// <summary>
    /// Applies accumulated run rewards (XP, Fidelis, item drops) when a stage run ends.
    /// Called after defeat to commit all rewards earned during the run.
    /// Not called on cancel/back — rewards are forfeited.
    /// </summary>
    public async Task ApplyRunRewardsAsync(int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0, int fitab = 0, int? restoreHp = null, Dictionary<InventoryItemType, int>? instrumentParts = null, Dictionary<InventoryItemType, int>? equipment = null, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await ApplyRunRewardsCoreAsync(characterId, xp, fidelis, finos, canecas, cigarros, canhaos, shots, penalties, fitab, restoreHp, instrumentParts, equipment, cancellationToken);
                return;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        ex,
                        "ApplyRunRewardsAsync: Concurrency conflict for character {CharacterId}, retrying (attempt {Attempt}/{MaxRetries})...",
                        characterId, attempt + 1, maxRetries);
                    await Task.Delay(100 * (attempt + 1), cancellationToken);
                    continue;
                }

                _logger.LogError(ex, "ApplyRunRewardsAsync: Failed after {MaxRetries} retries for character {CharacterId}", maxRetries, characterId);
                throw;
            }
        }
    }

    private async Task ApplyRunRewardsCoreAsync(int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties, int fitab, int? restoreHp, Dictionary<InventoryItemType, int>? instrumentParts, Dictionary<InventoryItemType, int>? equipment, CancellationToken cancellationToken)
    {
        var hasInstrumentParts = instrumentParts != null && instrumentParts.Count > 0;
        var hasEquipment = equipment != null && equipment.Count > 0;

        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
        {
            _logger.LogWarning("ApplyRunRewardsAsync: Character {CharacterId} not found", characterId);
            return;
        }

        // Restore HP to the value the player had before the run started
        // null restoreHp means the player entered with full HP (CurrentHP was null)
        character.CurrentHP = restoreHp;
        
        // Consume shot buff if it was active during this run
        // ExpireShotBuff handles scaling CurrentHP proportionally when buff reaches 0
        if (character.ShotBuffBattlesRemaining > 0)
        {
            character.ExpireShotBuff();
        }

        // Consume penalty buff if it was active during this run
        if (character.PenaltyBuffActive > 0)
        {
            character.ExpirePenaltyBuff();
        }
        
        await _characterRepository.UpdateAsync(character);

        if (xp <= 0 && fidelis <= 0 && finos <= 0 && canecas <= 0 && cigarros <= 0 && canhaos <= 0 && shots <= 0 && penalties <= 0 && !hasInstrumentParts && !hasEquipment)
            return;

        // Apply XP
        if (xp > 0)
        {
            character.AddXP(xp);
            await _characterRepository.UpdateAsync(character);
        }

        // Apply Fidelis and FITAB
        var user = await _userManager.FindByIdAsync(character.UserId);
        if (user != null && (fidelis > 0 || fitab > 0))
        {
            if (fidelis > 0) user.FidelisBalance += fidelis;
            if (fitab > 0) user.FitabBalance += fitab;
            await _userManager.UpdateAsync(user);
        }

        // Batch all inventory drops into a single DB round-trip
        var allDrops = new Dictionary<InventoryItemType, int>();
        if (finos > 0) allDrops[InventoryItemType.Fino] = finos;
        if (canecas > 0) allDrops[InventoryItemType.Caneca] = canecas;
        if (cigarros > 0) allDrops[InventoryItemType.Cigarro] = cigarros;
        if (canhaos > 0) allDrops[InventoryItemType.Canhao] = canhaos;
        if (shots > 0) allDrops[InventoryItemType.Shot] = shots;
        if (penalties > 0) allDrops[InventoryItemType.Penalty] = penalties;

        if (hasInstrumentParts)
        {
            foreach (var (partType, quantity) in instrumentParts!)
                allDrops[partType] = allDrops.GetValueOrDefault(partType) + quantity;
        }

        if (hasEquipment)
        {
            foreach (var (equipType, quantity) in equipment!)
                allDrops[equipType] = allDrops.GetValueOrDefault(equipType) + quantity;
        }

        if (allDrops.Count > 0)
            await _inventoryRepository.AddItemsAsync(character.UserId, allDrops, cancellationToken);

        _logger.LogInformation(
            "Applied run rewards for {Username} (Character ID: {CharacterId}): +{XP} XP, +{Fidelis} Fidelis, +{Fitab} FITAB, +{Finos} finos, +{Canecas} canecas, +{Cigarros} cigarros, +{Canhaos} canhaos, +{Shots} shots, +{InstrumentParts} instrument parts, +{Equipment} equipment",
            user?.UserName ?? "Unknown", characterId, xp, fidelis, fitab, finos, canecas, cigarros, canhaos, shots, instrumentParts?.Values.Sum() ?? 0, equipment?.Values.Sum() ?? 0);
    }

    /// <summary>
    /// Updates character HP, XP and stage progress after battle.
    /// On victory: reduces HP and advances stage.
    /// On defeat: restores HP to full (no death in stage mode).
    /// Only persists StageProgress when there's a new record:
    /// - HighestStage increased (player beat their previous best)
    /// - EndlessModeUnlocked changed (beat stage 1000)
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

        // HP carries over between stages — only set to final HP from combat
        if (combatResult.AttackerFinalHP > 0)
        {
            character.CurrentHP = combatResult.AttackerFinalHP;
        }
        else
        {
            character.CurrentHP = null; // Defeated — restore to full on next run
        }

        // Write back consumable buff remaining counts from combat
        character.CigarroShieldHitsRemaining = combatResult.AttackerCigarroShieldRemaining;
        character.CanhaoDamageBoostHitsRemaining = combatResult.AttackerCanhaoBoostRemaining;
        
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

    /// <inheritdoc />
    public List<int> GetAvailableCheckpoints(int highestStage)
    {
        var checkpoints = new List<int> { 1 }; // Always start with stage 1

        if (highestStage <= 1) return checkpoints;

        // Add checkpoints every 10 stages (after each boss), starting at 11
        // Each biome has 10 bosses at stages 10, 20, 30... so checkpoints at 11, 21, 31...
        for (int stage = 11; stage <= highestStage; stage += 10)
        {
            checkpoints.Add(stage);
        }

        return checkpoints;
    }

    /// <inheritdoc />
    public List<int> GetCheckpointsForBiome(int stageMin, int stageMax, int highestStage)
    {
        var checkpoints = new List<int>();
        var maxReachable = Math.Min(stageMax, highestStage);

        // First checkpoint: the biome's starting stage (always stage 1 for Forest, stageMin for others)
        if (stageMin <= highestStage)
        {
            checkpoints.Add(stageMin);
        }

        // Add checkpoints every 10 stages after bosses
        // Bosses are at stages 10, 20, 30... relative to global. Checkpoints at 11, 21, 31...
        var firstCheckpointAfterBoss = stageMin == 1 ? 11 : stageMin + 10;
        for (int stage = firstCheckpointAfterBoss; stage <= maxReachable; stage += 10)
        {
            if (stage >= stageMin && stage <= stageMax)
            {
                checkpoints.Add(stage);
            }
        }

        return checkpoints;
    }
}
