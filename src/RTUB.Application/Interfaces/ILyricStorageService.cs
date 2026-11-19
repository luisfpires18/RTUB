namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing lyric PDF file storage and retrieval
/// </summary>
public interface ILyricStorageService
{
    /// <summary>
    /// Generates a pre-signed URL for accessing a lyric PDF file
    /// </summary>
    /// <param name="albumTitle">The album title (used as folder name)</param>
    /// <param name="songTitle">The song title</param>
    /// <returns>Pre-signed URL valid for a limited time, or null if file doesn't exist</returns>
    Task<string?> GetLyricPdfUrlAsync(string albumTitle, string songTitle);

}
