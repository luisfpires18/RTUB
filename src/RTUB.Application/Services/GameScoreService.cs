using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service implementation for game score operations
/// Handles business logic for game scores and leaderboards
/// </summary>
public class GameScoreService : IGameScoreService
{
    private readonly IGameScoreRepository _repository;

    public GameScoreService(IGameScoreRepository repository)
    {
        _repository = repository;
    }

    public async Task<GameScore> SubmitScoreAsync(string userId, string gameKey, int points, int maxLevel, TimeSpan timeSurvived)
    {
        // Check if user already has a score for this game
        var existingScore = await _repository.GetUserScoreAsync(userId, gameKey);

        if (existingScore != null)
        {
            // Update only if the new score is better
            if (existingScore.UpdateIfBetter(points, maxLevel, timeSurvived))
            {
                try
                {
                    await _repository.UpdateAsync(existingScore);
                }
                catch (DbUpdateConcurrencyException)
                {
                    var refreshedScore = await _repository.GetUserScoreAsync(userId, gameKey);
                    if (refreshedScore != null)
                    {
                        if (refreshedScore.UpdateIfBetter(points, maxLevel, timeSurvived))
                        {
                            await _repository.UpdateAsync(refreshedScore);
                        }

                        return refreshedScore;
                    }

                    var recreatedScore = GameScore.Create(userId, gameKey, points, maxLevel, timeSurvived);
                    await _repository.AddAsync(recreatedScore);
                    return recreatedScore;
                }
            }
            return existingScore;
        }

        // Create new score if none exists
        var score = GameScore.Create(userId, gameKey, points, maxLevel, timeSurvived);

        try
        {
            await _repository.AddAsync(score);
            return score;
        }
        catch (DbUpdateException)
        {
            // Handle race condition: another request may have inserted the score concurrently
            // Re-fetch the existing score and update it if necessary
            var concurrentScore = await _repository.GetUserScoreAsync(userId, gameKey);
            if (concurrentScore != null)
            {
                if (concurrentScore.UpdateIfBetter(points, maxLevel, timeSurvived))
                {
                    await _repository.UpdateAsync(concurrentScore);
                }
                return concurrentScore;
            }

            // If still no score found, re-throw the original exception
            throw;
        }
    }

    public async Task<List<GameScoreDto>> GetLeaderboardAsync(string gameKey, int count = 10)
    {
        var scores = await _repository.GetTopScoresAsync(gameKey, count);

        return scores.Select((s, index) => new GameScoreDto
        {
            Position = index + 1,
            UserId = s.UserId,
            UserName = s.User?.UserName ?? "Unknown",
            UserNickname = s.User?.Nickname,
            ProfilePictureSrc = s.User?.ProfilePictureSrc,
            Points = s.Points,
            MaxLevel = s.MaxLevel,
            TimeSurvived = s.TimeSurvived,
            SubmittedAt = s.CreatedAt
        }).ToList();
    }

    public async Task<GameScoreDto?> GetUserBestScoreAsync(string userId, string gameKey)
    {
        var score = await _repository.GetUserBestScoreAsync(userId, gameKey);
        if (score == null) return null;

        return new GameScoreDto
        {
            UserId = score.UserId,
            UserName = score.User?.UserName ?? "Unknown",
            UserNickname = score.User?.Nickname,
            ProfilePictureSrc = score.User?.ProfilePictureSrc,
            Points = score.Points,
            MaxLevel = score.MaxLevel,
            TimeSurvived = score.TimeSurvived,
            SubmittedAt = score.CreatedAt
        };
    }

    /// <summary>
    /// Filters leaderboard scores by search term (user name or nickname)
    /// </summary>
    /// <param name="scores">Collection of scores to filter</param>
    /// <param name="searchTerm">Search term to filter by</param>
    /// <returns>Filtered list of scores</returns>
    public List<GameScoreDto> FilterLeaderboardScores(IEnumerable<GameScoreDto> scores, string searchTerm)
    {
        if (scores == null) return new List<GameScoreDto>();
        if (string.IsNullOrWhiteSpace(searchTerm)) return scores.ToList();

        var search = searchTerm.ToLower();
        return scores.Where(s =>
            (s.UserName?.ToLower().Contains(search) ?? false) ||
            (s.UserNickname?.ToLower().Contains(search) ?? false)
        ).ToList();
    }
}
