using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing song play count tracking with cooldown
/// Extracted from Songs.razor to improve separation of concerns
/// </summary>
public interface ISongPlayService
{
    /// <summary>
    /// Checks if a song play count should be incremented based on cooldown
    /// </summary>
    /// <param name="songId">The song ID</param>
    /// <param name="lastPlayTime">The last time this song was played (null if never played)</param>
    /// <param name="songDurationSeconds">The song duration in seconds (used as cooldown, defaults to 30)</param>
    /// <returns>True if play count should be incremented, false if still in cooldown</returns>
    bool ShouldIncrementPlayCount(int songId, DateTime? lastPlayTime, int? songDurationSeconds = null);

    /// <summary>
    /// Records a play time for a song (for cooldown tracking)
    /// </summary>
    /// <param name="songId">The song ID</param>
    /// <param name="playTime">The time the song was played</param>
    void RecordPlayTime(int songId, DateTime playTime);

    /// <summary>
    /// Gets the last play time for a song
    /// </summary>
    /// <param name="songId">The song ID</param>
    /// <returns>Last play time or null if never played</returns>
    DateTime? GetLastPlayTime(int songId);
}
