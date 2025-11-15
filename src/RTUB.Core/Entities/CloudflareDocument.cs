namespace RTUB.Core.Entities;

/// <summary>
/// Represents a document file stored in Cloudflare R2 storage
/// </summary>
public class CloudflareDocument
{
    /// <summary>
    /// Display name of the document (filename)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the document in R2 storage
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// File extension (e.g., "pdf", "docx")
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Date and time when the document was uploaded
    /// </summary>
    public DateTime UploadedDate { get; set; }
}
