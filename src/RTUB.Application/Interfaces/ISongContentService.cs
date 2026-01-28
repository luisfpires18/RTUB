using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for processing song content (video titles, etc.)
/// Extracted from Songs.razor to improve separation of concerns
/// </summary>
public interface ISongContentService
{
    /// <summary>
    /// Generates a clean display title for a video.
    /// If the title looks like a storage key (very long, contains .mp4, etc.), 
    /// generates a generic title based on song name and video index.
    /// </summary>
    /// <param name="video">The video to get title for</param>
    /// <param name="song">The song this video belongs to</param>
    /// <param name="videoNumber">The index/number of this video</param>
    /// <returns>Clean display title for the video</returns>
    string GetCleanVideoTitle(SongVideo video, Song song, int videoNumber);
}
