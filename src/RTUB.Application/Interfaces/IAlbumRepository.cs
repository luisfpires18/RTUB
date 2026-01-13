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

    /// <summary>
    /// Gets albums visible to a specific user.
    /// Returns non-exclusive albums plus exclusive albums where the user is in the access list.
    /// Owners can see all albums regardless of exclusive status.
    /// </summary>
    /// <param name="userId">The user ID to check access for</param>
    /// <param name="isOwner">Whether the user has the Owner role (can see all exclusive albums)</param>
    Task<IEnumerable<Album>> GetAlbumsForUserAsync(string userId, bool isOwner = false);

    /// <summary>
    /// Gets the list of user IDs authorized to access an exclusive album
    /// </summary>
    /// <param name="albumId">The album ID</param>
    Task<IEnumerable<string>> GetAuthorizedUserIdsAsync(int albumId);

    /// <summary>
    /// Adds a user to the album access list
    /// </summary>
    /// <param name="albumId">The album ID</param>
    /// <param name="userId">The user ID to grant access</param>
    /// <param name="saveChanges">Whether to save changes immediately</param>
    Task AddAlbumAccessAsync(int albumId, string userId, bool saveChanges = true);

    /// <summary>
    /// Removes a user from the album access list
    /// </summary>
    /// <param name="albumId">The album ID</param>
    /// <param name="userId">The user ID to revoke access</param>
    /// <param name="saveChanges">Whether to save changes immediately</param>
    Task RemoveAlbumAccessAsync(int albumId, string userId, bool saveChanges = true);

    /// <summary>
    /// Removes all access entries for an album
    /// </summary>
    /// <param name="albumId">The album ID</param>
    /// <param name="saveChanges">Whether to save changes immediately</param>
    Task RemoveAllAlbumAccessAsync(int albumId, bool saveChanges = true);

    /// <summary>
    /// Checks if a user has access to an album
    /// </summary>
    /// <param name="albumId">The album ID</param>
    /// <param name="userId">The user ID to check</param>
    /// <returns>True if the user has access (album is not exclusive, or user is in access list)</returns>
    Task<bool> HasAccessAsync(int albumId, string userId);
}
