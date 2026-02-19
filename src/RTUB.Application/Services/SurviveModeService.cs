using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service for Survive Mode â€” a survivor.io-inspired game.
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
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IMemoryCache _memoryCache;

    // Survive mode constants
    private const double BaseTimerSeconds = 300.0;         // Level 1 timer (5 minutes)
    private const double TimerIncreasePerLevel = 30.0;     // +30s per level
    private const double MaxTimerSeconds = 870.0;          // Cap at level 20 = 14.5 min
    private const int BaseEnemyCount = 3;                  // Starting enemies (same for all levels)
    private const int MaxEnemyCountBase = 15;              // Base max alive enemies (same for all levels)
    private const int MaxEnemyCountCap = 50;               // Hard cap on alive enemies
    private const double BaseEnemySpeed = 60.0;            // Pixels per second (same for all levels)
    private const double MaxEnemySpeedCap = 250.0;         // Speed cap
    private const double BasePlayerSpeed = 120.0;          // Player is faster than enemies
    private const double PlayerSpeedDecayPerLevel = 2.0;   // Gets slightly slower each level
    private const double MinPlayerSpeed = 80.0;            // Never slower than this
    private const double BaseSpawnInterval = 3.0;          // Seconds between spawn waves (same for all levels)
    private const double MinSpawnInterval = 0.5;           // Fastest spawn rate
    private const double BaseEnemyScale = 0.6;             // Enemy sprite scale (same for all levels)
    private const double MinEnemyScale = 0.3;
    private const int MapWidth = 2400;                     // Scrollable map
    private const int MapHeight = 2400;
    private const int ViewportWidth = 800;                 // Visible area
    private const int ViewportHeight = 500;
    private const int MaxLevel = 20;                       // 20 levels total

    // All levels start the same â€” difficulty ramps over TIME within each level,
    // not across levels. The per-biome time-ramp rates below control how fast
    // spawns and enemy speed increase every minute during a level.
    private static readonly (double spawnRamp, double speedRamp)[] BiomeTimeRamp = new[]
    {
        (0.08, 0.05), // Level 1  - Forest:     gentle ramp
        (0.10, 0.06), // Level 2  - Swamp:      slightly faster ramp
        (0.12, 0.08), // Level 3  - Mountains:  moderate
        (0.14, 0.10), // Level 4  - Snowy:      noticeable pressure
        (0.16, 0.12), // Level 5  - Tropical:   challenging mid-game
        (0.18, 0.14), // Level 6  - Caverns:    aggressive spawn ramp
        (0.20, 0.16), // Level 7  - Desert:     demanding
        (0.22, 0.18), // Level 8  - Volcanic:   intense
        (0.24, 0.20), // Level 9  - Ruins:      very hard
        (0.26, 0.22), // Level 10 - Dark:       punishing
        (0.28, 0.24), // Level 11 - Light:      extreme
        (0.30, 0.26), // Level 12 - Void
        (0.32, 0.28), // Level 13 - Void
        (0.34, 0.30), // Level 14 - Void
        (0.36, 0.32), // Level 15 - Void
        (0.38, 0.34), // Level 16 - Void
        (0.40, 0.35), // Level 17 - Void
        (0.42, 0.36), // Level 18 - Void
        (0.44, 0.37), // Level 19 - Void
        (0.46, 0.38), // Level 20 - Void: brutal final level
    };

    // Reward constants — scale up per level so harder biomes are worth more
    private const int BaseXPPerLevel = 30;
    private const decimal BaseFidelisPerLevel = 25;
    private const double XPPerEnemyKill = 3.0;
    private const double FidelisPerEnemyKill = 1.0;
    private const double RewardScalePerLevel = 0.15;  // +15% rewards per level beyond 1

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
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IMemoryCache memoryCache)
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
        _contextFactory = contextFactory;
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<SurviveModeProgress> GetOrCreateProgressAsync(string userId, CancellationToken cancellationToken = default)
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
    public async Task<SurviveModeProgress?> GetProgressAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _progressRepository.GetByUserIdAsync(userId);
    }

    /// <inheritdoc />
    public async Task<SurviveModeProgress> StartRunAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        if ((character.CurrentHP ?? character.HP) <= 0)
            throw new InvalidOperationException("Character is dead â€” cannot start a survive run");

        var progress = await GetOrCreateProgressAsync(character.UserId, cancellationToken);

        if (progress.IsRunActive)
        {
            _logger.LogWarning("User {UserId} tried to start survive run while one is active. Cancelling old run.",
                character.UserId);
            progress.CancelRun();
        }

        progress.StartRun();
        await _progressRepository.UpdateAsync(progress);

        var user = await _userManager.FindByIdAsync(character.UserId);
        _logger.LogInformation("Survive Mode initiated by {UserName}", user?.UserName ?? character.UserId);

        return progress;
    }

    /// <inheritdoc />
    public async Task<SurviveModeProgress> SetStartLevelAsync(string userId, int targetLevel, CancellationToken cancellationToken = default)
    {
        var progress = await GetOrCreateProgressAsync(userId, cancellationToken);

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

        // Get biome reward multiplier from stage mode config (difficulty multipliers are always 1.0)
        var biomeConfig = _config.StageMode?.Biomes?.FirstOrDefault(b =>
            string.Equals(b.Name, biomeName, StringComparison.OrdinalIgnoreCase));

        var difficultyMult = 1.0;
        var rewardMult = biomeConfig?.RewardMultiplier ?? 1.0 + (level - 1) * 0.2;

        // Timer: 5 min base + 30s per level, caps at MaxTimerSeconds
        var timer = Math.Min(BaseTimerSeconds + (level - 1) * TimerIncreasePerLevel, MaxTimerSeconds);

        // All levels start with the SAME baseline stats — like level 1.
        // Difficulty within each level is controlled 100% by the BiomeTimeRamp.
        var baseCount = BaseEnemyCount;
        var maxCount = MaxEnemyCountBase;

        // Enemy speed: always starts at the same base (ramp handles acceleration)
        var enemySpeed = BaseEnemySpeed;
        var maxEnemySpeed = MaxEnemySpeedCap;

        // Player speed: starts high, slowly decreases (still faster than enemies)
        var playerSpeed = Math.Max(BasePlayerSpeed - (level - 1) * PlayerSpeedDecayPerLevel, MinPlayerSpeed);

        // Spawn interval: same base for all levels (ramp handles acceleration)
        var spawnInterval = BaseSpawnInterval;

        // Enemy scale: same for all levels
        var enemyScale = BaseEnemyScale;

        // Elite enemies appear from level 3+
        var hasElites = level >= 3;
        var eliteChance = hasElites ? Math.Min(0.05 + (level - 3) * 0.03, 0.30) : 0.0;

        // Void (level 11) is the final level â€” no bosses, timer expiry = win
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
    public async Task<SurviveModeLevelResult> CompleteLevelAsync(int characterId, int enemiesKilled, double survivalTimeSeconds, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        var progress = await GetOrCreateProgressAsync(character.UserId, cancellationToken);

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
        await _progressRepository.UpdateAsync(progress);

        var completedUser = await _userManager.FindByIdAsync(character.UserId);
        _logger.LogInformation("Survive Mode ended on level {Level} by {UserName} - rewards: {XP} XP, {Fidelis} Fidelis",
            level, completedUser?.UserName ?? character.UserId, result.XPReward, result.FidelisReward);

        return result;
    }

    /// <inheritdoc />
    public async Task<SurviveModeLevelResult> EndRunAsync(int characterId, int enemiesKilled, double survivalTimeSeconds, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        var progress = await GetOrCreateProgressAsync(character.UserId, cancellationToken);

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
        await _progressRepository.UpdateAsync(progress);

        var diedUser = await _userManager.FindByIdAsync(character.UserId);
        _logger.LogInformation("Survive Mode ended on level {Level} by {UserName} - rewards: {XP} XP, {Fidelis} Fidelis",
            level, diedUser?.UserName ?? character.UserId, result.XPReward, result.FidelisReward);

        return result;
    }

    /// <inheritdoc />
    public Task ApplyRunRewardsAsync(int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0,
        Dictionary<InventoryItemType, int>? instrumentParts = null,
        CancellationToken cancellationToken = default)
    {
        // Survive mode no longer grants rewards — no-op.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> CancelRunAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId)
            ?? throw new InvalidOperationException("Character not found");

        var progress = await GetOrCreateProgressAsync(character.UserId, cancellationToken);

        if (!progress.IsRunActive)
            return false;

        progress.CancelRun();
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
    public async Task<List<string>> GetEnemySpritesAsync(int level, int count, CancellationToken cancellationToken = default)
    {
        var biomeName = SurviveModeProgress.GetBiomeName(level).ToLowerInvariant();
        var spritePath = $"sprites/games/my-tuno/enemies/{biomeName}";

        // Look for sprite files on disk (cached)
        var webRootPath = _environment.WebRootPath;
        var fullPath = Path.Combine(webRootPath, spritePath.Replace('/', Path.DirectorySeparatorChar));
        var cacheKey = $"survive_enemy_sprites:{biomeName}";

        var sprites = new List<string>();

        if (!_memoryCache.TryGetValue(cacheKey, out List<string>? cachedFiles))
        {
            if (Directory.Exists(fullPath))
            {
                cachedFiles = Directory.GetFiles(fullPath, "*.png")
                    .Where(f => !Path.GetFileName(f).StartsWith("boss_", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                _memoryCache.Set(cacheKey, cachedFiles, TimeSpan.FromMinutes(5));
            }
        }

        if (cachedFiles != null && cachedFiles.Count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                var file = cachedFiles[Random.Shared.Next(cachedFiles.Count)];
                var relativePath = "/" + Path.GetRelativePath(webRootPath, file).Replace('\\', '/');
                sprites.Add(relativePath);
            }
            return sprites;
        }

        // Fallback: try to find enemies from the stage enemy DB
        var region = SurviveModeProgress.GetRegionForLevel(level);
        await using var dbContext = _contextFactory.CreateDbContext();
        var enemies = await dbContext.StageEnemies
            .Where(e => e.Region == region && e.Type == EnemyType.Normal && e.SpritePath != null)
            .Select(e => e.SpritePath!)
            .ToListAsync(cancellationToken);

        if (enemies.Count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                sprites.Add(enemies[Random.Shared.Next(enemies.Count)]);
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
    public Task<List<string>> GetBossSpritesAsync(int level, int count, CancellationToken cancellationToken = default)
    {
        var biomeName = SurviveModeProgress.GetBiomeName(level).ToLowerInvariant();
        var spritePath = $"sprites/games/my-tuno/enemies/{biomeName}";
        var webRootPath = _environment.WebRootPath;
        var fullPath = Path.Combine(webRootPath, spritePath.Replace('/', Path.DirectorySeparatorChar));
        var bossCacheKey = $"survive_boss_sprites:{biomeName}";

        var sprites = new List<string>();

        if (!_memoryCache.TryGetValue(bossCacheKey, out List<string>? cachedBossFiles))
        {
            if (Directory.Exists(fullPath))
            {
                cachedBossFiles = Directory.GetFiles(fullPath, "boss_*.png").ToList();
                _memoryCache.Set(bossCacheKey, cachedBossFiles, TimeSpan.FromMinutes(5));
            }
        }

        if (cachedBossFiles != null && cachedBossFiles.Count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                var file = cachedBossFiles[Random.Shared.Next(cachedBossFiles.Count)];
                var relativePath = "/" + Path.GetRelativePath(webRootPath, file).Replace('\\', '/');
                sprites.Add(relativePath);
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
        // Survive mode no longer grants rewards — it's a pure challenge mode.
        return new SurviveModeLevelResult
        {
            Level = level,
            Region = config.Region,
            BiomeName = config.BiomeName,
            Survived = survived,
            SurvivalTimeSeconds = survivalTimeSeconds,
            RequiredTimeSeconds = config.TimerDurationSeconds,
            EnemiesKilled = enemiesKilled,
            XPReward = 0,
            FidelisReward = 0,
            FinosDropped = 0,
            CanecasDropped = 0,
            CigarrosDropped = 0,
            CanhaosDropped = 0,
            ShotsDropped = 0,
            PenaltiesDropped = 0,
            InstrumentPartsDropped = new List<InventoryItemType>(),
            FitabDropped = 0
        };
    }
}
