using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for finding AI opponents for battles
/// Uses power rating to match players with similar strength opponents
/// Cooldown is tracked on Character.LastOpponentId and LastBattleAt
/// </summary>
public class MatchmakingService : IMatchmakingService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeSpan _cooldownPeriod = TimeSpan.FromHours(1); // Can't fight same opponent within 1 hour

    public MatchmakingService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Finds an AI opponent for the given character
    /// </summary>
    public async Task<Character?> FindAIOpponentAsync(Character playerCharacter)
    {
        if (playerCharacter == null)
            throw new ArgumentNullException(nameof(playerCharacter));

        // Calculate player's power rating
        var playerPowerRating = CalculatePowerRating(playerCharacter);

        // Define power range (±30% of player's rating)
        var minPowerRating = (int)(playerPowerRating * 0.7);
        var maxPowerRating = (int)(playerPowerRating * 1.3);

        // Get all characters from members only (exclude Leitao, include Caloiro, Tuno, Veterano, Tunossauro)
        // Query users who are effective members first, then get their characters
        // This ensures Categories are properly loaded and filters at database level
        var memberUserIds = await _context.Users
            .Where(u => u.Categories.Contains(MemberCategory.Caloiro) ||
                       u.Categories.Contains(MemberCategory.Tuno) ||
                       u.Categories.Contains(MemberCategory.Veterano) ||
                       u.Categories.Contains(MemberCategory.Tunossauro))
            .Select(u => u.Id)
            .ToListAsync();

        // Get characters belonging to member users, excluding player's own character
        var memberCharacters = await _context.Characters
            .Include(c => c.User)
            .Where(c => memberUserIds.Contains(c.UserId) && c.Id != playerCharacter.Id)
            .ToListAsync();

        // If no member characters found, fallback to any character (for development/testing)
        // In production, this should ideally not happen if there are member characters
        if (!memberCharacters.Any())
        {
            // Fallback: return any character if no members available
            // This allows the game to work even if there are no member characters yet
            var allCharactersFallback = await _context.Characters
                .Where(c => c.Id != playerCharacter.Id)
                .ToListAsync();

            if (allCharactersFallback.Any())
            {
                var fallbackRandom = new Random();
                var fallbackIndex = fallbackRandom.Next(0, allCharactersFallback.Count);
                return allCharactersFallback[fallbackIndex];
            }
            return null; // No characters available at all
        }

        // Filter by power rating range
        var candidates = memberCharacters
            .Where(c =>
            {
                var rating = CalculatePowerRating(c);
                return rating >= minPowerRating && rating <= maxPowerRating;
            })
            .ToList();

        if (!candidates.Any())
        {
            // If no candidates in range, expand search (±50%)
            minPowerRating = (int)(playerPowerRating * 0.5);
            maxPowerRating = (int)(playerPowerRating * 1.5);
            candidates = memberCharacters
                .Where(c =>
                {
                    var rating = CalculatePowerRating(c);
                    return rating >= minPowerRating && rating <= maxPowerRating;
                })
                .ToList();
        }

        if (!candidates.Any())
        {
            // If still no candidates, return any member character (except player's own)
            candidates = memberCharacters.ToList();
        }

        // Filter out the last opponent if still on cooldown
        // Cooldown is tracked on Character.LastOpponentId and LastBattleAt
        var isOnCooldown = playerCharacter.LastOpponentId.HasValue &&
                          playerCharacter.LastBattleAt.HasValue &&
                          playerCharacter.LastBattleAt.Value >= DateTime.UtcNow - _cooldownPeriod;

        var availableCandidates = candidates;
        if (isOnCooldown && playerCharacter.LastOpponentId.HasValue)
        {
            availableCandidates = candidates
                .Where(c => c.Id != playerCharacter.LastOpponentId.Value)
                .ToList();
        }

        // If no candidates after cooldown filter, ignore cooldown (allow repeat fights)
        if (!availableCandidates.Any())
        {
            availableCandidates = candidates;
        }

        // Select a random opponent from available candidates
        if (!availableCandidates.Any())
        {
            return null; // No opponents available
        }

        // Use a simple random selection (could be improved with weighted selection based on level difference)
        var random = new Random();
        var selectedIndex = random.Next(0, availableCandidates.Count);
        return availableCandidates[selectedIndex];
    }

    /// <summary>
    /// Calculates a power rating based on HP, Power, and Speed
    /// Formula: (HP * 0.5) + (Power * 2) + (Speed * 1.5)
    /// This gives a balanced rating where Power and Speed are weighted more than HP
    /// </summary>
    private static int CalculatePowerRating(Character character)
    {
        var hpWeight = 0.5;
        var powerWeight = 2.0;
        var speedWeight = 1.5;

        var rating = (character.TotalHP * hpWeight) +
                    (character.TotalPower * powerWeight) +
                    (character.TotalSpeed * speedWeight);

        return (int)Math.Round(rating, MidpointRounding.AwayFromZero);
    }
}
