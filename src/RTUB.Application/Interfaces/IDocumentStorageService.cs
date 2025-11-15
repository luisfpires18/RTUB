namespace RTUB.Application.Interfaces;

/// <summary>
/// Metadata information for a document stored in S3
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// Name of the file
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Full path to the file in storage
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Size of the file in bytes
    /// </summary>
    public long SizeBytes { get; set; }
    
    /// <summary>
    /// Last modified date of the file
    /// </summary>
    public DateTime LastModified { get; set; }
    
    /// <summary>
    /// File extension (e.g., ".pdf", ".docx")
    /// </summary>
    public string Extension { get; set; } = string.Empty;
}

/// <summary>
/// Service for managing general document storage and retrieval from S3
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Generates a pre-signed URL for accessing a document
    /// </summary>
    /// <param name="documentPath">The full path to the document (e.g., "docs/rtub_rgi.pdf")</param>
    /// <param name="forceDownload">If true, sets content-disposition to attachment to force download</param>
    /// <returns>Pre-signed URL valid for a limited time, or null if file doesn't exist</returns>
    Task<string?> GetDocumentUrlAsync(string documentPath, bool forceDownload = false);
    
    /// <summary>
    /// Checks if a document exists in storage
    /// </summary>
    /// <param name="documentPath">The full path to the document</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> DocumentExistsAsync(string documentPath);
    
    /// <summary>
    /// Lists all folders under a given prefix
    /// </summary>
    /// <param name="prefix">The prefix path to search (e.g., "docs/")</param>
    /// <returns>List of folder names (not full paths)</returns>
    Task<List<string>> ListFoldersAsync(string prefix = "docs/");
    
    /// <summary>
    /// Lists all documents in a specific folder
    /// </summary>
    /// <param name="folderPath">The folder path to search (e.g., "docs/General/")</param>
    /// <returns>List of document metadata objects</returns>
    Task<List<DocumentMetadata>> ListDocumentsInFolderAsync(string folderPath);
    
    /// <summary>
    /// Uploads a document to storage
    /// </summary>
    /// <param name="folderPath">The folder path where the document should be stored</param>
    /// <param name="fileName">Name of the file</param>
    /// <param name="fileStream">Stream containing file data</param>
    /// <param name="contentType">MIME content type of the file</param>
    /// <returns>The full path of the uploaded document</returns>
    Task<string> UploadDocumentAsync(string folderPath, string fileName, Stream fileStream, string contentType);
    
    /// <summary>
    /// Creates a new folder in storage
    /// </summary>
    /// <param name="folderPath">The full path of the folder to create</param>
    Task CreateFolderAsync(string folderPath);
    
    /// <summary>
    /// Gets the size of a document in bytes
    /// </summary>
    /// <param name="documentPath">The full path to the document</param>
    /// <returns>File size in bytes, or 0 if file doesn't exist</returns>
    Task<long> GetFileSizeAsync(string documentPath);
    
    /// <summary>
    /// Deletes a document from storage
    /// </summary>
    /// <param name="documentPath">The full path to the document to delete</param>
    Task DeleteDocumentAsync(string documentPath);
    
    /// <summary>
    /// Deletes an entire folder and all its contents from storage
    /// </summary>
    /// <param name="folderPath">The full path to the folder to delete</param>
    Task DeleteFolderAsync(string folderPath);
}
