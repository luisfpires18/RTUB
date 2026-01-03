namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for practice session data transfer
/// </summary>
public class PracticeSessionDto
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Instrument { get; set; } = string.Empty;
    public string? SongTitle { get; set; }
    public string? Notes { get; set; }
    public int XpAwarded { get; set; }
}

/// <summary>
/// DTO for practice statistics with gamification elements
/// </summary>
public class PracticeStatsDto
{
    public Dictionary<string, int> MinutesByInstrument { get; set; } = new();
    public int TotalMinutes { get; set; }
    public int SessionCount { get; set; }
    public int StreakDays { get; set; }
    public int XpEarned { get; set; }
    public List<TopSongDto> MostPracticedSongs { get; set; } = new();
    public int WeeklyGoalMinutes { get; set; }
    public int WeeklyMinutes { get; set; }
}

/// <summary>
/// DTO for top practiced songs
/// </summary>
public class TopSongDto
{
    public int SongId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public int TotalMinutes { get; set; }
}
