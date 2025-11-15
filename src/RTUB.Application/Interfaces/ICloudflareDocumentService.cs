using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing documents in Cloudflare R2 storage
/// Handles listing folders, uploading, downloading, and organizing documents
/// </summary>
public interface ICloudflareDocumentService
{
    /// <summary>
    /// Gets all folders within a specified root path
    /// </summary>
    /// <param name="rootPath">Root path to search for folders (e.g., "docs/")</param>
    /// <returns>List of folders found in the root path</returns>
    Task<List<CloudflareFolder>> GetFoldersAsync(string rootPath);

    /// <summary>
    /// Gets all documents within a specific folder
    /// </summary>
    /// <param name="folderPath">Path to the folder</param>
    /// <returns>List of documents in the folder</returns>
    Task<List<CloudflareDocument>> GetDocumentsInFolderAsync(string folderPath);

    /// <summary>
    /// Uploads a document to a specific folder in R2 storage
    /// </summary>
    /// <param name="fileStream">Stream containing the document data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="folderPath">Target folder path</param>
    /// <returns>Full path of the uploaded document</returns>
    Task<string> UploadDocumentAsync(Stream fileStream, string fileName, string folderPath);

    /// <summary>
    /// Gets a presigned URL for downloading or viewing a document
    /// </summary>
    /// <param name="documentPath">Full path to the document</param>
    /// <returns>Presigned URL valid for a limited time</returns>
    Task<string> GetDocumentUrlAsync(string documentPath);

    /// <summary>
    /// Creates a new folder in R2 storage
    /// </summary>
    /// <param name="folderPath">Full path of the folder to create</param>
    /// <returns>True if folder was created successfully</returns>
    Task<bool> CreateFolderAsync(string folderPath);
}
