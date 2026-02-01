using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for filtering games by user category
/// Extracted from Games.razor to improve separation of concerns
/// </summary>
public class GameFilterService : IGameFilterService
{
    /// <summary>
    /// Filters games based on user category (Leitao users cannot see members-only games unless admin)
    /// </summary>
    /// <param name="games">Collection of games to filter</param>
    /// <param name="isLeitao">Whether the user is a Leitao</param>
    /// <param name="isAdmin">Whether the user is an admin</param>
    /// <returns>Filtered list of games</returns>
    public List<GameDto> FilterGamesByUserCategory(List<GameDto> games, bool isLeitao, bool isAdmin)
    {
        // Leitao users cannot see members-only games (unless they're admin)
        if (isLeitao && !isAdmin)
        {
            return games.Where(g => !g.MembersOnly).ToList();
        }

        // All other users (including admins) see all games
        return games;
    }
}
