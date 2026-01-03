using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing practice session tracking with XP gamification
/// Implements practice time tracking, streak calculation, and XP rewards
/// </summary>
public class PracticeTrackingService : IPracticeTrackingService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<PracticeTrackingService> _logger;

    public PracticeTrackingService(
        ApplicationDbContext context,
        IConfiguration config,
        ILogger<PracticeTrackingService> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    public async Task<int> StartSessionAsync(string userId, InstrumentType instrument, int? songId = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting practice session for user {UserId} with instrument {Instrument}", userId, instrument);

        // Check if user already has an active session
        var activeSession = await _context.PracticeSessions
            .Where(s => s.UserId == userId && s.EndTime == null)
            .FirstOrDefaultAsync(ct);

        if (activeSession != null)
        {
            _logger.LogWarning("User {UserId} already has an active session {SessionId}", userId, activeSession.Id);
            throw new InvalidOperationException("Já existe uma sessão de prática ativa. Termina a sessão atual antes de iniciar uma nova.");
        }

        var session = new PracticeSession
        {
            UserId = userId,
            InstrumentType = instrument,
            SongId = songId,
            StartTime = DateTime.UtcNow
        };

        _context.PracticeSessions.Add(session);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Practice session {SessionId} started for user {UserId}", session.Id, userId);
        return session.Id;
    }

    public async Task EndSessionAsync(int sessionId, string? notes = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Ending practice session {SessionId}", sessionId);

        var session = await _context.PracticeSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            _logger.LogWarning("Practice session {SessionId} not found", sessionId);
            throw new InvalidOperationException("Sessão de prática não encontrada.");
        }

        if (session.EndTime.HasValue)
        {
            _logger.LogWarning("Practice session {SessionId} already ended", sessionId);
            throw new InvalidOperationException("Esta sessão de prática já foi terminada.");
        }

        session.EndTime = DateTime.UtcNow;
        session.DurationMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;
        session.Notes = notes;

        // Calculate XP: 10 XP per 15 minutes (quarter hour)
        var xpPerQuarterHour = _config.GetValue<int>("PracticeTracking:XpPerQuarterHour", 10);
        var xpEarned = (session.DurationMinutes / 15) * xpPerQuarterHour;

        // Check daily XP limit (100 XP/day per user)
        var maxDailyXp = _config.GetValue<int>("PracticeTracking:MaxDailyXp", 100);
        var today = DateTime.UtcNow.Date;
        var todaysXp = await _context.PracticeSessions
            .Where(s => s.UserId == session.UserId && s.StartTime.Date == today && s.EndTime.HasValue)
            .SumAsync(s => s.XpAwarded, ct);

        session.XpAwarded = Math.Min(xpEarned, Math.Max(0, maxDailyXp - todaysXp));

        // Update user's total XP
        if (session.User != null && session.XpAwarded > 0)
        {
            session.User.ExperiencePoints += session.XpAwarded;
            _logger.LogInformation("Awarded {XpAwarded} XP to user {UserId}. Total XP: {TotalXp}", 
                session.XpAwarded, session.UserId, session.User.ExperiencePoints);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Practice session {SessionId} ended. Duration: {Duration} minutes, XP awarded: {XpAwarded}", 
            sessionId, session.DurationMinutes, session.XpAwarded);
    }

    public async Task<PracticeSessionDto> QuickLogAsync(string userId, InstrumentType instrument, int durationMinutes, 
        int? songId = null, string? notes = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Quick logging practice session for user {UserId}. Duration: {Duration} minutes", userId, durationMinutes);

        if (durationMinutes <= 0)
        {
            throw new ArgumentException("A duração deve ser maior que zero.", nameof(durationMinutes));
        }

        var now = DateTime.UtcNow;
        var session = new PracticeSession
        {
            UserId = userId,
            InstrumentType = instrument,
            SongId = songId,
            StartTime = now.AddMinutes(-durationMinutes),
            EndTime = now,
            DurationMinutes = durationMinutes,
            Notes = notes
        };

        // Calculate XP
        var xpPerQuarterHour = _config.GetValue<int>("PracticeTracking:XpPerQuarterHour", 10);
        var xpEarned = (durationMinutes / 15) * xpPerQuarterHour;

        // Check daily XP limit
        var maxDailyXp = _config.GetValue<int>("PracticeTracking:MaxDailyXp", 100);
        var today = DateTime.UtcNow.Date;
        var todaysXp = await _context.PracticeSessions
            .Where(s => s.UserId == userId && s.StartTime.Date == today && s.EndTime.HasValue)
            .SumAsync(s => s.XpAwarded, ct);

        session.XpAwarded = Math.Min(xpEarned, Math.Max(0, maxDailyXp - todaysXp));

        _context.PracticeSessions.Add(session);

        // Update user's total XP
        var user = await _context.Users.FindAsync(new object[] { userId }, ct);
        if (user != null && session.XpAwarded > 0)
        {
            user.ExperiencePoints += session.XpAwarded;
            _logger.LogInformation("Awarded {XpAwarded} XP to user {UserId}. Total XP: {TotalXp}", 
                session.XpAwarded, userId, user.ExperiencePoints);
        }

        await _context.SaveChangesAsync(ct);

        // Load song for DTO
        var song = songId.HasValue ? await _context.Songs.FindAsync(new object[] { songId.Value }, ct) : null;

        _logger.LogInformation("Quick logged practice session {SessionId} for user {UserId}", session.Id, userId);

        return new PracticeSessionDto
        {
            Id = session.Id,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            DurationMinutes = session.DurationMinutes,
            Instrument = session.InstrumentType.ToString(),
            SongTitle = song?.Title,
            Notes = session.Notes,
            XpAwarded = session.XpAwarded
        };
    }

    public async Task<PracticeStatsDto> GetStatsAsync(string userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting practice stats for user {UserId}", userId);

        var query = _context.PracticeSessions
            .Where(s => s.UserId == userId && s.EndTime.HasValue);

        if (startDate.HasValue)
        {
            query = query.Where(s => s.StartTime >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.StartTime <= endDate.Value);
        }

        var sessions = await query.ToListAsync(ct);

        // Calculate stats
        var minutesByInstrument = sessions
            .GroupBy(s => s.InstrumentType.ToString())
            .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationMinutes));

        var totalMinutes = sessions.Sum(s => s.DurationMinutes);
        var sessionCount = sessions.Count;
        var xpEarned = sessions.Sum(s => s.XpAwarded);

        // Calculate streak
        var streak = await CalculateStreakAsync(userId, ct);

        // Get top practiced songs
        var topSongs = await GetTopSongsAsync(userId, ct);

        // Calculate weekly stats (Sunday to Saturday)
        var now = DateTime.UtcNow;
        var weekStart = now.Date.AddDays(-(int)now.DayOfWeek); // Sunday
        var weeklyMinutes = await _context.PracticeSessions
            .Where(s => s.UserId == userId && s.StartTime >= weekStart && s.EndTime.HasValue)
            .SumAsync(s => s.DurationMinutes, ct);

        var weeklyGoalMinutes = _config.GetValue<int>("PracticeTracking:WeeklyGoalMinutes", 180);

        return new PracticeStatsDto
        {
            MinutesByInstrument = minutesByInstrument,
            TotalMinutes = totalMinutes,
            SessionCount = sessionCount,
            StreakDays = streak,
            XpEarned = xpEarned,
            MostPracticedSongs = topSongs,
            WeeklyGoalMinutes = weeklyGoalMinutes,
            WeeklyMinutes = weeklyMinutes
        };
    }

    public async Task<List<PracticeSessionDto>> GetSessionsAsync(string userId, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting practice sessions for user {UserId}. Page: {PageNumber}, Size: {PageSize}", 
            userId, pageNumber, pageSize);

        if (pageNumber < 1)
        {
            pageNumber = 1;
        }

        if (pageSize < 1 || pageSize > 100)
        {
            pageSize = 20;
        }

        var sessions = await _context.PracticeSessions
            .Where(s => s.UserId == userId)
            .Include(s => s.Song)
            .OrderByDescending(s => s.StartTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new PracticeSessionDto
            {
                Id = s.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                DurationMinutes = s.DurationMinutes,
                Instrument = s.InstrumentType.ToString(),
                SongTitle = s.Song != null ? s.Song.Title : null,
                Notes = s.Notes,
                XpAwarded = s.XpAwarded
            })
            .ToListAsync(ct);

        return sessions;
    }

    public async Task<PracticeSessionDto?> GetActiveSessionAsync(string userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting active practice session for user {UserId}", userId);

        var session = await _context.PracticeSessions
            .Where(s => s.UserId == userId && s.EndTime == null)
            .Include(s => s.Song)
            .Select(s => new PracticeSessionDto
            {
                Id = s.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                DurationMinutes = s.DurationMinutes,
                Instrument = s.InstrumentType.ToString(),
                SongTitle = s.Song != null ? s.Song.Title : null,
                Notes = s.Notes,
                XpAwarded = s.XpAwarded
            })
            .FirstOrDefaultAsync(ct);

        return session;
    }

    /// <summary>
    /// Calculates the consecutive day streak for a user
    /// A streak is maintained if the user practiced today or yesterday, and continues for each consecutive day before that
    /// </summary>
    private async Task<int> CalculateStreakAsync(string userId, CancellationToken ct)
    {
        var distinctDates = await _context.PracticeSessions
            .Where(s => s.UserId == userId && s.EndTime.HasValue)
            .Select(s => s.StartTime.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(ct);

        if (!distinctDates.Any())
        {
            return 0;
        }

        var currentDate = DateTime.UtcNow.Date;
        var streak = 0;

        // Check if there's practice today or yesterday to start the streak
        if (distinctDates[0] == currentDate || distinctDates[0] == currentDate.AddDays(-1))
        {
            streak = 1;

            // Count consecutive days
            for (int i = 1; i < distinctDates.Count; i++)
            {
                if (distinctDates[i] == distinctDates[i - 1].AddDays(-1))
                {
                    streak++;
                }
                else
                {
                    break;
                }
            }
        }

        return streak;
    }

    /// <summary>
    /// Gets the top 5 most practiced songs by total practice time
    /// </summary>
    private async Task<List<TopSongDto>> GetTopSongsAsync(string userId, CancellationToken ct)
    {
        var topSongs = await _context.PracticeSessions
            .Where(s => s.UserId == userId && s.SongId.HasValue && s.EndTime.HasValue)
            .GroupBy(s => new { s.SongId, s.Song!.Title })
            .Select(g => new TopSongDto
            {
                SongId = g.Key.SongId!.Value,
                Title = g.Key.Title,
                SessionCount = g.Count(),
                TotalMinutes = g.Sum(s => s.DurationMinutes)
            })
            .OrderByDescending(s => s.TotalMinutes)
            .Take(5)
            .ToListAsync(ct);

        return topSongs;
    }
}
