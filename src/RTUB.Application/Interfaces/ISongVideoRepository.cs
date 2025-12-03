using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for SongVideo entity
/// </summary>
public interface ISongVideoRepository : IRepository<SongVideo>
{
    /// <summary>
    /// Gets video items for a specific song
    /// </summary>
    Task<IEnumerable<SongVideo>> GetBySongIdAsync(int songId);

    /// <summary>
    /// Gets the count of videos for a specific song
    /// </summary>
    Task<int> GetCountBySongIdAsync(int songId);
}
