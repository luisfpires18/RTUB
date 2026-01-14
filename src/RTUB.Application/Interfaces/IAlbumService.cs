using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

public interface IAlbumService
{
    Task<Album?> GetAlbumByIdAsync(int id);
    Task<IEnumerable<Album>> GetAllAlbumsAsync();
    Task<IEnumerable<Album>> GetPublicAlbumsAsync();
    /// <summary>
    /// Gets all released albums (IsDraft = false)
    /// </summary>
    Task<IEnumerable<Album>> GetReleasedAlbumsAsync();

    /// <summary>
    /// Gets all draft albums (IsDraft = true)
    /// </summary>
    Task<IEnumerable<Album>> GetDraftAlbumsAsync();
    Task<IEnumerable<Album>> GetAlbumsWithSongsAsync();
    Task<Album?> GetAlbumWithSongsAsync(int id);
    Task<Album> CreateAlbumAsync(string title, int? year, string? description = null, string? imageUrl = null, bool isPrivate = false, bool isExclusive = false, bool isDraft = false);
    Task UpdateAlbumAsync(int id, string title, int? year, string? description, bool isPrivate, bool isExclusive = false, bool isDraft = false);
    Task UpdateAlbumWithCoverAsync(int id, string title, int? year, string? description, bool isPrivate, Stream imageStream, string fileName, string contentType);
    Task SetAlbumCoverAsync(int id, Stream imageStream, string fileName, string contentType);
    Task DeleteAlbumAsync(int id);

    /// <summary>
    /// Creates an exclusive album with an initial list of authorized users
    /// </summary>
    /// <param name="title">Album title</param>
    /// <param name="year">Album year</param>
    /// <param name="description">Album description</param>
    /// <param name="imageUrl">Album cover image URL</param>
    /// <param name="authorizedUserIds">List of user IDs authorized to access the album</param>
    Task<Album> CreateExclusiveAlbumAsync(string title, int? year, string? description, string? imageUrl, List<string> authorizedUserIds);

    /// <summary>
    /// Updates the list of authorized users for an album
    /// </summary>
    /// <param name="albumId">The album ID</param>
    /// <param name="authorizedUserIds">New list of user IDs authorized to access the album</param>
    Task UpdateAlbumAccessAsync(int albumId, List<string> authorizedUserIds);

    /// <summary>
    /// Gets the list of user IDs authorized to access an album
    /// </summary>
    /// <param name="albumId">The album ID</param>
    Task<IEnumerable<string>> GetAuthorizedUserIdsAsync(int albumId);

    /// <summary>
    /// Gets albums visible to a specific user (non-exclusive + exclusive where user has access).
    /// Owners can see all albums regardless of exclusive status.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="isOwner">Whether the user has the Owner role (can see all exclusive albums)</param>
    Task<IEnumerable<Album>> GetAlbumsForUserAsync(string userId, bool isOwner = false);

    /// <summary>
    /// Checks if a user has access to an album
    /// </summary>
    /// <param name="albumId">The album ID</param>
    /// <param name="userId">The user ID</param>
    Task<bool> HasAccessAsync(int albumId, string userId);
}
