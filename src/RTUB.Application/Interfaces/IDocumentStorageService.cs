namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing general document storage and retrieval from S3
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Generates a pre-signed URL for accessing a document
    /// </summary>
    /// <param name="documentPath">The full path to the document (e.g., "docs/rtub_rgi.pdf")</param>
    /// <returns>Pre-signed URL valid for a limited time, or null if file doesn't exist</returns>
    Task<string?> GetDocumentUrlAsync(string documentPath);
    
    /// <summary>
    /// Checks if a document exists in storage
    /// </summary>
    /// <param name="documentPath">The full path to the document</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> DocumentExistsAsync(string documentPath);
}
