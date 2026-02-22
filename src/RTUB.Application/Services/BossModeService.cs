using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Helpers;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service for Boss Mode — an endless boss-only mode.
/// Every stage is a boss fight. Bosses start at stage mode 500+ difficulty.
/// Requires 1 FITAB to enter.
/// </summary>
public class BossModeService : IBossModeService
{
    private readonly IBossModeProgressRepository _bossModeProgressRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BossModeService> _logger;
    private readonly MyTunoScalingConfiguration _config;
    private readonly IStageBiomeService _biomeService;
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IWebHostEnvironment _environment;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IMemoryCache _memoryCache;

    public BossModeService(
        IBossModeProgressRepository bossModeProgressRepository,
        ICharacterRepository characterRepository,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<BossModeService> logger,
        IOptions<MyTunoScalingConfiguration> config,
        IStageBiomeService biomeService,
        IStageProgressRepository stageProgressRepository,
        IWebHostEnvironment environment,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IMemoryCache memoryCache)
    {
        _bossModeProgressRepository = bossModeProgressRepository;
        _characterRepository = characterRepository;
        _combatEngine = combatEngine;
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
    public async Task<BossModeProgress> GetOrCreateBossModeProgressAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var progress = await _bossModeProgressRepository.GetByUserIdAsync(userId);
        if (progress != null)
        {
            // Check and apply daily reset if a new day has started
            if (progress.CheckAndApplyDailyReset())
            {
                await _bossModeProgressRepository.UpdateAsync(progress);
            }
            return progress;
        }

        progress = BossModeProgress.Create(userId);
        await _bossModeProgressRepository.AddAsync(progress);
        return progress;
    }

    /// <inheritdoc />
    public async Task<BossModeProgress?> GetBossModeProgressAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return await _bossModeProgressRepository.GetByUserIdAsync(userId);
    }

    /// <inheritdoc />
    public async Task<BossModeProgress> StartBossModeRunAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Guard: if the user already has a run in progress, return it without
        // deducting FITAB.  This prevents double-charge caused by Blazor Server
        // prerender (OnInitializedAsync fires twice — once during prerender and
        // once when the SignalR circuit connects, with a NEW component instance).
        var existingProgress = await GetOrCreateBossModeProgressAsync(userId, cancellationToken);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new Core.Exceptions.EntityNotFoundException(nameof(ApplicationUser), userId);

        if (existingProgress.CurrentBossStage > 0)
        {
            _logger.LogInformation(
                "User {UserName} already has a Boss Mode run in progress at stage {Stage}. Skipping FITAB deduction.",
                user.UserName, existingProgress.CurrentBossStage);
            return existingProgress;
        }

        // Check level requirement
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null || character.Level < 100)
            throw new InvalidOperationException("Level 100 required to enter Boss Mode.");

        if (user.FitabBalance < 1)
            throw new InvalidOperationException("Not enough FITAB to enter Boss Mode. You need at least 1 FITAB.");

        // Deduct 1 FITAB using a short-lived context to avoid change tracker pollution.
        // In Blazor Server, the scoped DbContext is long-lived (per circuit). Using a separate
        // context for user balance updates prevents stale ConcurrencyStamp issues.
        await using (var fitabContext = _contextFactory.CreateDbContext())
        {
            var freshUser = await fitabContext.Users.FindAsync(new object[] { user.Id }, cancellationToken);
            if (freshUser == null || freshUser.FitabBalance < 1)
                throw new InvalidOperationException("Not enough FITAB to enter Boss Mode. You need at least 1 FITAB.");
            freshUser.FitabBalance -= 1;
            await fitabContext.SaveChangesAsync(cancellationToken);
            user.FitabBalance = freshUser.FitabBalance;
        }

        // Start new run
        existingProgress.StartRun();
        await _bossModeProgressRepository.UpdateAsync(existingProgress);

        _logger.LogInformation(
            "User {UserName} started Boss Mode run #{Run}. FITAB balance: {Balance}",
            user.UserName, existingProgress.TotalRunsAttempted, user.FitabBalance);

        return existingProgress;
    }

    /// <inheritdoc />
    public async Task<BossModeBattleResult> ExecuteBossBattleAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new Core.Exceptions.EntityNotFoundException(nameof(Character), characterId);

        var progress = await GetOrCreateBossModeProgressAsync(character.UserId, cancellationToken);

        if (progress.CurrentBossStage <= 0)
            throw new InvalidOperationException("No Boss Mode run in progress. Start a run first.");

        var bossStage = progress.CurrentBossStage;
        var equivalentStage = GetEquivalentStage(bossStage);

        // Check if shot buff is active
        var hasShotBuff = character.ShotBuffBattlesRemaining > 0;
        var combatCharacter = hasShotBuff
            ? Character.CreateShotBuffedCopy(character)
            : character;

        // Get boss sprite
        var bossSprite = await GetBossSpriteAsync(bossStage, cancellationToken);
        var bossPlacement = 0; // Terrestrial by default

        // Create boss enemy using equivalent stage difficulty
        var bossEnemy = CreateBossEnemy(bossStage, equivalentStage);
        var bossName = $"Boss #{bossStage}";
        bossEnemy.User = new ApplicationUser { UserName = bossName };

        // If the boss has remaining HP from a previous run today, apply it
        var bossFullHP = bossEnemy.TotalHP;
        if (progress.DailyBossRemainingHP.HasValue && progress.DailyBossRemainingHP.Value > 0
            && progress.DailyBossRemainingHP.Value < bossFullHP)
        {
            bossEnemy.CurrentHP = progress.DailyBossRemainingHP.Value;
        }

        var seed = Random.Shared.Next(int.MinValue, int.MaxValue);

        // Simulate combat
        var combatResult = _combatEngine.Simulate(combatCharacter, bossEnemy, seed);

        var battleData = new
        {
            Events = combatResult.Events,
            EnemyCount = 1,
            EnemySprites = new List<string> { bossSprite },
            EnemyPlacements = new List<int> { bossPlacement },
            BiomeName = "Jeans",
            EnemyStats = new[]
            {
                new
                {
                    Name = bossName,
                    HP = bossEnemy.CurrentHP ?? bossEnemy.TotalHP,
                    MaxHP = bossEnemy.TotalHP,
                    Power = bossEnemy.TotalPower,
                    Defense = bossEnemy.TotalDefense,
                    Speed = bossEnemy.TotalSpeed,
                    ActionTime = Math.Round(bossEnemy.ActionTime, 1)
                }
            }
        };

        var replayJson = JsonSerializer.Serialize(battleData, JsonSerializerConstants.Compact);

        var stageProgress = await _stageProgressRepository.GetByUserIdAsync(character.UserId);
        var highestStage = stageProgress?.HighestStage ?? 1;

        var (fidelisReward, leitaoDropped, finosDropped, canecasDropped, cigarrosDropped, canhaosDropped, shotsDropped, penaltiesDropped, instrumentPartsDropped) =
            CalculateBossRewards(combatResult, bossStage, character.Level, highestStage);

        // Update progress (hasShotBuff and hasPenaltyBuff tracked for potential future use)
        await UpdateProgressAfterBattle(character, progress, combatResult, bossFullHP);

        return new BossModeBattleResult
        {
            BattleId = Guid.NewGuid(),
            CharacterId = characterId,
            BossStage = bossStage,
            BossName = bossName,
            Seed = seed,
            Outcome = combatResult.Outcome,
            XPReward = 0,
            FidelisReward = fidelisReward,
            LeitaoDropped = leitaoDropped,
            FinosDropped = finosDropped,
            CanecasDropped = canecasDropped,
            CigarrosDropped = cigarrosDropped,
            CanhaosDropped = canhaosDropped,
            ShotsDropped = shotsDropped,
            PenaltiesDropped = penaltiesDropped,
            InstrumentPartsDropped = instrumentPartsDropped,
            ReplayJson = replayJson,
            PlayerFinalHP = combatResult.AttackerFinalHP
        };
    }

    /// <inheritdoc />
    public async Task ApplyBossRunRewardsAsync(
        int characterId, decimal fidelis, int leitao, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0,
        long? restoreHp = null,
        Dictionary<InventoryItemType, int>? instrumentParts = null,
        bool expireShotBuff = false,
        bool expirePenaltyBuff = false,
        int startBossFloor = 0,
        int endBossFloor = 0,
        CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
        {
            _logger.LogWarning("ApplyBossRunRewardsAsync: Character {CharacterId} not found", characterId);
            return;
        }

        // Always restore full HP after boss run — players no longer lose HP between battles
        character.CurrentHP = null;

        // Expire buffs once per run (not per-boss).
        // Must happen BEFORE saving so HP scaling (when shot buff reaches 0) is persisted.
        if (expireShotBuff && character.ShotBuffBattlesRemaining > 0)
        {
            character.ExpireShotBuff();
        }
        if (character.CigarroShieldHitsRemaining > 0)
        {
            character.ExpireCigarroBuff();
        }
        // Canhão and Penalty are now timed buffs — they expire automatically via DateTime

        await _characterRepository.UpdateAsync(character);

        if (fidelis <= 0 && leitao <= 0 && finos <= 0 && canecas <= 0 && cigarros <= 0 && canhaos <= 0 && shots <= 0 && penalties <= 0 &&
            (instrumentParts == null || instrumentParts.Count == 0))
            return;

        var user = await _userManager.FindByIdAsync(character.UserId);
        if (user != null && fidelis > 0)
        {
            // Apply Fidelis earned multiplier from Improvements upgrade
            user.FidelisBalance += fidelis * (decimal)character.FidelisEarnedMultiplier;
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
        if (leitao > 0) allDrops[InventoryItemType.Leitao] = leitao;

        if (instrumentParts != null)
        {
            foreach (var (partType, quantity) in instrumentParts)
                allDrops[partType] = allDrops.GetValueOrDefault(partType) + quantity;
        }

        if (allDrops.Count > 0)
            await _inventoryRepository.AddItemsAsync(character.UserId, allDrops, cancellationToken);

        var loot = new List<string>();
        if (fidelis > 0)  loot.Add($"+{fidelis} Fidelis");
        if (leitao > 0)   loot.Add($"+{leitao} Leitão");
        if (finos > 0)    loot.Add($"+{finos} finos");
        if (canecas > 0)  loot.Add($"+{canecas} canecas");
        if (cigarros > 0) loot.Add($"+{cigarros} cigarros");
        if (canhaos > 0)  loot.Add($"+{canhaos} canhaos");
        if (shots > 0)    loot.Add($"+{shots} shots");
        if (penalties > 0) loot.Add($"+{penalties} penalties");

        var instrTotal = instrumentParts?.Values.Sum() ?? 0;
        if (instrTotal > 0) loot.Add($"+{instrTotal} instrument parts");

        var floorRange = startBossFloor > 0 && endBossFloor > 0
            ? $"(Floor {startBossFloor} - Floor {endBossFloor})"
            : string.Empty;

        _logger.LogInformation(
            "Applied boss run rewards for {UserName} {FloorRange}: {Loot}",
            user?.UserName ?? "unknown", floorRange, loot.Count > 0 ? string.Join(", ", loot) : "no rewards");
    }

    /// <inheritdoc />
    public async Task<bool> CancelBossRunAsync(int characterId, long restoreHp, int restoreShotBuffBattles = 0, int restoreCigarroShield = 0, DateTime? restoreCanhaoExpiresAt = null, DateTime? restorePenaltyExpiresAt = null, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;
        // Pre-fetch character for logging (available in catch blocks)
        var character = await _characterRepository.GetByIdAsync(characterId);
        var cancelUser = character != null ? await _userManager.FindByIdAsync(character.UserId) : null;
        var userName = cancelUser?.UserName ?? $"CharId:{characterId}";

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Re-fetch character to get fresh state after potential retry
                character = await _characterRepository.GetByIdAsync(characterId);
                if (character == null) return false;

                var progress = await _bossModeProgressRepository.GetByUserIdAsync(character.UserId);
                if (progress == null) return false;

                // Restore character state
                character.CurrentHP = restoreHp;
                character.ShotBuffBattlesRemaining = restoreShotBuffBattles;
                character.CigarroShieldHitsRemaining = restoreCigarroShield;
                character.CanhaoBuffExpiresAt = restoreCanhaoExpiresAt;
                character.PenaltyBuffExpiresAt = restorePenaltyExpiresAt;
                await _characterRepository.UpdateAsync(character);

                // End the run
                progress.EndRun();
                await _bossModeProgressRepository.UpdateAsync(progress);

                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "CancelBossRunAsync: Concurrency conflict, retrying ({Attempt}/{Max})...", attempt + 1, maxRetries);
                    await Task.Delay(100 * (attempt + 1), cancellationToken);
                    continue;
                }
                _logger.LogError(ex, "CancelBossRunAsync: Failed after {Max} retries for {UserName}", maxRetries, userName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling boss run for {UserName}", userName);
                return false;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<string> GetBossSpriteAsync(int bossStage, CancellationToken cancellationToken = default)
    {
        var spritePath = _config.BossMode.EnemySpritePath;
        var fullPath = Path.Combine(_environment.WebRootPath, spritePath);
        var cacheKey = $"boss_sprites:{spritePath}";

        try
        {
            if (!_memoryCache.TryGetValue(cacheKey, out List<string>? cachedFiles))
            {
                if (Directory.Exists(fullPath))
                {
                    cachedFiles = Directory.GetFiles(fullPath, "*.png")
                        .Concat(Directory.GetFiles(fullPath, "*.webp"))
                        .Concat(Directory.GetFiles(fullPath, "*.jpg"))
                        .ToList();
                    _memoryCache.Set(cacheKey, cachedFiles, TimeSpan.FromMinutes(5));
                }
            }

            if (cachedFiles != null && cachedFiles.Count > 0)
            {
                // Use bossStage to deterministically pick a sprite so it stays
                // consistent across retries and doesn't change mid-run.
                var selectedFile = cachedFiles[Math.Abs(bossStage) % cachedFiles.Count];
                var relativePath = Path.GetRelativePath(_environment.WebRootPath, selectedFile)
                    .Replace('\\', '/');
                return $"/{relativePath}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load boss sprites from {Path}", fullPath);
        }

        // Fallback
        return "/sprites/games/my-tuno/enemies/forest/boss_1_bear.png";
    }

    /// <inheritdoc />
    public string GetBackgroundPath()
    {
        return _config.BossMode.BackgroundPath;
    }

    /// <inheritdoc />
    public int GetEquivalentStage(int bossStage)
    {
        var scaling = Math.Max(1, _config.BossMode.BossStageScaling);
        return _config.BossMode.StageOffset + (bossStage - 1) * scaling;
    }

    /// <summary>
    /// Creates a temporary boss character for combat using tier-based stats.
    /// Maps bossStage → equivalent stage → tier lookup, then applies boss stat multiplier.
    /// </summary>
    private Character CreateBossEnemy(int bossStage, int equivalentStage)
    {
        var bossConfig = _config.BossMode;
        var tier = _biomeService.GetEnemyTierForStage(equivalentStage);
        var bossMult = bossConfig.BossStatMultiplier;
        var stageMult = _biomeService.GetStageProgressionMultiplier(equivalentStage);

        // Use tier boss stats, further amplified by boss mode multiplier + per-stage growth
        var hp = Math.Max(1, (long)(tier.BossHP * bossMult * stageMult));
        var power = Math.Max(1, (long)(tier.BossPower * bossMult * stageMult));
        var defense = Math.Max(1, (long)(tier.BossDefense * bossMult * stageMult));
        var speed = (long)tier.Speed;
        var critChance = Math.Min(tier.CritChance, _config.Combat.CriticalChanceCap);

        // Boss action time gets faster as boss stages progress (much more aggressive)
        var actionTimeTier = (bossStage - 1) / 5;
        var actionTime = Math.Max(0.8, 4.0 - (actionTimeTier * 0.4));

        var bossName = $"Boss #{bossStage}";
        return Character.CreateStageEnemy(hp, power, speed, defense, critChance, bossName, actionTime);
    }

    /// <summary>
    /// Calculates rewards for defeating a boss in Boss Mode.
    /// Boss Mode no longer awards XP — only Fidelis, Leitão, and consumable drops.
    /// </summary>
    private (decimal fidelis, int leitao, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties, List<InventoryItemType> instrumentParts) CalculateBossRewards(
        CombatResult combatResult, int bossStage, int characterLevel, int highestStage = 1)
    {
        if (combatResult.Outcome != BattleOutcome.AttackerWon)
            return (0m, 0, 0, 0, 0, 0, 0, 0, new List<InventoryItemType>());

        var random = Random.Shared;
        var bossConfig = _config.BossMode;
        var dropRates = bossConfig.DropRates;

        // Fidelis: tier-based reward × biome multiplier × level bonus
        var equivalentStage = GetEquivalentStage(bossStage);
        var baseFidelis = bossConfig.FidelisRewards.BossWin;
        var biomeRewardMult = _biomeService.GetRewardMultiplierForStage(equivalentStage);
        var rewardConfig = bossConfig.RewardCurve;
        var rawLevelBonus = 1.0 + (characterLevel - 1) * rewardConfig.LevelBonusPerLevel;
        var levelBonus = Math.Min(rawLevelBonus, rewardConfig.LevelBonusCap);
        var fidelisReward = Math.Round(baseFidelis * (decimal)(biomeRewardMult * levelBonus), 2);

        // Leitão drop — Boss Mode exclusive currency (15% chance for 1 per boss kill)
        var leitaoDropped = 0;
        if (random.NextDouble() < dropRates.LeitaoDropChance)
            leitaoDropped++;

        // Drop rolls
        var finosDropped = 0;
        var canecasDropped = 0;
        var cigarrosDropped = 0;
        var canhaosDropped = 0;
        var shotsDropped = 0;
        var penaltiesDropped = 0;
        var instrumentPartsDropped = new List<InventoryItemType>();

        // Gate consumable drops behind biome progression (1000-floor biomes)
        // Fino=1(Forest), Shot=1001(Swamp), Cigarro=3001(Snowy), Caneca=5001(Caverns), Canhão=7001(Volcanic), Penalty=9001(Sky)
        if (highestStage >= 1 && random.NextDouble() < dropRates.FinoDropChance) finosDropped++;
        if (highestStage >= 5001 && random.NextDouble() < dropRates.CanecaDropChance) canecasDropped++;
        if (highestStage >= 3001 && random.NextDouble() < dropRates.CigarroDropChance) cigarrosDropped++;
        if (highestStage >= 7001 && random.NextDouble() < dropRates.CanhaoDropChance) canhaosDropped++;
        if (highestStage >= 1001 && random.NextDouble() < dropRates.ShotDropChance) shotsDropped++;
        if (highestStage >= 9001 && random.NextDouble() < dropRates.PenaltyDropChance) penaltiesDropped++;

        var instrumentTypes = InstrumentTypeHelper.GameInstrumentTypesArray;
        if (random.NextDouble() < dropRates.InstrumentPartDropChance)
        {
            var randomInstrument = instrumentTypes[random.Next(instrumentTypes.Length)];
            instrumentPartsDropped.Add(InstrumentTypeHelper.ToInventoryPartType(randomInstrument));
        }

        return (fidelisReward, leitaoDropped, finosDropped, canecasDropped, cigarrosDropped, canhaosDropped, shotsDropped, penaltiesDropped, instrumentPartsDropped);
    }

    /// <summary>
    /// Updates character HP and boss mode progress after a battle.
    /// Both entities are saved in a single SaveChangesAsync call to avoid
    /// change tracker conflicts from multiple sequential saves.
    /// On defeat, saves the boss's remaining HP so the next run continues where this one left off.
    /// </summary>
    /// <inheritdoc />
    public async Task ConfirmBossVictoryAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var progress = await GetOrCreateBossModeProgressAsync(userId, cancellationToken);

        // Only advance if the run is still active (CurrentBossStage > 0).
        // This is the deferred advancement from UpdateProgressAfterBattle.
        if (progress.CurrentBossStage > 0)
        {
            progress.AdvanceBossStage();
            await _bossModeProgressRepository.UpdateAsync(progress);
        }
    }

    /// <inheritdoc />
    public async Task RecordInteractiveDefeatAsync(string userId, long bossRemainingHP, long bossMaxHP, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var progress = await GetOrCreateBossModeProgressAsync(userId, cancellationToken);

        // Always save the boss's remaining HP from the interactive session.
        // The pre-computed engine may have already ended the run (CurrentBossStage=0)
        // and saved a different boss HP — overwrite it with the interactive value.
        if (bossRemainingHP > 0)
            progress.SaveBossHP(bossRemainingHP, bossMaxHP);

        // End the run if still active (pre-computed WIN case where EndRun was deferred).
        if (progress.CurrentBossStage > 0)
            progress.EndRun();

        await _bossModeProgressRepository.UpdateAsync(progress);
    }

    /// <inheritdoc />
    public async Task CorrectInteractiveWinAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var progress = await GetOrCreateBossModeProgressAsync(userId, cancellationToken);

        // EndRun() set CurrentBossStage = 0 but preserved DailyBossStage.
        // Restore the stage the player was fighting, then advance past it.
        if (progress.CurrentBossStage <= 0 && progress.DailyBossStage > 0)
        {
            progress.CurrentBossStage = progress.DailyBossStage;
            progress.AdvanceBossStage();
            await _bossModeProgressRepository.UpdateAsync(progress);
        }
    }

    /// <inheritdoc />
    public async Task SaveBossRemainingHPAsync(string userId, long bossRemainingHP, long bossMaxHP, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var progress = await GetOrCreateBossModeProgressAsync(userId, cancellationToken);

        if (bossRemainingHP > 0)
            progress.SaveBossHP(bossRemainingHP, bossMaxHP);
        else
        {
            // Boss was killed — clear remaining HP
            progress.DailyBossRemainingHP = null;
            progress.DailyBossMaxHP = 0;
        }

        await _bossModeProgressRepository.UpdateAsync(progress);
    }

    private async Task UpdateProgressAfterBattle(
        Character character, BossModeProgress progress, CombatResult combatResult, long bossMaxHP)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Update character HP (in buffed scale if buff was active)
                if (combatResult.AttackerFinalHP > 0)
                    character.CurrentHP = combatResult.AttackerFinalHP;
                else
                    character.CurrentHP = null;


                // In boss mode, buffs are run-scoped (1 charge per entire run),
                // NOT per-boss. Expiry happens when the run ends via
                // ApplyBossRunRewardsAsync, not here.

                // Update boss progress (only on first attempt — retries already applied these)
                if (attempt == 0)
                {
                    if (combatResult.Outcome == BattleOutcome.AttackerWon)
                    {
                        // Don't advance stage here — defer to ConfirmBossVictoryAsync
                        // which is called from OnBattleFinished after the interactive
                        // session confirms the win. This prevents DailyBossStage from
                        // advancing prematurely if the player disconnects mid-animation.
                    }
                    else
                    {
                        // Save the boss's remaining HP so the next run picks up where this left off
                        if (combatResult.DefenderFinalHP > 0)
                        {
                            progress.SaveBossHP(combatResult.DefenderFinalHP, bossMaxHP);
                        }
                        progress.EndRun();
                    }
                }

                // Save both entities in one SaveChangesAsync call.
                // UpdateAsync marks progress as Modified and calls SaveChangesAsync.
                // EF Core change detection will also pick up the character.CurrentHP change
                // and save both in a single database round-trip.
                await _bossModeProgressRepository.UpdateAsync(progress);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "UpdateProgressAfterBattle: Concurrency conflict, retrying ({Attempt}/{Max})...", attempt + 1, maxRetries);
                    await Task.Delay(50 * (attempt + 1));

                    // Reload game entities for retry
                    try
                    {
                        await _characterRepository.ReloadAsync(character);
                        await _bossModeProgressRepository.ReloadAsync(progress);
                    }
                    catch (Exception reloadEx)
                    {
                        _logger.LogWarning(reloadEx, "Failed to reload entities for retry");
                    }

                    continue;
                }

                _logger.LogError(ex, "UpdateProgressAfterBattle: Failed after {Max} retries", maxRetries);
            }
        }
    }
}
