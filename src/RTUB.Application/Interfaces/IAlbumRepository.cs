using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Album entity with domain-specific operations
/// </summary>
public interface IAlbumRepository : IRepository<Album>
{
    /// <summary>
    /// Gets all public albums (IsPrivate = false)
    /// </summary>
    Task<IEnumerable<Album>> GetPublicAlbumsAsync();

    /// <summary>
    /// Gets all albums with their songs included
    /// </summary>
    Task<IEnumerable<Album>> GetAlbumsWithSongsAsync();

    /// <summary>
    /// Gets an album with its songs included
    /// </summary>
    Task<Album?> GetAlbumWithSongsAsync(int id);
}
