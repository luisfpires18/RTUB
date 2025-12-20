using RTUB.Core.Entities;

namespace RTUB.Web.Services;

/// <summary>
/// Service for managing media playback queue in PWA mode
/// Handles queue building, navigation (Next/Previous), and track state
/// </summary>
public class MediaQueueService
{
    /// <summary>
    /// Represents a track in the playback queue
    /// </summary>
    public class QueueTrack
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string AudioUrl { get; set; } = string.Empty;
        public string? ArtworkUrl { get; set; }
        public int TrackNumber { get; set; }
    }

    /// <summary>
    /// Build a queue from a list of songs, ordered by track number
    /// </summary>
    /// <param name="songs">List of songs in the album</param>
    /// <param name="albumTitle">Album title for metadata</param>
    /// <returns>Ordered list of queue tracks</returns>
    public List<QueueTrack> BuildQueue(List<Song> songs, string albumTitle)
    {
        return songs
            .OrderBy(s => s.TrackNumber ?? int.MaxValue)
            .Select(s => new QueueTrack
            {
                Id = s.Id,
                Title = s.Title,
                Artist = string.Empty, // RTUB doesn't store artist per song
                Album = albumTitle,
                TrackNumber = s.TrackNumber ?? 0
            })
            .ToList();
    }

    /// <summary>
    /// Get the next track in the queue
    /// </summary>
    /// <param name="queue">Current queue</param>
    /// <param name="currentIndex">Current track index</param>
    /// <returns>Next track or null if at end of queue</returns>
    public QueueTrack? GetNextTrack(List<QueueTrack> queue, int currentIndex)
    {
        if (currentIndex < queue.Count - 1)
        {
            return queue[currentIndex + 1];
        }
        return null;
    }

    /// <summary>
    /// Get the previous track or determine if current track should restart
    /// </summary>
    /// <param name="queue">Current queue</param>
    /// <param name="currentIndex">Current track index</param>
    /// <param name="currentTime">Current playback time in seconds</param>
    /// <param name="restartThreshold">Time threshold for restarting track (default: 3s)</param>
    /// <returns>Previous track, current track (for restart), or null if at start</returns>
    public (QueueTrack? track, bool shouldRestart) GetPreviousTrackOrRestart(
        List<QueueTrack> queue,
        int currentIndex,
        double currentTime,
        double restartThreshold = 3.0)
    {
        // If more than threshold seconds into track, restart it
        if (currentTime > restartThreshold)
        {
            return (queue[currentIndex], true);
        }

        // Go to previous track if available
        if (currentIndex > 0)
        {
            return (queue[currentIndex - 1], false);
        }

        // At start of queue and under threshold - restart current track
        return (queue[currentIndex], true);
    }

    /// <summary>
    /// Find the index of a song in the queue
    /// </summary>
    /// <param name="queue">Current queue</param>
    /// <param name="songId">Song ID to find</param>
    /// <returns>Index of the song or -1 if not found</returns>
    public int FindSongIndex(List<QueueTrack> queue, int songId)
    {
        return queue.FindIndex(t => t.Id == songId);
    }
}
