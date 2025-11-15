using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing the ranking/level system
/// Calculates XP from attendance and determines user levels
/// </summary>
public class RankingService : IRankingService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptions<RankingConfiguration> _rankingConfig;

    public RankingService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOptions<RankingConfiguration> config)
    {
        _context = context;
        _userManager = userManager;
        _rankingConfig = config;
    }

    public async Task<int> CalculateTotalXpAsync(string userId)
    {
        var now = DateTime.UtcNow;
        
        // Count rehearsal attendances where Attended == true AND rehearsal date is in the past
        var rehearsalXp = await _context.RehearsalAttendances
            .Include(ra => ra.Rehearsal)
            .Where(ra => ra.UserId == userId && ra.Attended && ra.Rehearsal!.Date < now)
            .CountAsync() * _rankingConfig.Value.XpPerRehearsal;

        // Calculate event XP with type-specific values - only count events with configured XP
        var eventTypes = await _context.Enrollments
            .Include(e => e.Event)
            .Where(e => e.UserId == userId && e.WillAttend && e.Event!.Date < now)
            .Select(e => e.Event!.Type.ToString())
            .ToListAsync();

        var eventXp = 0;
        foreach (var eventType in eventTypes)
        {
            if (_rankingConfig.Value.XpPerEventType.TryGetValue(eventType, out var xp))
            {
                eventXp += xp;
            }
        }

        return rehearsalXp + eventXp;
    }

    public int GetLevelFromXp(int xp)
    {
        if (!_rankingConfig.Value.Levels.Any())
            return 1;

        // Sort levels by XP threshold descending to find the highest level user qualifies for
        var sortedLevels = _rankingConfig.Value.Levels.OrderByDescending(l => l.XpThreshold).ToList();
        
        foreach (var levelDef in sortedLevels)
        {
            if (xp >= levelDef.XpThreshold)
            {
                return levelDef.Level;
            }
        }

        // Return the lowest level if somehow none match
        return _rankingConfig.Value.Levels.OrderBy(l => l.Level).First().Level;
    }

    public string GetRankName(int level)
    {
        var levelDef = _rankingConfig.Value.Levels.FirstOrDefault(l => l.Level == level);
        return levelDef?.Name ?? "Desconhecido";
    }

    public int GetXpForNextLevel(int currentLevel)
    {
        var nextLevel = _rankingConfig.Value.Levels
            .Where(l => l.Level > currentLevel)
            .OrderBy(l => l.Level)
            .FirstOrDefault();

        return nextLevel?.XpThreshold ?? int.MaxValue;
    }

    public int GetXpForCurrentLevel(int currentLevel)
    {
        var currentLevelDef = _rankingConfig.Value.Levels.FirstOrDefault(l => l.Level == currentLevel);
        return currentLevelDef?.XpThreshold ?? 0;
    }

    public async Task UpdateUserRankingAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return;

        var totalXp = await CalculateTotalXpAsync(userId);
        var level = GetLevelFromXp(totalXp);

        user.ExperiencePoints = totalXp;
        user.Level = level;

        await _userManager.UpdateAsync(user);
    }

    public async Task<RankProgressInfo> GetRankProgressAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new RankProgressInfo
            {
                CurrentLevel = 1,
                CurrentRankName = "Desconhecido"
            };
        }

        // Calculate current XP
        var currentXp = await CalculateTotalXpAsync(userId);
        var currentLevel = GetLevelFromXp(currentXp);
        var currentRankName = GetRankName(currentLevel);
        var xpForCurrentLevel = GetXpForCurrentLevel(currentLevel);
        var xpForNextLevel = GetXpForNextLevel(currentLevel);

        // Check if at max level
        var isMaxLevel = xpForNextLevel == int.MaxValue;

        // Calculate progress
        var xpInCurrentLevel = currentXp - xpForCurrentLevel;
        var xpNeededForNextLevel = xpForNextLevel - xpForCurrentLevel;
        var progressPercentage = isMaxLevel ? 100.0 : (double)xpInCurrentLevel / xpNeededForNextLevel * 100.0;
        var xpToNextLevel = isMaxLevel ? 0 : xpForNextLevel - currentXp;

        return new RankProgressInfo
        {
            CurrentXp = currentXp,
            CurrentLevel = currentLevel,
            CurrentRankName = currentRankName,
            XpForCurrentLevel = xpForCurrentLevel,
            XpForNextLevel = xpForNextLevel,
            XpToNextLevel = xpToNextLevel,
            XpInCurrentLevel = xpInCurrentLevel,
            XpNeededForNextLevel = xpNeededForNextLevel,
            ProgressPercentage = Math.Min(100, Math.Max(0, progressPercentage)),
            IsMaxLevel = isMaxLevel
        };
    }
}
