using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Configuration;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing battles
/// Handles battle creation, combat simulation, and reward distribution
/// Battles are NOT persisted to database - only win/loss stats are tracked on Character
/// </summary>
public class BattleService : IBattleService
{
    private readonly ICharacterRepository _characterRepository;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BattleService> _logger;
    private readonly MyTunoScalingConfiguration _myTunoScalingConfig;
    private readonly IAuditLogService _auditLogService;
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public BattleService(
        ICharacterRepository characterRepository,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<BattleService> logger,
        IOptions<MyTunoScalingConfiguration> myTunoScalingConfig,
        IAuditLogService auditLogService,
        IStageProgressRepository stageProgressRepository,
        IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _characterRepository = characterRepository;
        _combatEngine = combatEngine;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _myTunoScalingConfig = myTunoScalingConfig.Value;
        _auditLogService = auditLogService;
        _stageProgressRepository = stageProgressRepository;
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Creates and executes a battle vs a specific opponent character
    /// Rewards are NOT applied until FinalizeAndApplyRewardsAsync is called
    /// </summary>
    public async Task<BattleResult> CreateBattleVsOpponentAsync(int playerCharacterId, int opponentCharacterId, CancellationToken cancellationToken = default)
    {
        // Load player character
        var playerCharacter = await _characterRepository.GetByIdAsync(playerCharacterId);
        if (playerCharacter == null)
            throw new EntityNotFoundException(nameof(Character), playerCharacterId);

        // Load opponent character
        var opponentCharacter = await _characterRepository.GetByIdAsync(opponentCharacterId);
        if (opponentCharacter == null)
            throw new EntityNotFoundException(nameof(Character), opponentCharacterId);

        if (playerCharacterId == opponentCharacterId)
            throw new InvalidOperationException("Não podes lutar contra ti mesmo");

        var playerUser = await _userManager.FindByIdAsync(playerCharacter.UserId);
        var opponentUser = await _userManager.FindByIdAsync(opponentCharacter.UserId);
        var playerName = playerUser?.UserName ?? playerCharacter.UserId;
        var opponentName = opponentUser?.UserName ?? opponentCharacter.UserId;
        _logger.LogInformation("Battle started by {PlayerName} against {OpponentName}", playerName, opponentName);

        // Create CPU snapshot of opponent with full HP
        // This ensures the opponent always starts at full health regardless of their persisted state
        var opponentSnapshot = Character.CreateCpuSnapshot(opponentCharacter);

        // Arena fights use base stats — no consumable buffs applied or consumed
        var combatCharacter = playerCharacter;

        // Generate seed for deterministic combat
        var seed = GenerateSeed();

        // Run combat simulation using the snapshot (not the persisted character)
        var combatResult = _combatEngine.Simulate(combatCharacter, opponentSnapshot, seed);

        // Calculate rewards based on outcome
        var (xpReward, fidelisReward) = CalculateRewards(combatResult.Outcome, playerCharacter, opponentCharacter);

        // Calculate arena rating change
        var ratingChange = CalculateRatingChange(combatResult.Outcome, playerCharacter, opponentCharacter);

        // Serialize replay events to JSON
        var replayJson = JsonSerializer.Serialize(combatResult.Events, JsonSerializerConstants.Compact);

        // Create and return battle result (not persisted)
        // No buff usage/expiry tracked — Arena doesn't consume buffs
        return new BattleResult
        {
            BattleId = Guid.NewGuid(),
            AttackerCharacterId = playerCharacterId,
            DefenderCharacterId = opponentCharacterId,
            Seed = seed,
            Outcome = combatResult.Outcome,
            AttackerXP = xpReward,
            AttackerFidelis = fidelisReward,
            ReplayJson = replayJson,
            AttackerFinalHP = combatResult.AttackerFinalHP,
            ShotBuffUsed = false,
            ShotBuffExpired = false,
            ShotBuffBattlesRemaining = playerCharacter.ShotBuffBattlesRemaining,
            PenaltyBuffUsed = false,
            PenaltyBuffExpired = false,
            RatingChange = ratingChange
        };
    }

    /// <summary>
    /// Finalizes a battle and applies all pending rewards and state changes
    /// Should be called after the battle animation finishes
    /// Handles concurrency exceptions by reloading and retrying once
    /// </summary>
    public async Task<bool> FinalizeAndApplyRewardsAsync(BattleResult result, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 1;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await ApplyBattleRewardsAsync(result, cancellationToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        ex,
                        "Concurrency conflict applying battle rewards for character {CharacterId}, retrying...",
                        result.AttackerCharacterId);
                    // Retry once - reload character fresh from database
                    continue;
                }

                // Final retry failed - log error and return false
                _logger.LogError(
                    ex,
                    "Failed to apply battle rewards for character {CharacterId} after {MaxRetries} retries",
                    result.AttackerCharacterId,
                    maxRetries);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Internal method that applies battle rewards
    /// Separated to allow retry logic in FinalizeAndApplyRewardsAsync
    /// </summary>
    private async Task<bool> ApplyBattleRewardsAsync(BattleResult result, CancellationToken cancellationToken = default)
    {
        // Load the attacker character
        var playerCharacter = await _characterRepository.GetByIdAsync(result.AttackerCharacterId);
        if (playerCharacter == null)
        {
            _logger.LogError("Player character {CharacterId} not found for battle finalization", result.AttackerCharacterId);
            return false;
        }

        // Idempotency check — if this battle was already finalized, return cached success
        if (playerCharacter.LastBattleId.HasValue && playerCharacter.LastBattleId.Value == result.BattleId)
        {
            _logger.LogWarning("Duplicate finalization for BattleId {BattleId}, skipping", result.BattleId);
            return true;
        }

        // Always restore full HP after arena battle — players no longer lose HP between battles
        playerCharacter.CurrentHP = null;

        // Arena does NOT consume or expire any buffs — they are stage-only resources.
        // Buffs remain untouched so they aren't wasted on arena fights.

        // Update arena statistics
        switch (result.Outcome)
        {
            case BattleOutcome.AttackerWon:
                playerCharacter.ArenaWins++;
                break;
            case BattleOutcome.DefenderWon:
                playerCharacter.ArenaLosses++;
                break;
            case BattleOutcome.Draw:
                playerCharacter.ArenaDraws++;
                break;
        }

        // Update cooldown tracking
        playerCharacter.LastOpponentId = result.DefenderCharacterId;
        playerCharacter.LastBattleAt = DateTime.UtcNow;
        playerCharacter.LastBattleId = result.BattleId;

        // Re-derive rating change from the *actual* outcome + live character data.
        // result.RatingChange was pre-computed by the deterministic sim; if the
        // interactive session flipped the outcome (e.g. player survived a predicted
        // loss), the pre-computed value would be wrong (-10 on a real win).
        var opponentCharacter = await _characterRepository.GetByIdAsync(result.DefenderCharacterId);
        var ratingChange = opponentCharacter != null
            ? CalculateRatingChange(result.Outcome, playerCharacter, opponentCharacter)
            : result.RatingChange; // fallback if opponent was deleted

        // Apply arena rating change (min 0)
        playerCharacter.ArenaRating = Math.Max(0, playerCharacter.ArenaRating + ratingChange);

        // Sync back so the UI shows the correct value
        result.RatingChange = ratingChange;

        // Arena battles no longer award XP or Fidelis — only rating
        // (kept for drop logic below)

        await _characterRepository.UpdateAsync(playerCharacter);

        // Roll for consumable drops and FITAB if player won
        if (result.Outcome == BattleOutcome.AttackerWon)
        {
            await TryDropConsumablesAsync(playerCharacter.UserId, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Calculates XP and Fidelis rewards based on battle outcome and level difference.
    /// In v5, arena rewards are flat base values scaled only by level difference (no level-power scaling).
    /// </summary>
    private (int xp, decimal fidelis) CalculateRewards(BattleOutcome outcome, Character attacker, Character defender)
    {
        var rewards = _myTunoScalingConfig.BattleRewards;

        // Unified level-diff multiplier for both XP and Fidelis
        var levelDiff = defender.Level - attacker.Level;
        var levelDiffMult = Math.Clamp(1.0 + levelDiff * rewards.LevelDiffScale,
            1.0 - rewards.LevelDiffCap, 1.0 + rewards.LevelDiffCap);

        return outcome switch
        {
            BattleOutcome.AttackerWon => (
                (int)Math.Round(rewards.BaseWinXP * levelDiffMult),
                (decimal)Math.Round((double)rewards.WinReward * levelDiffMult, 2)),
            BattleOutcome.DefenderWon => (0, 0m),
            BattleOutcome.Draw => (
                (int)Math.Round(rewards.BaseDrawXP * levelDiffMult),
                (decimal)Math.Round((double)rewards.DrawReward * levelDiffMult, 2)),
            _ => (0, 0m)
        };
    }

    /// <summary>
    /// Calculates arena rating change based on battle outcome and level differences.
    /// Win: +15 if opponent level is above yours, +10 if close level, +0 if 10+ levels above opponent.
    /// Lose: -10 rating. Draw: 0.
    /// </summary>
    private static int CalculateRatingChange(BattleOutcome outcome, Character attacker, Character defender)
    {
        if (outcome == BattleOutcome.Draw)
            return 0;

        if (outcome == BattleOutcome.DefenderWon)
            return -10;

        // AttackerWon — check if rating should be awarded
        var levelDiff = attacker.Level - defender.Level;

        // If attacker is 10+ levels above defender, no rating gain
        if (levelDiff >= 10)
            return 0;

        // If defender's level is above attacker's, award +15
        if (defender.Level > attacker.Level)
            return 15;

        // Otherwise close level match, award +10
        return 10;
    }

    /// <summary>
    /// Generates a random seed for combat simulation
    /// </summary>
    private static int GenerateSeed()
    {
        return Random.Shared.Next(int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Rolls for consumable drops (Fino, Caneca, Cigarro, Canhão) and adds to player's inventory if successful.
    /// Accepts an already-loaded user entity to batch FITAB changes without an extra round-trip.
    /// </summary>
    private async Task TryDropConsumablesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rewards = _myTunoScalingConfig.BattleRewards;

        // Gate consumable drops behind biome progression (1000-floor biomes)
        // Fino=1(Forest), Cigarro=2001(Mountains), Shot=5001(Caverns), Caneca=11001(Underground), Canhão=7001(Volcanic), Penalty=9001(Sky)
        var stageProgress = await _stageProgressRepository.GetByUserIdAsync(userId);
        var highestStage = stageProgress?.HighestStage ?? 1;

        var drops = new Dictionary<InventoryItemType, int>();

        if (highestStage >= 1 && Random.Shared.NextDouble() < rewards.FinoDropChance)
            drops[InventoryItemType.Fino] = 1;

        if (highestStage >= 11001 && Random.Shared.NextDouble() < rewards.CanecaDropChance)
            drops[InventoryItemType.Caneca] = 1;

        if (highestStage >= 2001 && Random.Shared.NextDouble() < rewards.CigarroDropChance)
            drops[InventoryItemType.Cigarro] = 1;

        if (highestStage >= 7001 && Random.Shared.NextDouble() < rewards.CanhaoDropChance)
            drops[InventoryItemType.Canhao] = 1;

        if (highestStage >= 5001 && Random.Shared.NextDouble() < rewards.ShotDropChance)
            drops[InventoryItemType.Shot] = 1;

        if (highestStage >= 9001 && Random.Shared.NextDouble() < rewards.PenaltyDropChance)
            drops[InventoryItemType.Penalty] = 1;

        if (drops.Count > 0)
            await _inventoryRepository.AddItemsAsync(userId, drops, cancellationToken);

        // Roll for FITAB drop — use a fresh context to avoid stale entity issues
        var fitabRoll = Random.Shared.NextDouble();
        if (fitabRoll < _myTunoScalingConfig.BossMode.FitabDropChanceBattle)
        {
            using var ctx = _contextFactory.CreateDbContext();
            var user = await ctx.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user != null)
            {
                user.FitabBalance++;
                await ctx.SaveChangesAsync(cancellationToken);
            }
        }
    }
    
}
