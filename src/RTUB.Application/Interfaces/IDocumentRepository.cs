using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Document entity with domain-specific operations
/// </summary>
public interface IDocumentRepository : IRepository<Document>
{
    /// <summary>
    /// Gets a document by its object key (S3 storage key)
    /// </summary>
    /// <param name="objectKey">The object key</param>
    /// <returns>The document if found, otherwise null</returns>
    Task<Document?> GetByObjectKeyAsync(string objectKey);

    /// <summary>
    /// Gets all documents in a specific folder
    /// </summary>
    /// <param name="folderId">The folder ID</param>
    /// <returns>Collection of documents in the folder</returns>
    Task<IEnumerable<Document>> GetByFolderIdAsync(int folderId);

    /// <summary>
    /// Checks if a document with the given object key exists
    /// </summary>
    /// <param name="objectKey">The object key to check</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsAsync(string objectKey);
}
