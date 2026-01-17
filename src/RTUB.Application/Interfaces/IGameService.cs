using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for game configuration operations
/// </summary>
public interface IGameService
{
    Task<List<GameDto>> GetAllGamesAsync();
    Task<GameDto?> GetGameByKeyAsync(string key);
    Task<Game> CreateGameAsync(string key, string title, string? description, string? imageUrl, string? playRoute, bool isComingSoon, bool membersOnly);
    Task<Game?> UpdateGameAsync(int id, string title, string? description, bool isComingSoon, bool membersOnly);
    Task SeedDefaultGamesAsync();
}
