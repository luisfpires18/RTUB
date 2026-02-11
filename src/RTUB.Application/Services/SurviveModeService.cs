using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for Survive Mode — a survivor.io-inspired game.
/// Player spawns center-map, enemies swarm from edges, dodge to survive the timer.
/// Each level = 1 biome. Harder waves, faster enemies, longer timer per level.
/// </summary>
public class SurviveModeService : ISurviveModeService
{
    private readonly ISurviveModeProgressRepository _progressRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SurviveModeService> _logger;
    private readonly MyTunoScalingConfiguration _config;
    private readonly IStageBiomeService _biomeService;
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationDbContext _context;
    private readonly Random _random = new();

    // Survive mode constants
    private const double BaseTimerSeconds = 480.0;         // Level 1 timer (8 minutes)
    private const double TimerIncreasePerLevel = 60.0;     // +1 min per level
    private const double MaxTimerSeconds = 1200.0;         // Cap at 20 minutes
    private const int BaseEnemyCount = 5;                  // Starting enemies
    private const int EnemyCountIncreasePerLevel = 3;      // +3 max enemies per level
    private const int MaxEnemyCountCap = 50;               // Cap alive enemies
    private const double BaseEnemySpeed = 72.0;            // Pixels per second
    private const double MaxEnemySpeedCap = 234.0;         // Speed cap
    private const double BasePlayerSpeed = 120.0;          // Player is faster than enemies
    private const double PlayerSpeedDecayPerLevel = 2.0;   // Gets slightly slower each level
    private const double MinPlayerSpeed = 80.0;            // Never slower than this
    private const double BaseSpawnInterval = 3.0;          // Seconds between waves
    private const double MinSpawnInterval = 0.5;           // Fastest spawn rate
    private const double BaseEnemyScale = 0.6;             // Smaller enemies = harder
    private const double EnemyScaleDecreasePerLevel = 0.02;
    private const double MinEnemyScale = 0.3;
    private const int MapWidth = 2400;                     // Scrollable map
    private const int MapHeight = 2400;
    private const int ViewportWidth = 800;                 // Visible area
    private const int ViewportHeight = 500;
    private const int MaxLevel = 11;                       // Void is the final level

    // All levels start the same — difficulty ramps over TIME within each level,
    // not across levels. The per-biome time-ramp rates below control how fast
    // spawns and enemy speed increase every minute during a level.
    private static readonly (double spawnMult, double speedMult)[] LevelScaling = new[]
    {
        (1.00, 1.00), // Level 1  - Forest
        (1.00, 1.00), // Level 2  - Swamp
        (1.00, 1.00), // Level 3  - Mountains
        (1.00, 1.00), // Level 4  - Snowy
        (1.00, 1.00), // Level 5  - Tropical
        (1.00, 1.00), // Level 6  - Caverns
        (1.00, 1.00), // Level 7  - Desert
        (1.00, 1.00), // Level 8  - Volcanic
        (1.00, 1.00), // Level 9  - Ruins
        (1.00, 1.00), // Level 10 - Dark
        (1.00, 1.00), // Level 11 - Void (final)
    };

    // Per-biome time-based ramp: (spawnRampPerMinute, speedRampPerMinute)
    // Each minute within a level, spawns get X% faster and enemies move Y% faster.
    private static readonly (double spawnRamp, double speedRamp)[] BiomeTimeRamp = new[]
    {
        (0.20, 0.10), // Level 1  - Forest:    +20% spawn / +10% speed per min
        (0.25, 0.15), // Level 2  - Swamp:     +25% spawn / +15% speed per min
        (0.30, 0.20), // Level 3  - Mountains: +30% spawn / +20% speed per min
        (0.35, 0.25), // Level 4  - Snowy:     +35% spawn / +25% speed per min
        (0.40, 0.30), // Level 5  - Tropical:  +40% spawn / +30% speed per min
        (0.45, 0.35), // Level 6  - Caverns:   +45% spawn / +35% speed per min
        (0.50, 0.40), // Level 7  - Desert:    +50% spawn / +40% speed per min
        (0.55, 0.45), // Level 8  - Volcanic:  +55% spawn / +45% speed per min
        (0.60, 0.50), // Level 9  - Ruins:     +60% spawn / +50% speed per min
        (0.65, 0.55), // Level 10 - Dark:      +65% spawn / +55% speed per min
        (0.70, 0.60), // Level 11 - Void:      +70% spawn / +60% speed per min
    };

    // Reward constants
    private const int BaseXPPerLevel = 20;
    private const decimal BaseFidelisPerLevel = 15;
    private const double XPPerEnemyKill = 2.0;
    private const double FidelisPerEnemyKill = 0.5;

    public SurviveModeService(
        ISurviveModeProgressRepository progressRepository,
        ICharacterRepository characterRepository,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<SurviveModeService> logger,
        IOptions<MyTunoScalingConfiguration> config,
        IStageBiomeService biomeService,
        IStageProgressRepository stageProgressRepository,
        IWebHostEnvironment environment,
        ApplicationDbContext context)
    {
        _progressRepository = progressRepository;
        _characterRepository = characterRepository;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _config = config.Value;
        _biomeService = biomeService;
        _stageProgressRepository = stageProgressRepository;
        _environment = environment;
        _context = context;
    }

    /// <summary>
    /// Resets stale ApplicationUser entries in the change tracker.
    /// </summary>
    private void ResetStaleUserEntries()
    {
        foreach (var entry in _context.ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Unchanged;
            }
        }
    }

    /// <inheritdoc />
    public async Task<SurviveModeProgress> GetOrCreateProgressAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var progress = await _progressRepository.GetByUserIdAsync(userId);
        if (progress != null)
            return progress;

        progress = SurviveModeProgress.Create(userId);
        await _progressRepository.AddAsync(progress);
        return progress;
    }

    /// <inheritdoc />
    public async Task<SurviveModeProgress?> GetProgressAsync(string userId)
    {
        return await _progressRepository.GetByUserIdAsync(userId);
    }

    /// <inheritdoc />
    public async Task<SurviveModeProgress> StartRunAsync(int characterId)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        if ((character.CurrentHP ?? character.HP) <= 0)
            throw new InvalidOperationException("Character is dead — cannot start a survive run");

        var progress = await GetOrCreateProgressAsync(character.UserId);

        if (progress.IsRunActive)
        {
            _logger.LogWarning("User {UserId} tried to start survive run while one is active. Cancelling old run.",
                character.UserId);
            progress.CancelRun();
        }

        progress.StartRun();
        ResetStaleUserEntries();
        await _progressRepository.UpdateAsync(progress);

        var user = await _userManager.FindByIdAsync(character.UserId);
        _logger.LogInformation("Survive Mode initiated by {UserName}", user?.UserName ?? character.UserId);

        return progress;
    }

    /// <inheritdoc />
    public async Task<SurviveModeProgress> SetStartLevelAsync(string userId, int targetLevel)
    {
        var progress = await GetOrCreateProgressAsync(userId);

        // Clamp to valid range: [1, HighestLevel]
        var validLevel = Math.Clamp(targetLevel, 1, progress.HighestLevel);

        if (validLevel != progress.CurrentLevel)
        {
            progress.CurrentLevel = validLevel;
            progress.CurrentRegion = SurviveModeProgress.GetRegionForLevel(validLevel);
            await _progressRepository.UpdateAsync(progress);
        }

        return progress;
    }

    /// <inheritdoc />
    public SurviveModeLevelConfig GetLevelConfig(int level, int characterLevel)
    {
        var region = SurviveModeProgress.GetRegionForLevel(level);
        var biomeName = SurviveModeProgress.GetBiomeName(level);

        // Get biome difficulty/reward multipliers from stage mode config
        var biomeConfig = _config.StageMode?.Biomes?.FirstOrDefault(b =>
            string.Equals(b.Name, biomeName, StringComparison.OrdinalIgnoreCase));

        var difficultyMult = biomeConfig?.DifficultyMultiplier ?? 1.0 + (level - 1) * 0.3;
        var rewardMult = biomeConfig?.RewardMultiplier ?? 1.0 + (level - 1) * 0.2;

        // Per-level scaling from the LevelScaling table (clamped to array bounds)
        var scaleIdx = Math.Clamp(level - 1, 0, LevelScaling.Length - 1);
        var (spawnMult, speedMult) = LevelScaling[scaleIdx];

        // Timer: 8 min base + 1 min per level, caps at MaxTimerSeconds
        var timer = Math.Min(BaseTimerSeconds + (level - 1) * TimerIncreasePerLevel, MaxTimerSeconds);

        // Enemy count: scales with level and spawn multiplier
        var baseCount = Math.Min((int)(BaseEnemyCount + (level - 1) * 2 * spawnMult), MaxEnemyCountCap / 2);
        var maxCount = Math.Min((int)((BaseEnemyCount + (level - 1) * EnemyCountIncreasePerLevel) * spawnMult), MaxEnemyCountCap);

        // Enemy speed scales with level, difficulty, and speed multiplier
        var enemySpeed = Math.Min(BaseEnemySpeed * speedMult * difficultyMult, MaxEnemySpeedCap);
        var maxEnemySpeed = Math.Min(enemySpeed * 1.5, MaxEnemySpeedCap * 1.2);

        // Player speed: starts high, slowly decreases (still faster than enemies)
        var playerSpeed = Math.Max(BasePlayerSpeed - (level - 1) * PlayerSpeedDecayPerLevel, MinPlayerSpeed);

        // Spawn interval: gets faster each level, scaled by spawnMult
        var spawnInterval = Math.Max(BaseSpawnInterval / spawnMult, MinSpawnInterval);

        // Enemy scale: gets smaller each level (harder to see, more can fit)
        var enemyScale = Math.Max(BaseEnemyScale - (level - 1) * EnemyScaleDecreasePerLevel, MinEnemyScale);

        // Elite enemies appear from level 3+
        var hasElites = level >= 3;
        var eliteChance = hasElites ? Math.Min(0.05 + (level - 3) * 0.03, 0.30) : 0.0;

        // Void (level 11) is the final level — no bosses, timer expiry = win
        var isFinalLevel = level >= MaxLevel;

        // Per-biome time ramp rates
        var rampIdx = Math.Clamp(level - 1, 0, BiomeTimeRamp.Length - 1);
        var (spawnRampPerMin, speedRampPerMin) = BiomeTimeRamp[rampIdx];

        return new SurviveModeLevelConfig
        {
            Level = level,
            BiomeName = biomeName,
            Region = region,
            TimerDurationSeconds = timer,
            BaseEnemyCount = baseCount,
            MaxEnemyCount = maxCount,
            SpawnIntervalSeconds = spawnInterval,
            EnemySpeed = enemySpeed,
            MaxEnemySpeed = maxEnemySpeed,
            PlayerSpeed = playerSpeed,
            EnemyScale = enemyScale,
            DifficultyMultiplier = difficultyMult,
            RewardMultiplier = rewardMult,
            EnemySpriteVariants = 4,
            HasEliteEnemies = hasElites,
            EliteSpawnChance = eliteChance,
            MapWidth = MapWidth,
            MapHeight = MapHeight,
            ViewportWidth = ViewportWidth,
            ViewportHeight = ViewportHeight,
            IsFinalLevel = isFinalLevel,
            SpawnRampPerMinute = spawnRampPerMin,
            SpeedRampPerMinute = speedRampPerMin
        };
    }

    /// <inheritdoc />
    public async Task<SurviveModeLevelResult> CompleteLevelAsync(int characterId, int enemiesKilled, double survivalTimeSeconds)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        var progress = await GetOrCreateProgressAsync(character.UserId);

        if (!progress.IsRunActive)
            throw new InvalidOperationException("No active survive run");

        var level = progress.CurrentLevel;
        var config = GetLevelConfig(level, character.Level);

        // Server-side timing validation (allow 2s grace for network latency)
        if (progress.RunStartedAt.HasValue)
        {
            var elapsed = (DateTime.UtcNow - progress.RunStartedAt.Value).TotalSeconds;
            if (survivalTimeSeconds > elapsed + 2.0)
            {
                _logger.LogWarning("Survive mode timing validation failed for user {UserId}. " +
                    "Claimed {ClaimedTime}s but only {ElapsedTime}s elapsed.",
                    character.UserId, survivalTimeSeconds, elapsed);
                survivalTimeSeconds = elapsed;
            }
        }

        // Calculate rewards (gate consumable drops behind biome progression)
        var stageProgressForDrops = await _stageProgressRepository.GetByUserIdAsync(character.UserId);
        var highestStageForDrops = stageProgressForDrops?.HighestStage ?? 1;
        var result = CalculateRewards(level, config, character.Level, enemiesKilled, survivalTimeSeconds, true, highestStageForDrops);
        result.CharacterId = characterId;

        // Update progress
        progress.CompleteLevel(survivalTimeSeconds, enemiesKilled);
        progress.RunStartedAt = DateTime.UtcNow; // Reset timer for next level
        ResetStaleUserEntries();
        await _progressRepository.UpdateAsync(progress);

        var completedUser = await _userManager.FindByIdAsync(character.UserId);
        _logger.LogInformation("Survive Mode ended on level {Level} by {UserName} - rewards: {XP} XP, {Fidelis} Fidelis",
            level, completedUser?.UserName ?? character.UserId, result.XPReward, result.FidelisReward);

        return result;
    }

    /// <inheritdoc />
    public async Task<SurviveModeLevelResult> EndRunAsync(int characterId, int enemiesKilled, double survivalTimeSeconds)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        var progress = await GetOrCreateProgressAsync(character.UserId);

        if (!progress.IsRunActive)
            throw new InvalidOperationException("No active survive run");

        var level = progress.CurrentLevel;
        var config = GetLevelConfig(level, character.Level);

        // Calculate partial rewards (didn't survive full timer)
        var stageProgressForDrops = await _stageProgressRepository.GetByUserIdAsync(character.UserId);
        var highestStageForDrops = stageProgressForDrops?.HighestStage ?? 1;
        var survivalRatio = Math.Min(survivalTimeSeconds / config.TimerDurationSeconds, 1.0);
        var result = CalculateRewards(level, config, character.Level, enemiesKilled, survivalTimeSeconds, false, highestStageForDrops);
        result.CharacterId = characterId;

        // Scale rewards by survival ratio (died early = less rewards)
        result.XPReward = (int)(result.XPReward * survivalRatio);
        result.FidelisReward = Math.Round(result.FidelisReward * (decimal)survivalRatio, 2);

        // Update progress
        progress.EndRun(survivalTimeSeconds, enemiesKilled);
        ResetStaleUserEntries();
        await _progressRepository.UpdateAsync(progress);

        var diedUser = await _userManager.FindByIdAsync(character.UserId);
        _logger.LogInformation("Survive Mode ended on level {Level} by {UserName} - rewards: {XP} XP, {Fidelis} Fidelis",
            level, diedUser?.UserName ?? character.UserId, result.XPReward, result.FidelisReward);

        return result;
    }

    /// <inheritdoc />
    public async Task ApplyRunRewardsAsync(int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0,
        Dictionary<InventoryItemType, int>? instrumentParts = null,
        Dictionary<InventoryItemType, int>? equipment = null)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        var user = await _userManager.FindByIdAsync(character.UserId)
            ?? throw new InvalidOperationException("User not found");

        // Apply XP
        if (xp > 0)
        {
            character.AddXP(xp);
        }

        // Apply Fidelis
        if (fidelis > 0)
        {
            user.FidelisBalance += fidelis;
        }

        // Batch all inventory drops into a single DB round-trip
        var allDrops = new Dictionary<InventoryItemType, int>();
        if (finos > 0) allDrops[InventoryItemType.Fino] = finos;
        if (canecas > 0) allDrops[InventoryItemType.Caneca] = canecas;
        if (cigarros > 0) allDrops[InventoryItemType.Cigarro] = cigarros;
        if (canhaos > 0) allDrops[InventoryItemType.Canhao] = canhaos;
        if (shots > 0) allDrops[InventoryItemType.Shot] = shots;
        if (penalties > 0) allDrops[InventoryItemType.Penalty] = penalties;

        if (instrumentParts != null)
        {
            foreach (var (partType, quantity) in instrumentParts)
                allDrops[partType] = allDrops.GetValueOrDefault(partType) + quantity;
        }

        if (equipment != null)
        {
            foreach (var (equipType, quantity) in equipment)
                allDrops[equipType] = allDrops.GetValueOrDefault(equipType) + quantity;
        }

        if (allDrops.Count > 0)
            await _inventoryRepository.AddItemsAsync(character.UserId, allDrops);

        ResetStaleUserEntries();
        await _characterRepository.UpdateAsync(character);
        await _userManager.UpdateAsync(user);
    }

    /// <inheritdoc />
    public async Task<bool> CancelRunAsync(int characterId)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        var progress = await GetOrCreateProgressAsync(character.UserId);

        if (!progress.IsRunActive)
            return false;

        progress.CancelRun();
        ResetStaleUserEntries();
        await _progressRepository.UpdateAsync(progress);

        return true;
    }

    /// <inheritdoc />
    public string GetBackgroundPath(int level)
    {
        var biomeName = SurviveModeProgress.GetBiomeName(level).ToLowerInvariant();
        return $"/sprites/games/my-tuno/backgrounds/{biomeName}.png";
    }

    /// <inheritdoc />
    public async Task<List<string>> GetEnemySpritesAsync(int level, int count)
    {
        var biomeName = SurviveModeProgress.GetBiomeName(level).ToLowerInvariant();
        var spritePath = $"sprites/games/my-tuno/enemies/{biomeName}";

        // Look for sprite files on disk
        var webRootPath = _environment.WebRootPath;
        var fullPath = Path.Combine(webRootPath, spritePath.Replace('/', Path.DirectorySeparatorChar));

        var sprites = new List<string>();

        if (Directory.Exists(fullPath))
        {
            var files = Directory.GetFiles(fullPath, "*.png")
                .Where(f => !Path.GetFileName(f).StartsWith("boss_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (files.Count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var file = files[_random.Next(files.Count)];
                    var relativePath = "/" + Path.GetRelativePath(webRootPath, file).Replace('\\', '/');
                    sprites.Add(relativePath);
                }
                return sprites;
            }
        }

        // Fallback: try to find enemies from the stage enemy DB
        var region = SurviveModeProgress.GetRegionForLevel(level);
        var enemies = await _context.StageEnemies
            .Where(e => e.Region == region && e.Type == EnemyType.Normal && e.SpritePath != null)
            .Select(e => e.SpritePath!)
            .ToListAsync();

        if (enemies.Count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                sprites.Add(enemies[_random.Next(enemies.Count)]);
            }
            return sprites;
        }

        // Last resort: placeholder sprites
        for (int i = 0; i < count; i++)
        {
            sprites.Add($"/sprites/games/my-tuno/enemies/forest/enemy_{(i % 4) + 1}.png");
        }
        return sprites;
    }

    /// <inheritdoc />
    public Task<List<string>> GetBossSpritesAsync(int level, int count)
    {
        var biomeName = SurviveModeProgress.GetBiomeName(level).ToLowerInvariant();
        var spritePath = $"sprites/games/my-tuno/enemies/{biomeName}";
        var webRootPath = _environment.WebRootPath;
        var fullPath = Path.Combine(webRootPath, spritePath.Replace('/', Path.DirectorySeparatorChar));

        var sprites = new List<string>();

        if (Directory.Exists(fullPath))
        {
            var bossFiles = Directory.GetFiles(fullPath, "boss_*.png").ToList();

            if (bossFiles.Count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var file = bossFiles[_random.Next(bossFiles.Count)];
                    var relativePath = "/" + Path.GetRelativePath(webRootPath, file).Replace('\\', '/');
                    sprites.Add(relativePath);
                }
            }
        }

        return Task.FromResult(sprites);
    }

    /// <summary>
    /// Calculates rewards for a survive level attempt.
    /// </summary>
    private SurviveModeLevelResult CalculateRewards(int level, SurviveModeLevelConfig config,
        int characterLevel, int enemiesKilled, double survivalTimeSeconds, bool survived, int highestStage = 1)
    {
        var diffMult = config.DifficultyMultiplier;
        var rewardMult = config.RewardMultiplier;

        // Base XP from level completion + kill bonus
        var baseXP = (int)(BaseXPPerLevel * level * diffMult);
        var killXP = (int)(enemiesKilled * XPPerEnemyKill * Math.Sqrt(level));
        var xpReward = survived ? baseXP + killXP : killXP; // Only full XP on survival

        // Fidelis reward
        var baseFidelis = BaseFidelisPerLevel * level * (decimal)rewardMult;
        var killFidelis = (int)(enemiesKilled * FidelisPerEnemyKill);
        var fidelisReward = survived ? baseFidelis + killFidelis : killFidelis;

        // Level bonus (higher character level = slightly more rewards)
        var levelBonus = 1.0 + Math.Min(characterLevel * 0.005, 0.5);
        xpReward = (int)(xpReward * levelBonus);
        fidelisReward = Math.Round(fidelisReward * (decimal)levelBonus, 2);

        // Drop calculations
        var dropRates = _config.StageMode?.DropRates;
        // Gate consumable drops behind biome progression
        // Fino=1(Forest), Shot=101(Swamp), Cigarro=301(Snowy), Caneca=501(Caverns), Canhão=701(Volcanic)
        var finoChance = highestStage >= 1 ? (dropRates?.FinoDropChance ?? 0.1) : 0;
        var canecaChance = highestStage >= 501 ? (dropRates?.CanecaDropChance ?? 0.04) : 0;
        var cigarroChance = highestStage >= 301 ? (dropRates?.CigarroDropChance ?? 0.05) : 0;
        var canhaoChance = highestStage >= 701 ? (dropRates?.CanhaoDropChance ?? 0.05) : 0;
        var shotChance = highestStage >= 101 ? (dropRates?.ShotDropChance ?? 0.01) : 0;
        var penaltyChance = highestStage >= 901 ? (dropRates?.PenaltyDropChance ?? 0.003) : 0;
        var instrChance = dropRates?.InstrumentPartDropChance ?? 0.005;
        var equipChance = dropRates?.EquipmentDropChance ?? 0.008;

        // More kills = more drop rolls, scaled by level difficulty
        var dropRolls = enemiesKilled;
        var finos = 0;
        var canecas = 0;
        var cigarros = 0;
        var canhaos = 0;
        var shots = 0;
        var penalties = 0;
        var fitab = 0;
        var instrParts = new List<InventoryItemType>();
        var equipPieces = new List<InventoryItemType>();

        var instrPartTypes = new[]
        {
            InventoryItemType.GuitarraPart, InventoryItemType.BaixoPart,
            InventoryItemType.CavaquinhoPart, InventoryItemType.AcordeaoPart,
            InventoryItemType.ViolinoPart, InventoryItemType.PercussaoPart,
            InventoryItemType.FlautaPart, InventoryItemType.SaxofonePart
        };

        var equipSlotTypes = new[]
        {
            InventoryItemType.EquipmentHead, InventoryItemType.EquipmentShoulders,
            InventoryItemType.EquipmentChest, InventoryItemType.EquipmentGloves,
            InventoryItemType.EquipmentLegs, InventoryItemType.EquipmentBoots
        };

        for (int i = 0; i < dropRolls; i++)
        {
            if (_random.NextDouble() < finoChance * rewardMult)
                finos++;
            if (_random.NextDouble() < canecaChance * rewardMult)
                canecas++;
            if (_random.NextDouble() < cigarroChance * rewardMult)
                cigarros++;
            if (_random.NextDouble() < canhaoChance * rewardMult)
                canhaos++;
            if (_random.NextDouble() < shotChance * rewardMult)
                shots++;
            if (_random.NextDouble() < penaltyChance * rewardMult)
                penalties++;
            if (_random.NextDouble() < instrChance * rewardMult)
                instrParts.Add(instrPartTypes[_random.Next(instrPartTypes.Length)]);
            if (_random.NextDouble() < equipChance * rewardMult)
                equipPieces.Add(equipSlotTypes[_random.Next(equipSlotTypes.Length)]);
            if (_random.NextDouble() < 0.002 * rewardMult)
                fitab++;
        }

        return new SurviveModeLevelResult
        {
            Level = level,
            Region = config.Region,
            BiomeName = config.BiomeName,
            Survived = survived,
            SurvivalTimeSeconds = survivalTimeSeconds,
            RequiredTimeSeconds = config.TimerDurationSeconds,
            EnemiesKilled = enemiesKilled,
            XPReward = xpReward,
            FidelisReward = fidelisReward,
            FinosDropped = finos,
            CanecasDropped = canecas,
            CigarrosDropped = cigarros,
            CanhaosDropped = canhaos,
            ShotsDropped = shots,
            PenaltiesDropped = penalties,
            InstrumentPartsDropped = instrParts,
            EquipmentDropped = equipPieces,
            FitabDropped = fitab
        };
    }
}
