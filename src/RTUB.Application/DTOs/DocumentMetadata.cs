namespace RTUB.Application.DTOs;

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
