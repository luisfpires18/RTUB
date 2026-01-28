using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing song play count tracking with cooldown
/// Extracted from Songs.razor to improve separation of concerns
/// Session-based tracking (scoped service)
/// </summary>
public class SongPlayService : ISongPlayService
{
    private readonly Dictionary<int, DateTime> _lastPlayTimes = new();
    private const int DefaultCooldownSeconds = 30;

    public bool ShouldIncrementPlayCount(int songId, DateTime? lastPlayTime, int? songDurationSeconds = null)
    {
        if (!lastPlayTime.HasValue)
            return true;

        // Use song duration (in seconds) as cooldown, or default to 30 seconds if no duration
        int cooldownSeconds = songDurationSeconds ?? DefaultCooldownSeconds;
        var timeSinceLastPlay = DateTime.UtcNow - lastPlayTime.Value;
        return timeSinceLastPlay.TotalSeconds >= cooldownSeconds;
    }

    public void RecordPlayTime(int songId, DateTime playTime)
    {
        _lastPlayTimes[songId] = playTime;
    }

    public DateTime? GetLastPlayTime(int songId)
    {
        return _lastPlayTimes.TryGetValue(songId, out var lastPlayTime) ? lastPlayTime : null;
    }
}
