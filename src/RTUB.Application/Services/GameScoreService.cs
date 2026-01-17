using Microsoft.AspNetCore.Identity;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing game scores and leaderboards
/// Uses Repository pattern following clean architecture principles
/// </summary>
public class GameScoreService : IGameScoreService
{
    private readonly IGameScoreRepository _gameScoreRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public GameScoreService(
        IGameScoreRepository gameScoreRepository,
        UserManager<ApplicationUser> userManager)
    {
        _gameScoreRepository = gameScoreRepository;
        _userManager = userManager;
    }

    /// <summary>
    /// Records a new game score for a user
    /// </summary>
    public async Task<GameScoreDto> RecordScoreAsync(string userId, string gameId, int score, int level)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new EntityNotFoundException(nameof(ApplicationUser), userId);

        var gameScore = GameScore.Create(userId, gameId, score, level);
        var createdScore = await _gameScoreRepository.AddAsync(gameScore);

        // Calculate rank by getting position in leaderboard
        var rank = await CalculateRankAsync(gameId, score, level, createdScore.PlayedAt);

        return MapToDto(createdScore, user, rank);
    }

    /// <summary>
    /// Gets the leaderboard for a specific game
    /// </summary>
    public async Task<IEnumerable<GameScoreDto>> GetLeaderboardAsync(string gameId, int limit = 10)
    {
        var scores = await _gameScoreRepository.GetTopScoresAsync(gameId, limit);

        var scoreDtos = new List<GameScoreDto>();
        var rank = 1;

        foreach (var score in scores)
        {
            scoreDtos.Add(MapToDto(score, score.User, rank));
            rank++;
        }

        return scoreDtos;
    }

    /// <summary>
    /// Gets a user's best score for a specific game
    /// </summary>
    public async Task<GameScoreDto?> GetUserBestScoreAsync(string userId, string gameId)
    {
        var bestScore = await _gameScoreRepository.GetUserBestScoreAsync(userId, gameId);

        if (bestScore == null)
            return null;

        var rank = await CalculateRankAsync(gameId, bestScore.Score, bestScore.Level, bestScore.PlayedAt);

        return MapToDto(bestScore, bestScore.User, rank);
    }

    /// <summary>
    /// Gets a user's score history for a specific game
    /// </summary>
    public async Task<IEnumerable<GameScoreDto>> GetUserHistoryAsync(string userId, string gameId, int limit = 10)
    {
        var scores = await _gameScoreRepository.GetUserScoresAsync(userId, gameId, limit);

        var scoreDtos = new List<GameScoreDto>();

        foreach (var score in scores)
        {
            var rank = await CalculateRankAsync(gameId, score.Score, score.Level, score.PlayedAt);
            scoreDtos.Add(MapToDto(score, score.User, rank));
        }

        return scoreDtos;
    }

    /// <summary>
    /// Calculates the rank of a score in the leaderboard
    /// </summary>
    private async Task<int> CalculateRankAsync(string gameId, int score, int level, DateTime playedAt)
    {
        // Count how many scores are better than this one
        // A score is better if: higher score, OR same score but higher level, OR same score and level but played earlier
        var betterScoresCount = await _gameScoreRepository.CountAsync(gs =>
            gs.GameId == gameId &&
            (gs.Score > score ||
             (gs.Score == score && gs.Level > level) ||
             (gs.Score == score && gs.Level == level && gs.PlayedAt < playedAt)));

        return betterScoresCount + 1;
    }

    /// <summary>
    /// Maps a GameScore entity to a GameScoreDto
    /// </summary>
    private static GameScoreDto MapToDto(GameScore score, ApplicationUser user, int rank)
    {
        return new GameScoreDto
        {
            Id = score.Id,
            UserId = score.UserId,
            Username = user.Nickname ?? user.UserName ?? "Unknown",
            ProfilePictureSrc = user.ProfilePictureSrc,
            GameId = score.GameId,
            Score = score.Score,
            Level = score.Level,
            PlayedAt = score.PlayedAt,
            Rank = rank
        };
    }
}
