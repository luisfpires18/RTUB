using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Documentation feature (folders and documents)
/// Handles business logic for document management and visibility control
/// </summary>
public interface IDocumentationService
{
    /// <summary>
    /// Gets all folders visible to the specified user based on their roles and positions
    /// </summary>
    /// <param name="user">The application user</param>
    /// <param name="isAdmin">Whether the user is an admin</param>
    /// <param name="isMod">Whether the user is a moderator</param>
    /// <returns>Collection of visible folders</returns>
    Task<IEnumerable<Folder>> GetVisibleFoldersAsync(ApplicationUser user, bool isAdmin, bool isMod);

    /// <summary>
    /// Gets all documents in a folder if the user has access to it
    /// </summary>
    /// <param name="folderId">The folder ID</param>
    /// <param name="user">The application user</param>
    /// <param name="isAdmin">Whether the user is an admin</param>
    /// <returns>Collection of documents in the folder</returns>
    Task<IEnumerable<Document>> GetDocumentsByFolderIdAsync(int folderId, ApplicationUser user, bool isAdmin);

    /// <summary>
    /// Creates a new folder with automatic normalized key generation and collision handling
    /// </summary>
    /// <param name="displayName">The display name for the folder</param>
    /// <param name="fiscalYear">The fiscal year for the folder (e.g., "2024-2025")</param>
    /// <param name="environment">The environment name (e.g., "Production", "Development")</param>
    /// <param name="isSpecial">Whether this is a special folder with visibility restrictions</param>
    /// <param name="specialVisibility">The visibility level for special folders</param>
    /// <param name="createdByUserId">The ID of the user creating the folder</param>
    /// <param name="createdByUserName">The name of the user creating the folder</param>
    /// <returns>The created folder</returns>
    Task<Folder> CreateFolderAsync(
        string displayName,
        string fiscalYear,
        string environment,
        bool isSpecial = false,
        SpecialVisibility? specialVisibility = null,
        string? createdByUserId = null,
        string? createdByUserName = null);

    /// <summary>
    /// Creates a new document in a folder
    /// </summary>
    /// <param name="folderId">The folder ID</param>
    /// <param name="displayName">The display name for the document</param>
    /// <param name="cloudflareUrl">The Cloudflare R2 URL</param>
    /// <param name="objectKey">The S3 object key</param>
    /// <param name="sizeBytes">The file size in bytes</param>
    /// <param name="contentType">The MIME content type</param>
    /// <param name="createdByUserId">The ID of the user creating the document</param>
    /// <param name="createdByUserName">The name of the user creating the document</param>
    /// <returns>The created document</returns>
    Task<Document> CreateDocumentAsync(
        int folderId,
        string displayName,
        string cloudflareUrl,
        string objectKey,
        long sizeBytes,
        string? contentType = null,
        string? createdByUserId = null,
        string? createdByUserName = null);

    /// <summary>
    /// Deletes a folder and all its documents
    /// </summary>
    /// <param name="folderId">The folder ID to delete</param>
    Task DeleteFolderAsync(int folderId);

    /// <summary>
    /// Deletes a document
    /// </summary>
    /// <param name="documentId">The document ID to delete</param>
    Task DeleteDocumentAsync(int documentId);

    /// <summary>
    /// Gets all users who have explicit viewer permissions for a folder
    /// </summary>
    /// <param name="folderId">The folder ID</param>
    /// <returns>Collection of users with viewer permissions</returns>
    Task<IEnumerable<ApplicationUser>> GetFolderViewersAsync(int folderId);

    /// <summary>
    /// Checks if a user can access a specific folder based on visibility rules
    /// </summary>
    /// <param name="folderId">The folder ID</param>
    /// <param name="user">The application user</param>
    /// <param name="isAdmin">Whether the user is an admin</param>
    /// <returns>True if user can access the folder, false otherwise</returns>
    Task<bool> CanUserAccessFolderAsync(int folderId, ApplicationUser user, bool isAdmin);

    /// <summary>
    /// Gets an existing folder or creates a new one if it doesn't exist
    /// </summary>
    /// <param name="displayName">The display name for the folder</param>
    /// <param name="fiscalYear">The fiscal year for the folder (e.g., "2024-2025")</param>
    /// <param name="environment">The environment name (e.g., "Production", "Development")</param>
    /// <param name="isSpecial">Whether this is a special folder with visibility restrictions</param>
    /// <param name="specialVisibility">The visibility level for special folders</param>
    /// <param name="createdByUserId">The ID of the user creating the folder (if created)</param>
    /// <param name="createdByUserName">The name of the user creating the folder (if created)</param>
    /// <returns>The existing or newly created folder</returns>
    Task<Folder> GetOrCreateFolderAsync(
        string displayName,
        string fiscalYear,
        string environment,
        bool isSpecial = false,
        SpecialVisibility? specialVisibility = null,
        string? createdByUserId = null,
        string? createdByUserName = null);
}
