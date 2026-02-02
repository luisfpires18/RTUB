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
/// </summary>
public class BattleService : IBattleService
{
    private readonly ICharacterRepository _characterRepository;
    private readonly IBattleRepository _battleRepository;
    private readonly IMatchmakingService _matchmakingService;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BattleService> _logger;
    private readonly MyTunoScalingConfiguration _myTunoScalingConfig;

    // Reward constants
    private const int BaseWinXP = 50;
    private const int BaseLossXP = 20;
    private const int BaseDrawXP = 30;

    public BattleService(
        ICharacterRepository characterRepository,
        IBattleRepository battleRepository,
        IMatchmakingService matchmakingService,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<BattleService> logger,
        IOptions<MyTunoScalingConfiguration> myTunoScalingConfig)
    {
        _characterRepository = characterRepository;
        _battleRepository = battleRepository;
        _matchmakingService = matchmakingService;
        _combatEngine = combatEngine;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _myTunoScalingConfig = myTunoScalingConfig.Value;
    }

    /// <summary>
    /// Creates and executes a battle vs AI opponent
    /// </summary>
    public async Task<Battle> CreateBattleVsAIAsync(int playerCharacterId)
    {
        // Load player character
        var playerCharacter = await _characterRepository.GetByIdAsync(playerCharacterId);
        if (playerCharacter == null)
            throw new EntityNotFoundException(nameof(Character), playerCharacterId);

        // Find AI opponent
        var aiOpponent = await _matchmakingService.FindAIOpponentAsync(playerCharacter);
        if (aiOpponent == null)
        {
            _logger.LogWarning("No AI opponent found for character {CharacterId}. Player may need to wait for more member characters to be created.", playerCharacterId);
            throw new InvalidOperationException("Nenhum oponente AI disponível. Aguarda até que mais membros criem personagens.");
        }

        // Generate seed for deterministic combat
        var seed = GenerateSeed();

        // Run combat simulation
        var combatResult = _combatEngine.Simulate(playerCharacter, aiOpponent, seed);

        // Calculate rewards based on outcome
        var (xpReward, fidelisReward) = CalculateRewards(combatResult.Outcome, playerCharacter, aiOpponent);

        // Create battle record
        var battle = Battle.Create(playerCharacterId, aiOpponent.Id, seed, combatResult.Outcome);
        battle.SetRewards(xpReward, fidelisReward);

        // Serialize replay events to JSON
        var replayJson = JsonSerializer.Serialize(combatResult.Events, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        battle.SetReplay(replayJson);

        // Persist battle
        await _battleRepository.AddAsync(battle);

        // Apply rewards to player character and user
        await ApplyRewardsAsync(playerCharacter, xpReward, fidelisReward);

        // Roll for beer drop if player won
        if (combatResult.Outcome == BattleOutcome.AttackerWon)
        {
            await TryDropBeerAsync(playerCharacter.UserId);
        }

        _logger.LogInformation(
            "Battle created: Player {PlayerCharacterId} vs AI {AIOpponentId}, Outcome: {Outcome}, XP: {XP}, Fidelis: {Fidelis}",
            playerCharacterId, aiOpponent.Id, combatResult.Outcome, xpReward, fidelisReward);

        return battle;
    }

    /// <summary>
    /// Creates and executes a battle vs a specific opponent character
    /// </summary>
    public async Task<Battle> CreateBattleVsOpponentAsync(int playerCharacterId, int opponentCharacterId)
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

        // Create CPU snapshot of opponent with full HP
        // This ensures the opponent always starts at full health regardless of their persisted state
        var opponentSnapshot = Character.CreateCpuSnapshot(opponentCharacter);

        // Generate seed for deterministic combat
        var seed = GenerateSeed();

        // Run combat simulation using the snapshot (not the persisted character)
        var combatResult = _combatEngine.Simulate(playerCharacter, opponentSnapshot, seed);

        // Calculate rewards based on outcome
        var (xpReward, fidelisReward) = CalculateRewards(combatResult.Outcome, playerCharacter, opponentCharacter);

        // Create battle record
        var battle = Battle.Create(playerCharacterId, opponentCharacterId, seed, combatResult.Outcome);
        battle.SetRewards(xpReward, fidelisReward);

        // Serialize replay events to JSON
        var replayJson = JsonSerializer.Serialize(combatResult.Events, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        battle.SetReplay(replayJson);

        // Persist battle
        await _battleRepository.AddAsync(battle);

        // Apply rewards to player character and user
        await ApplyRewardsAsync(playerCharacter, xpReward, fidelisReward);

        // Update HP based on battle outcome
        await ApplyAttackerHPChangesAsync(playerCharacter, combatResult);

        // Roll for beer drop if player won
        if (combatResult.Outcome == BattleOutcome.AttackerWon)
        {
            await TryDropBeerAsync(playerCharacter.UserId);
        }

        _logger.LogInformation(
            "Battle created: Player {PlayerCharacterId} vs Opponent {OpponentCharacterId}, Outcome: {Outcome}, XP: {XP}, Fidelis: {Fidelis}",
            playerCharacterId, opponentCharacterId, combatResult.Outcome, xpReward, fidelisReward);

        return battle;
    }

    /// <summary>
    /// Calculates XP and Fidelis rewards based on battle outcome and level difference
    /// XP scales based on opponent level - fighting stronger opponents gives more XP
    /// Losses award no rewards
    /// </summary>
    private (int xp, decimal fidelis) CalculateRewards(BattleOutcome outcome, Character attacker, Character defender)
    {
        // Get base rewards based on outcome
        return outcome switch
        {
            BattleOutcome.AttackerWon => (ApplyLevelScaling(BaseWinXP, attacker.Level, defender.Level), _myTunoScalingConfig.BattleRewards.WinReward),
            BattleOutcome.DefenderWon => (0, 0m), // No rewards for losing
            BattleOutcome.Draw => (ApplyLevelScaling(BaseDrawXP, attacker.Level, defender.Level), _myTunoScalingConfig.BattleRewards.DrawReward),
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
    /// Updates attacker HP based on battle outcome
    /// Winners keep their remaining HP, losers go to 0 HP
    /// Note: Currently only updates attacker HP as defenders are AI opponents.
    /// For PvP implementation, defender HP should also be updated.
    /// </summary>
    private async Task ApplyAttackerHPChangesAsync(Character attacker, CombatResult combatResult)
    {
        // Update attacker HP based on combat result
        attacker.CurrentHP = combatResult.AttackerFinalHP;
        await _characterRepository.UpdateAsync(attacker);

        _logger.LogInformation(
            "HP updated after battle: Attacker HP = {AttackerHP}, Outcome = {Outcome}",
            attacker.CurrentHP, combatResult.Outcome);
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

            _logger.LogInformation(
                "Beer dropped for user {UserId}! Roll: {Roll:F3}, Drop chance: {DropChance:F3}",
                userId, roll, MyTunoScaling.BeerDropChance);
        }
        else
        {
            _logger.LogDebug(
                "No beer drop for user {UserId}. Roll: {Roll:F3}, Drop chance: {DropChance:F3}",
                userId, roll, MyTunoScaling.BeerDropChance);
        }
    }
}
