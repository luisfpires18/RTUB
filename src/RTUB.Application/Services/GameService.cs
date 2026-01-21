using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service implementation for game configuration operations
/// Handles business logic for game configurations
/// </summary>
public class GameService : IGameService
{
    private readonly IGameRepository _repository;

    public GameService(IGameRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<GameDto>> GetAllGamesAsync()
    {
        var games = await _repository.GetActiveGamesAsync();
        return games.Select(MapToDto).ToList();
    }

    public async Task<GameDto?> GetGameByKeyAsync(string key)
    {
        var game = await _repository.GetByKeyAsync(key);
        if (game == null) return null;

        return MapToDto(game);
    }

    public async Task<Game> CreateGameAsync(string key, string title, string? description, string? imageUrl, string? playRoute, bool isComingSoon, bool membersOnly)
    {
        var game = Game.Create(key, title, description, imageUrl, playRoute, isComingSoon, membersOnly);
        await _repository.AddAsync(game);
        await _repository.SaveChangesAsync();
        return game;
    }

    public async Task<Game?> UpdateGameAsync(int id, string title, string? description, bool isComingSoon, bool membersOnly)
    {
        var game = await _repository.GetByIdAsync(id);
        if (game == null) return null;

        game.Update(title, description, isComingSoon, membersOnly);
        await _repository.SaveChangesAsync();
        return game;
    }

    public async Task SeedDefaultGamesAsync()
    {
        var existingGame = await _repository.GetByKeyAsync("avoid-questions");
        if (existingGame == null)
        {
            await CreateGameAsync(
                "avoid-questions",
                "Evitar Perguntas",
                "Joga como um Magister e evita as perguntas dos membros que caem do céu. Sobrevive o máximo tempo possível!",
                "/sprites/games/avoid-questions-thumb.svg",
                "/games/avoid-questions",
                false,
                false
            );
        }

        var bmrGame = await _repository.GetByKeyAsync("bmr-bebe-mais-rui");
        if (bmrGame == null)
        {
            await CreateGameAsync(
                "bmr-bebe-mais-rui",
                "BMR — Bebe mais Rui",
                "Run, jump, collect beers, dodge heavy hitters.",
                "/sprites/bmr/thumbnail.svg",
                "/games/bmr-bebe-mais-rui",
                false,
                false
            );
        }

        var tomatoGame = await _repository.GetByKeyAsync("tomato-thrower");
        if (tomatoGame == null)
        {
            await CreateGameAsync(
                "tomato-thrower",
                "Atira Tomates",
                "Atira tomates aos caloteiros! Um jogo estilo whack-a-mole onde os membros com dívidas aparecem e tu tens de os acertar.",
                "/sprites/games/tomato-thrower-thumb.svg",
                "/games/tomato-thrower",
                false,
                true // Members only
            );
        }
    }

    /// <summary>
    /// Maps a Game entity to GameDto
    /// Centralizes mapping logic to avoid duplication
    /// </summary>
    private static GameDto MapToDto(Game game)
    {
        return new GameDto
        {
            Id = game.Id,
            Key = game.Key,
            Title = game.Title,
            Description = game.Description,
            ImageUrl = game.ImageUrl,
            PlayRoute = game.PlayRoute,
            IsComingSoon = game.IsComingSoon,
            MembersOnly = game.MembersOnly,
            IsActive = game.IsActive
        };
    }
}
