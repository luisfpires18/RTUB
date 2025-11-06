namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing audio file storage and retrieval
/// </summary>
public interface IAudioStorageService
{
    /// <summary>
    /// Generates a pre-signed URL for accessing an audio file
    /// </summary>
    /// <param name="songId">The song ID</param>
    /// <param name="fileName">The audio file name</param>
    /// <returns>Pre-signed URL valid for a limited time</returns>
    Task<string?> GetAudioUrlAsync(int songId, string fileName);
    
    /// <summary>
    /// Checks if an audio file exists in storage
    /// </summary>
    /// <param name="songId">The song ID</param>
    /// <param name="fileName">The audio file name</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> AudioFileExistsAsync(int songId, string fileName);
}
