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
}
