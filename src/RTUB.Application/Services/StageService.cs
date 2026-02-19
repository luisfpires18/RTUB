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
        
        // Get all enemy sprites/templates with placements
        var enemyTemplates = new List<StageEnemy?>();
        
        if (region == RegionType.Arena)
        {
            // Arena (20001+) uses filesystem-based sprites.
            // Drop new sprites into wwwroot/sprites/games/my-tuno/enemies/arena/
            // without touching SeedAllBiomeEnemiesAsync. boss_* files appear every 100 stages.
            if (enemyType == EnemyType.Boss)
            {
                var bossSprite = await _biomeService.GetBossSpriteForArenaAsync(stageNumber);
                for (int i = 0; i < enemyCount; i++)
                {
                    enemyTemplates.Add(null);
                    enemySpritePaths.Add(bossSprite);
                    enemyPlacements.Add(0); // Terrestrial
                }
            }
            else
            {
                var sprites = await _biomeService.GetRandomEnemySpritesAsync(stageNumber, enemyCount);
                foreach (var sprite in sprites)
                {
                    enemyTemplates.Add(null);
                    enemySpritePaths.Add(sprite);
                    enemyPlacements.Add(0); // Terrestrial
                }
            }
        }
        else if (enemyType == EnemyType.Boss)
        {
            // Deterministic boss selection: sprite is derived from the floor number
            // (boss_1 at floor 100, boss_2 at 200, …, boss_10 at 1000) — NOT random.
            var (bossSprite, bossPlacement) = await _biomeService.GetBossSpriteWithPlacementAsync(stageNumber);
            var boss = await _stageEnemyRepository.GetBossForStageAsync(stageNumber);
            for (int i = 0; i < enemyCount; i++)
            {
                enemyTemplates.Add(boss);
                enemySpritePaths.Add(bossSprite);
                enemyPlacements.Add(bossPlacement);
            }
        }
        else
        {
            // MiniBoss floors: pick a random Normal enemy from the same region
            // (no dedicated MiniBoss sprites exist — they're just buffed normals)
            var queryType = enemyType == EnemyType.MiniBoss ? EnemyType.Normal : enemyType;
            var randomEnemies = await _stageEnemyRepository.GetRandomEnemiesAsync(queryType, region, enemyCount);
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
            // Use the template's actual name (e.g. "Bee", "Spider") so each enemy
            // appears distinct in the battle UI. Fall back to generic name when no
            // DB template is available (Arena filesystem-sprite path).
            var displayName = template?.Name ?? $"{biomeName} #{i + 1}";
            enemy.User = new ApplicationUser { UserName = displayName };
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
        var (xpReward, fidelisReward, finosDropped, canecasDropped, cigarrosDropped, canhaosDropped, shotsDropped, penaltiesDropped, instrumentPartsDropped, fitabDropped) =
            CalculateRewardsForBattle(combatResult, stageNumber, character.Level, enemyCount, stageProgress.HighestStage);

        // Update character HP and stage progress in-memory only (no DB save per battle)
        UpdateCharacterAndProgressInMemory(character, stageProgress, combatResult, enemyType, enemyCount);

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
            FitabDropped = fitabDropped,
            ReplayJson = replayJson,
            PlayerFinalHP = combatResult.AttackerFinalHP,
            // Pre-parsed metadata so the UI doesn't need to re-deserialize ReplayJson
            EnemyCount = enemies.Count,
            EnemySpritePaths = enemySpritePaths,
            EnemyPlacements = enemyPlacements,
            EnemyStats = enemies.Select(e => new StageBattleEnemyStat
            {
                Name = e.User?.UserName ?? "Enemy",
                HP = e.TotalHP,
                Power = e.TotalPower,
                Defense = e.TotalDefense,
                ActionTime = Math.Round(e.ActionTime, 1)
            }).ToList()
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

                // Reload from database to ensure clean change tracker state.
                // In Blazor Server, the long-lived DbContext may have stale tracked state
                // from prior operations (e.g., ApplyRunRewardsAsync saving this entity as a
                // side effect when SaveChangesAsync flushes all modified tracked entities).
                // ReloadAsync refreshes original+current values and resets state to Unchanged.
                await _stageProgressRepository.ReloadAsync(stageProgress);

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
    public async Task<bool> CancelRunAsync(int characterId, long restoreHp, int restoreStage, int restoreShotBuffBattles = 0, int restoreCigarroShield = 0, int restoreCanhaoBoost = 0, int restorePenaltyBuff = 0, CancellationToken cancellationToken = default)
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

                // Reload stageProgress from database to ensure clean change tracker state.
                // The character save above may have flushed stageProgress as a side effect
                // (SaveChangesAsync saves all tracked Modified entities), leaving the
                // tracker with stale original values that cause concurrency exceptions.
                await _stageProgressRepository.ReloadAsync(stageProgress);

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
        var isBoss = type == EnemyType.Boss;
        var isMiniBoss = type == EnemyType.MiniBoss;
        return CreateEnemyUnifiedScaling(template, stageNumber, type, isBoss, isMiniBoss);
    }

    /// <summary>
    /// Creates an enemy character using the tiered enemy stat system.
    /// Normal enemies use tier stats directly; minibosses use tier miniboss overrides;
    /// bosses use tier boss overrides.
    /// Action time decreases every 100 stages (5.0s → 1.0s min).
    /// </summary>
    private Character CreateEnemyUnifiedScaling(StageEnemy? template, int stageNumber, EnemyType type, bool isBoss, bool isMiniBoss = false)
    {
        var tier = _biomeService.GetEnemyTierForStage(stageNumber);

        long baseHP, basePower, baseSpeed, baseDefense;
        double baseCriticalChance;
        string enemyName;

        if (template != null)
        {
            // Template provides name only; stats come from the tier
            enemyName = template.Name;

            if (isBoss)
            {
                baseHP = tier.BossHP;
                basePower = tier.BossPower;
                baseDefense = tier.BossDefense;
                baseSpeed = tier.Speed;
            }
            else if (isMiniBoss)
            {
                baseHP = tier.MinibossHP;
                basePower = tier.MinibossPower;
                baseDefense = tier.MinibossDefense;
                baseSpeed = tier.Speed;
            }
            else
            {
                baseHP = tier.HP;
                basePower = tier.Power;
                baseDefense = tier.Defense;
                baseSpeed = tier.Speed;
            }

            baseCriticalChance = Math.Min(tier.CritChance, _myTunoScalingConfig.Combat.CriticalChanceCap);
        }
        else if (isBoss)
        {
            baseHP = tier.BossHP;
            basePower = tier.BossPower;
            baseDefense = tier.BossDefense;
            baseSpeed = tier.Speed;
            baseCriticalChance = Math.Min(tier.CritChance, _myTunoScalingConfig.Combat.CriticalChanceCap);

            var biomeName = _biomeService.GetBiomeForStage(stageNumber);
            enemyName = $"{biomeName} Boss (Stage {stageNumber})";
        }
        else if (isMiniBoss)
        {
            baseHP = tier.MinibossHP;
            basePower = tier.MinibossPower;
            baseDefense = tier.MinibossDefense;
            baseSpeed = tier.Speed;
            baseCriticalChance = Math.Min(tier.CritChance, _myTunoScalingConfig.Combat.CriticalChanceCap);

            var biomeName = _biomeService.GetBiomeForStage(stageNumber);
            enemyName = $"{biomeName} MiniBoss (Stage {stageNumber})";
        }
        else
        {
            // Normal enemies use tier stats directly
            baseHP = tier.HP;
            basePower = tier.Power;
            baseDefense = tier.Defense;
            baseSpeed = tier.Speed;
            baseCriticalChance = Math.Min(tier.CritChance, _myTunoScalingConfig.Combat.CriticalChanceCap);

            var biomeName = _biomeService.GetBiomeForStage(stageNumber);
            enemyName = $"{biomeName} Enemy (Stage {stageNumber})";
        }

        var actionTime = GetEnemyActionTimeForStage(stageNumber);

        // Apply per-stage continuous growth multiplier on top of tier base stats
        var stageMult = _biomeService.GetStageProgressionMultiplier(stageNumber);
        baseHP = Math.Max(1, (long)Math.Round(baseHP * stageMult));
        basePower = Math.Max(1, (long)Math.Round(basePower * stageMult));
        baseDefense = Math.Max(1, (long)Math.Round(baseDefense * stageMult));

        return Character.CreateStageEnemy(baseHP, basePower, baseSpeed, baseDefense, baseCriticalChance, enemyName, actionTime);
    }

    /// <summary>
    /// Returns the enemy action time (in seconds) based on biome (1000-floor blocks).
    /// Every 2 biomes reduces action time by 0.5s.
    /// Biomes 1-2: 5.0s, 3-4: 4.5s, 5-6: 4.0s, …, 19-20: 0.5s, 21 (Arena): 0.1s
    /// </summary>
    private static double GetEnemyActionTimeForStage(int stageNumber)
    {
        var biomeIndex = (stageNumber - 1) / 1000; // 0 for 1-1000, 1 for 1001-2000, etc.

        // Arena (biome 21+, floors 20001+)
        if (biomeIndex >= 20)
            return 0.1;

        // Every 2 biomes drops 0.5s: pair 0 → 5.0, pair 1 → 4.5, …, pair 9 → 0.5
        var pair = biomeIndex / 2; // integer division: 0-1→0, 2-3→1, …, 18-19→9
        var actionTime = 5.0 - (pair * 0.5);
        return Math.Max(0.5, actionTime);
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
    private (int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties, List<InventoryItemType> instrumentParts, int fitab) CalculateRewardsForBattle(
        CombatResult combatResult,
        int stageNumber,
        int characterLevel,
        int enemyCount = 1,
        int highestStage = 1)
    {
        if (combatResult.Outcome != BattleOutcome.AttackerWon)
        {
            return (0, 0m, 0, 0, 0, 0, 0, 0, new List<InventoryItemType>(), 0);
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
        var stageConfig = _myTunoScalingConfig.StageMode;
        var dropRates = stageConfig.DropRates;

        // v5: Tier-based XP and Fidelis rewards (no formula — designer-tuned per tier)
        var tier = _biomeService.GetEnemyTierForStage(stageNumber);
        var enemyType = GetEnemyTypeForStageFromConfig(stageNumber);

        // Apply per-stage continuous growth multiplier to rewards
        var stageMult = _biomeService.GetStageProgressionMultiplier(stageNumber);
        var xpReward = (int)Math.Round(tier.XpReward * enemyCount * stageMult);

        // Fidelis: tier base × biome reward multiplier × enemy count × stage multiplier
        var biomeRewardMult = _biomeService.GetRewardMultiplierForStage(stageNumber);
        var baseFidelis = tier.FidelisReward;
        var fidelisReward = Math.Round(baseFidelis * enemyCount * (decimal)(biomeRewardMult * stageMult), 2);

        // Gate consumable drops behind biome progression (1000-floor biomes)
        // Fino=1(Forest), Shot=1001(Swamp), Cigarro=3001(Snowy), Caneca=5001(Caverns), Canhão=7001(Volcanic), Penalty=9001(Sky)
        var finoChance = highestStage >= 1 ? dropRates.FinoDropChance : 0;
        var canecaChance = highestStage >= 5001 ? dropRates.CanecaDropChance : 0;
        var cigarroChance = highestStage >= 3001 ? dropRates.CigarroDropChance : 0;
        var canhaoChance = highestStage >= 7001 ? dropRates.CanhaoDropChance : 0;
        var shotChance = highestStage >= 1001 ? dropRates.ShotDropChance : 0;
        var penaltyChance = highestStage >= 9001 ? dropRates.PenaltyDropChance : 0;
        var instrumentPartChance = dropRates.InstrumentPartDropChance;
        if (enemyType == EnemyType.Boss)
        {
            finoChance *= dropRates.BossDropMultiplier;
            canecaChance *= dropRates.BossDropMultiplier;
            cigarroChance *= dropRates.BossDropMultiplier;
            canhaoChance *= dropRates.BossDropMultiplier;
            shotChance *= dropRates.BossDropMultiplier;
            penaltyChance *= dropRates.BossDropMultiplier;
            instrumentPartChance *= dropRates.BossDropMultiplier;
        }

        var instrumentTypes = InstrumentTypeHelper.GameInstrumentTypes.ToArray();

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

            // Roll for FITAB drop (very rare — currency for Boss Mode entry)
            var fitabChance = _myTunoScalingConfig.BossMode.FitabDropChanceStage;
            if (enemyType == EnemyType.Boss)
                fitabChance *= dropRates.BossDropMultiplier;
            if (random.NextDouble() < fitabChance)
                fitabDropped++;
        }

        return (xpReward, fidelisReward, finosDropped, canecasDropped, cigarrosDropped, canhaosDropped, shotsDropped, penaltiesDropped, instrumentPartsDropped, fitabDropped);
    }

    /// <summary>
    /// Applies accumulated run rewards (XP, Fidelis, item drops) when a stage run ends.
    /// Called after defeat to commit all rewards earned during the run.
    /// Not called on cancel/back — rewards are forfeited.
    /// </summary>
    public async Task ApplyRunRewardsAsync(int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0, int fitab = 0, long? restoreHp = null, Dictionary<InventoryItemType, int>? instrumentParts = null, bool expirePenaltyBuff = true, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await ApplyRunRewardsCoreAsync(characterId, xp, fidelis, finos, canecas, cigarros, canhaos, shots, penalties, fitab, restoreHp, instrumentParts, expirePenaltyBuff, cancellationToken);
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

    private async Task ApplyRunRewardsCoreAsync(int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties, int fitab, long? restoreHp, Dictionary<InventoryItemType, int>? instrumentParts, bool expirePenaltyBuff, CancellationToken cancellationToken)
    {
        var hasInstrumentParts = instrumentParts != null && instrumentParts.Count > 0;

        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
        {
            _logger.LogWarning("ApplyRunRewardsAsync: Character {CharacterId} not found", characterId);
            return;
        }

        // Always restore full HP after run — players no longer lose HP between battles
        character.CurrentHP = null;
        
        // Expire all active buffs (one run consumed per call)
        if (character.ShotBuffBattlesRemaining > 0)
        {
            character.ExpireShotBuff();
        }
        if (character.CigarroShieldHitsRemaining > 0)
        {
            character.ExpireCigarroBuff();
        }
        if (character.CanhaoDamageBoostHitsRemaining > 0)
        {
            character.ExpireCanhaoBuff();
        }
        if (character.PenaltyBuffActive > 0)
        {
            character.ExpirePenaltyBuff();
        }
        
        await _characterRepository.UpdateAsync(character);

        if (xp <= 0 && fidelis <= 0 && fitab <= 0 && finos <= 0 && canecas <= 0 && cigarros <= 0 && canhaos <= 0 && shots <= 0 && penalties <= 0 && !hasInstrumentParts)
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
            // Apply Fidelis earned multiplier from Improvements upgrade
            if (fidelis > 0) user.FidelisBalance += fidelis * (decimal)character.FidelisEarnedMultiplier;
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

        if (allDrops.Count > 0)
            await _inventoryRepository.AddItemsAsync(character.UserId, allDrops, cancellationToken);

        _logger.LogInformation(
            "Applied run rewards for {Username} (Character ID: {CharacterId}): +{XP} XP, +{Fidelis} Fidelis, +{Fitab} FITAB, +{Finos} finos, +{Canecas} canecas, +{Cigarros} cigarros, +{Canhaos} canhaos, +{Shots} shots, +{InstrumentParts} instrument parts",
            user?.UserName ?? "Unknown", characterId, xp, fidelis, fitab, finos, canecas, cigarros, canhaos, shots, instrumentParts?.Values.Sum() ?? 0);
    }

    /// <summary>
    /// Updates character HP and stage progress after battle (in-memory only).
    /// No DB saves during a run — saves are deferred to run-end methods
    /// (ApplyRunRewardsAsync + ReturnToCheckpointAsync, or CancelRunAsync).
    /// This eliminates per-stage DB round-trips and concurrency issues.
    /// </summary>
    private void UpdateCharacterAndProgressInMemory(
        Character character,
        StageProgress stageProgress,
        CombatResult combatResult,
        EnemyType enemyType,
        int enemyCount = 1)
    {
        // HP carries over between stages — only set to final HP from combat
        if (combatResult.AttackerFinalHP > 0)
        {
            character.CurrentHP = combatResult.AttackerFinalHP;
        }
        else
        {
            character.CurrentHP = null; // Defeated — restore to full on next run
        }


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
    /// Calculates XP and Fidelis rewards for winning a given stage.
    /// Used when the interactive session wins but the deterministic sim predicted a loss
    /// (so the StageBattleResult has XPReward/FidelisReward = 0).
    /// </summary>
    public (int xp, decimal fidelis) GetWinRewardsForStage(int stageNumber, int enemyCount, int highestStage)
    {
        var tier = _biomeService.GetEnemyTierForStage(stageNumber);
        var biomeRewardMult = _biomeService.GetRewardMultiplierForStage(stageNumber);
        var stageMult = _biomeService.GetStageProgressionMultiplier(stageNumber);
        var xp = (int)Math.Round(tier.XpReward * enemyCount * stageMult);
        var fidelis = Math.Round(tier.FidelisReward * enemyCount * (decimal)(biomeRewardMult * stageMult), 2);
        return (xp, fidelis);
    }

    /// <summary>
    /// Gets the enemy type for a stage using biome config.
    /// Boss every 100 stages, MiniBoss every 10 stages (excluding boss stages).
    /// </summary>
    private EnemyType GetEnemyTypeForStageFromConfig(int stageNumber)
    {
        if (_biomeService.IsBossStage(stageNumber))
            return EnemyType.Boss;
        if (_biomeService.IsMiniBossStage(stageNumber))
            return EnemyType.MiniBoss;
        
        return EnemyType.Normal;
    }

    /// <inheritdoc />
    public List<int> GetAvailableCheckpoints(int highestStage)
    {
        var checkpoints = new List<int> { 1 }; // Always start with stage 1

        if (highestStage <= 1) return checkpoints;

        // Add checkpoints every 10 floors (after each miniboss), starting at 11
        // Each biome has minibosses at floors 10, 20, 30... so checkpoints at 11, 21, 31...
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

        // Add checkpoints every 10 floors after minibosses
        // Minibosses at floors 10, 20, 30... relative to global. Checkpoints at 11, 21, 31...
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
