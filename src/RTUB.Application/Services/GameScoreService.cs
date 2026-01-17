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
                await _repository.UpdateAsync(existingScore);
                await _repository.SaveChangesAsync();
            }
            return existingScore;
        }
        
        // Create new score if none exists
        var score = GameScore.Create(userId, gameKey, points, maxLevel, timeSurvived);
        
        try
        {
            await _repository.AddAsync(score);
            await _repository.SaveChangesAsync();
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
                    await _repository.SaveChangesAsync();
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
}
