namespace RTUB.Core.Entities;

/// <summary>
/// Represents a folder in Cloudflare R2 storage containing documents
/// </summary>
public class CloudflareFolder
{
    /// <summary>
    /// Display name of the folder
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the folder in R2 storage
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Number of files in this folder
    /// </summary>
    public int FileCount { get; set; }
}
