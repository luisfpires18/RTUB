using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing the ranking/level system
/// Calculates XP from attendance and determines user levels
/// Refactored to use repository pattern instead of direct DbContext access
/// </summary>
public class RankingService : IRankingService
{
    private readonly IRehearsalAttendanceRepository _attendanceRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOptions<RankingConfiguration> _rankingConfig;
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RankingService(
        IRehearsalAttendanceRepository attendanceRepository,
        IEnrollmentRepository enrollmentRepository,
        UserManager<ApplicationUser> userManager,
        IOptions<RankingConfiguration> config,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _attendanceRepository = attendanceRepository;
        _enrollmentRepository = enrollmentRepository;
        _userManager = userManager;
        _rankingConfig = config;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<int> CalculateTotalXpAsync(string userId)
    {
        var nowDate = DateTime.UtcNow.Date;

        // Count rehearsal attendances where Attended == true AND rehearsal date is in the past
        var rehearsalXp = await _attendanceRepository.Query()
            .Include(ra => ra.Rehearsal)
            .Where(ra => ra.UserId == userId && ra.Attended && ra.Rehearsal!.Date < nowDate)
            .CountAsync() * _rankingConfig.Value.XpPerRehearsal;

        // Calculate event XP with type-specific values - only count events with configured XP
        var eventTypes = await _enrollmentRepository.Query()
            .Include(e => e.Event)
            .Where(e => e.UserId == userId && e.WillAttend && (e.Event!.EndDate ?? e.Event!.Date).Date < nowDate)
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
        var levels = _rankingConfig.Value.Levels;
        if (levels == null || levels.Count == 0)
            return 1;

        // Find the highest level user qualifies for without creating intermediate list
        int? highestQualifiedLevel = null;
        int highestThreshold = int.MinValue;
        int lowestLevel = int.MaxValue;

        foreach (var levelDef in levels)
        {
            // Track lowest level for fallback
            if (levelDef.Level < lowestLevel)
                lowestLevel = levelDef.Level;

            // Find highest threshold the user qualifies for
            if (xp >= levelDef.XpThreshold && levelDef.XpThreshold > highestThreshold)
            {
                highestThreshold = levelDef.XpThreshold;
                highestQualifiedLevel = levelDef.Level;
            }
        }

        return highestQualifiedLevel ?? lowestLevel;
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

        // Optimize: Query only the current first place user with Take(1) for better performance
        // This avoids loading all users into memory while maintaining test compatibility
        var currentFirstPlace = _userManager.Users
            .OrderByDescending(u => u.ExperiencePoints)
            .ThenByDescending(u => u.Level)
            .Take(1)
            .AsEnumerable()
            .FirstOrDefault();

        var totalXp = await CalculateTotalXpAsync(userId);
        var level = GetLevelFromXp(totalXp);

        var oldXp = user.ExperiencePoints;
        var oldLevel = user.Level;

        user.ExperiencePoints = totalXp;
        user.Level = level;

        await _userManager.UpdateAsync(user);

        // Check if this user has become the new 1st place
        if (currentFirstPlace != null && currentFirstPlace.Id != userId &&
            (totalXp > currentFirstPlace.ExperiencePoints ||
             (totalXp == currentFirstPlace.ExperiencePoints && level > currentFirstPlace.Level)))
        {
            // This user has surpassed the previous 1st place
            try
            {
                var baseUrl = GetBaseUrl();
                var userNickname = user.Nickname ?? user.FirstName ?? "Utilizador";
                var notification = _pushNotificationFactory.CreateLeaderboardFirstPlaceNotification(
                    userNickname,
                    level,
                    baseUrl);

                // Broadcast to all users
                await _pushNotificationService.BroadcastAsync(notification);
            }
            catch
            {
                // Log error but don't fail the ranking update
            }
        }
    }

    public async Task<RankProgressInfo> GetRankProgressAsync(string userId)
    {
        // Calculate current XP
        var currentXp = await CalculateTotalXpAsync(userId);
        return BuildRankProgressInfo(currentXp);
    }

    public async Task<Dictionary<string, RankProgressInfo>> GetRankProgressBatchAsync(IEnumerable<string> userIds)
    {
        var userIdList = userIds.ToList();
        if (!userIdList.Any())
        {
            return new Dictionary<string, RankProgressInfo>();
        }

        var nowDate = DateTime.UtcNow.Date;

        // Batch load all rehearsal attendances in a single query
        var rehearsalXpByUser = await _attendanceRepository.Query()
            .Include(ra => ra.Rehearsal)
            .Where(ra => userIdList.Contains(ra.UserId) && ra.Attended && ra.Rehearsal!.Date < nowDate)
            .GroupBy(ra => ra.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count * _rankingConfig.Value.XpPerRehearsal);

        // Batch load all enrollments with event types in a single query
        var enrollmentsByUser = await _enrollmentRepository.Query()
            .Include(e => e.Event)
            .Where(e => userIdList.Contains(e.UserId) && e.WillAttend && (e.Event!.EndDate ?? e.Event!.Date).Date < nowDate)
            .Select(e => new { e.UserId, EventType = e.Event!.Type.ToString() })
            .ToListAsync();

        // Calculate event XP for each user
        var eventXpByUser = enrollmentsByUser
            .GroupBy(e => e.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => _rankingConfig.Value.XpPerEventType.TryGetValue(e.EventType, out var xp) ? xp : 0)
            );

        // Build result dictionary
        var result = new Dictionary<string, RankProgressInfo>();
        foreach (var userId in userIdList)
        {
            var rehearsalXp = rehearsalXpByUser.GetValueOrDefault(userId, 0);
            var eventXp = eventXpByUser.GetValueOrDefault(userId, 0);
            var totalXp = rehearsalXp + eventXp;

            result[userId] = BuildRankProgressInfo(totalXp);
        }

        return result;
    }

    public async Task<Dictionary<string, RankProgressInfo>> GetRankProgressBatchAsync(IEnumerable<string> userIds, DateTime startDate, DateTime endDate)
    {
        var userIdList = userIds.ToList();
        if (!userIdList.Any())
        {
            return new Dictionary<string, RankProgressInfo>();
        }

        var startDateOnly = startDate.Date;
        var endDateOnly = endDate.Date;
        var nowDate = DateTime.UtcNow.Date;

        // Batch load all rehearsal attendances within date range in a single query
        // Also exclude future rehearsals (after today) to match "all years" behavior
        var rehearsalXpByUser = await _attendanceRepository.Query()
            .Include(ra => ra.Rehearsal)
            .Where(ra => userIdList.Contains(ra.UserId) && ra.Attended &&
                        ra.Rehearsal!.Date >= startDateOnly && ra.Rehearsal!.Date <= endDateOnly &&
                        ra.Rehearsal!.Date < nowDate)
            .GroupBy(ra => ra.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count * _rankingConfig.Value.XpPerRehearsal);

        // Batch load all enrollments with event types within date range in a single query
        // Also exclude future events (after today) to match "all years" behavior
        var enrollmentsByUser = await _enrollmentRepository.Query()
            .Include(e => e.Event)
            .Where(e => userIdList.Contains(e.UserId) && e.WillAttend &&
                       (e.Event!.EndDate ?? e.Event!.Date).Date >= startDateOnly &&
                       (e.Event!.EndDate ?? e.Event!.Date).Date <= endDateOnly &&
                       (e.Event!.EndDate ?? e.Event!.Date).Date < nowDate)
            .Select(e => new { e.UserId, EventType = e.Event!.Type.ToString() })
            .ToListAsync();

        // Calculate event XP for each user
        var eventXpByUser = enrollmentsByUser
            .GroupBy(e => e.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => _rankingConfig.Value.XpPerEventType.TryGetValue(e.EventType, out var xp) ? xp : 0)
            );

        // Build result dictionary
        var result = new Dictionary<string, RankProgressInfo>();
        foreach (var userId in userIdList)
        {
            var rehearsalXp = rehearsalXpByUser.GetValueOrDefault(userId, 0);
            var eventXp = eventXpByUser.GetValueOrDefault(userId, 0);
            var totalXp = rehearsalXp + eventXp;

            result[userId] = BuildRankProgressInfo(totalXp);
        }

        return result;
    }

    private RankProgressInfo BuildRankProgressInfo(int currentXp)
    {
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

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "https://rtub.pt"; // Fallback
    }
}
