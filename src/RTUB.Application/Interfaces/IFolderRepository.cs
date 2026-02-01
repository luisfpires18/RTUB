using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Folder entity with domain-specific operations
/// </summary>
public interface IFolderRepository : IRepository<Folder>
{
    /// <summary>
    /// Gets a folder by its normalized key (S3 storage key)
    /// </summary>
    /// <param name="normalizedKey">The normalized key</param>
    /// <returns>The folder if found, otherwise null</returns>
    Task<Folder?> GetByNormalizedKeyAsync(string normalizedKey);

    /// <summary>
    /// Gets folders visible to a specific user based on their role and positions
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="isAdmin">Whether the user is an admin</param>
    /// <returns>Collection of visible folders</returns>
    Task<IEnumerable<Folder>> GetVisibleFoldersAsync(string userId, bool isAdmin);

    /// <summary>
    /// Checks if a folder with the given normalized key exists
    /// </summary>
    /// <param name="normalizedKey">The normalized key to check</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsAsync(string normalizedKey);
}
