using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for AI opponent matchmaking service
/// Finds suitable AI opponents (other players' characters) for battles
/// </summary>
public interface IMatchmakingService
{
    /// <summary>
    /// Finds an AI opponent for the given character
    /// </summary>
    /// <param name="playerCharacter">The player's character</param>
    /// <returns>An AI opponent character (another player's character), or null if none found</returns>
    Task<Character?> FindAIOpponentAsync(Character playerCharacter);
}
