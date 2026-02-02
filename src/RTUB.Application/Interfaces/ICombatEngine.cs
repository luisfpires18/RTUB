using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for deterministic combat engine
/// Simulates battles between characters using seeded RNG
/// </summary>
public interface ICombatEngine
{
    /// <summary>
    /// Simulates a battle between two characters
    /// </summary>
    /// <param name="attacker">The attacking character</param>
    /// <param name="defender">The defending character (AI opponent)</param>
    /// <param name="seed">RNG seed for deterministic results</param>
    /// <returns>Combat result with outcome, events, and final HP values</returns>
    CombatResult Simulate(Character attacker, Character defender, int seed);
    
    /// <summary>
    /// Simulates a battle between one player and multiple enemies
    /// Player focuses one enemy at a time until defeated (task requirement #4)
    /// </summary>
    /// <param name="player">The player character</param>
    /// <param name="enemies">List of enemy characters</param>
    /// <param name="seed">RNG seed for deterministic results</param>
    /// <returns>Combat result with outcome, events, and final HP values</returns>
    CombatResult SimulateMultiEnemy(Character player, List<Character> enemies, int seed);
}
