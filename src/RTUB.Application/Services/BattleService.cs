using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
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
    private readonly IMatchmakingService _matchmakingService;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BattleService> _logger;
    private readonly MyTunoScalingConfiguration _myTunoScalingConfig;

    // Reward constants
    private const int BaseWinXP = 50;
    private const int BaseDrawXP = 30;

    public BattleService(
        ICharacterRepository characterRepository,
        IMatchmakingService matchmakingService,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<BattleService> logger,
        IOptions<MyTunoScalingConfiguration> myTunoScalingConfig)
    {
        _characterRepository = characterRepository;
        _matchmakingService = matchmakingService;
        _combatEngine = combatEngine;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _myTunoScalingConfig = myTunoScalingConfig.Value;
    }

    /// <summary>
    /// Creates and executes a battle vs a specific opponent character
    /// Rewards are NOT applied until FinalizeAndApplyRewardsAsync is called
    /// </summary>
    public async Task<BattleResult> CreateBattleVsOpponentAsync(int playerCharacterId, int opponentCharacterId)
    {
        // Load player character
        var playerCharacter = await _characterRepository.GetByIdAsync(playerCharacterId);
        if (playerCharacter == null)
            throw new EntityNotFoundException(nameof(Character), playerCharacterId);

        // Check if player character is alive
        if (!playerCharacter.IsAlive())
            throw new InvalidOperationException("Personagem derrotado. Precisa de reviver antes de lutar.");

        // Load opponent character
        var opponentCharacter = await _characterRepository.GetByIdAsync(opponentCharacterId);
        if (opponentCharacter == null)
            throw new EntityNotFoundException(nameof(Character), opponentCharacterId);

        if (playerCharacterId == opponentCharacterId)
            throw new InvalidOperationException("Não podes lutar contra ti mesmo");

        var playerUser = await _userManager.FindByIdAsync(playerCharacter.UserId);
        var opponentUser = await _userManager.FindByIdAsync(opponentCharacter.UserId);
        _logger.LogInformation("Battle started by {PlayerName} against {OpponentName}", 
            playerUser?.UserName ?? playerCharacter.UserId, 
            opponentUser?.UserName ?? opponentCharacter.UserId);

        // Create CPU snapshot of opponent with full HP
        // This ensures the opponent always starts at full health regardless of their persisted state
        var opponentSnapshot = Character.CreateCpuSnapshot(opponentCharacter);

        // Check if shot buff is active and create buffed copy for combat
        var hasShotBuff = playerCharacter.ShotBuffBattlesRemaining > 0;
        var combatCharacter = hasShotBuff 
            ? Character.CreateShotBuffedCopy(playerCharacter) 
            : playerCharacter;

        // Generate seed for deterministic combat
        var seed = GenerateSeed();

        // Run combat simulation using the snapshot (not the persisted character)
        var combatResult = _combatEngine.Simulate(combatCharacter, opponentSnapshot, seed);

        // Calculate rewards based on outcome
        var (xpReward, fidelisReward) = CalculateRewards(combatResult.Outcome, playerCharacter, opponentCharacter);

        // Serialize replay events to JSON
        var replayJson = JsonSerializer.Serialize(combatResult.Events, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // Calculate shot buff state after this battle
        var shotBuffExpired = hasShotBuff && playerCharacter.ShotBuffBattlesRemaining == 1;
        var shotBuffRemaining = hasShotBuff ? playerCharacter.ShotBuffBattlesRemaining - 1 : 0;

        // Create and return battle result (not persisted)
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
            ShotBuffUsed = hasShotBuff,
            ShotBuffExpired = shotBuffExpired,
            ShotBuffBattlesRemaining = shotBuffRemaining
        };
    }

    /// <summary>
    /// Finalizes a battle and applies all pending rewards and state changes
    /// Should be called after the battle animation finishes
    /// </summary>
    public async Task<bool> FinalizeAndApplyRewardsAsync(BattleResult result)
    {
        // Load the attacker character
        var playerCharacter = await _characterRepository.GetByIdAsync(result.AttackerCharacterId);
        if (playerCharacter == null)
        {
            _logger.LogError("Player character {CharacterId} not found for battle finalization", result.AttackerCharacterId);
            return false;
        }

        // Apply shot buff decrement if used
        if (result.ShotBuffUsed)
        {
            playerCharacter.ShotBuffBattlesRemaining--;
        }

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

        // Apply rewards
        await ApplyRewardsAsync(playerCharacter, result.AttackerXP, result.AttackerFidelis);

        // Apply HP changes
        playerCharacter.CurrentHP = result.AttackerFinalHP;

        // If buff just expired, scale HP down to unbuffed range
        if (result.ShotBuffExpired)
        {
            const double buffMultiplier = 1.20;
            var currentHP = playerCharacter.CurrentHP ?? playerCharacter.TotalHP;
            var unbuffedHP = (int)(currentHP / buffMultiplier);
            playerCharacter.CurrentHP = Math.Min(unbuffedHP, playerCharacter.TotalHP);
        }

        await _characterRepository.UpdateAsync(playerCharacter);

        // Roll for beer drop if player won
        if (result.Outcome == BattleOutcome.AttackerWon)
        {
            await TryDropBeerAsync(playerCharacter.UserId);
        }

        return true;
    }

    /// <summary>
    /// Calculates XP and Fidelis rewards based on battle outcome and enemy level
    /// XP and Fidelis scale based on opponent level - fighting stronger opponents gives more rewards
    /// Losses award no rewards
    /// </summary>
    private (int xp, decimal fidelis) CalculateRewards(BattleOutcome outcome, Character attacker, Character defender)
    {
        // Base rewards scaled by enemy level
        var levelMultiplier = 1.0 + (defender.Level - 1) * 0.1; // +10% per enemy level above 1
        
        return outcome switch
        {
            BattleOutcome.AttackerWon => (
                ApplyLevelScaling(BaseWinXP, attacker.Level, defender.Level), 
                (decimal)(Math.Round((double)_myTunoScalingConfig.BattleRewards.WinReward * levelMultiplier, 2))),
            BattleOutcome.DefenderWon => (0, 0m), // No rewards for losing
            BattleOutcome.Draw => (
                ApplyLevelScaling(BaseDrawXP, attacker.Level, defender.Level), 
                (decimal)(Math.Round((double)_myTunoScalingConfig.BattleRewards.DrawReward * levelMultiplier, 2))),
            _ => (0, 0m)
        };
    }

    /// <summary>
    /// Applies level-based scaling to XP rewards
    /// Formula: XP = BaseXP * (1.0 + (defenderLevel - attackerLevel) * ScalingFactor)
    /// Clamped between MinXpMultiplier and MaxXpMultiplier
    /// </summary>
    private int ApplyLevelScaling(int baseXp, int attackerLevel, int defenderLevel)
    {
        var config = _myTunoScalingConfig.BattleRewards;

        // Calculate level difference
        var levelDiff = defenderLevel - attackerLevel;

        // Calculate multiplier based on level difference
        var multiplier = 1.0 + (levelDiff * config.XpScalingFactor);

        // Clamp multiplier to prevent extreme values
        multiplier = Math.Max(config.MinXpMultiplier, Math.Min(config.MaxXpMultiplier, multiplier));

        // Apply multiplier and round to integer
        var scaledXp = (int)Math.Round(baseXp * multiplier);

        // Ensure at least 1 XP is awarded (unless baseXp is 0)
        if (baseXp > 0 && scaledXp < 1)
            scaledXp = 1;

        return scaledXp;
    }

    /// <summary>
    /// Applies XP and Fidelis rewards to the player
    /// Note: AI opponents do not receive rewards
    /// </summary>
    private async Task ApplyRewardsAsync(Character character, int xp, decimal fidelis)
    {
        // Add XP to character (handles level-ups)
        character.AddXP(xp);

        // Update character
        await _characterRepository.UpdateAsync(character);

        // Add Fidelis to user
        var user = await _userManager.FindByIdAsync(character.UserId);
        if (user != null)
        {
            user.FidelisBalance += fidelis;
            await _userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Generates a random seed for combat simulation
    /// </summary>
    private static int GenerateSeed()
    {
        return new Random().Next(int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Rolls for beer drop and adds to player's inventory if successful
    /// </summary>
    private async Task TryDropBeerAsync(string userId)
    {
        var random = new Random();
        var roll = random.NextDouble();

        if (roll < MyTunoScaling.BeerDropChance)
        {
            // Beer dropped!
            await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Beer, 1);
        }
    }
}
