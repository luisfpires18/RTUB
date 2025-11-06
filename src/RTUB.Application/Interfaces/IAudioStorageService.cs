namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing audio file storage and retrieval
/// </summary>
public interface IAudioStorageService
{
    /// <summary>
    /// Generates a pre-signed URL for accessing an audio file
    /// </summary>
    /// <param name="albumTitle">The album title (used as folder name)</param>
    /// <param name="trackNumber">The track number</param>
    /// <param name="songTitle">The song title</param>
    /// <returns>Pre-signed URL valid for a limited time</returns>
    Task<string?> GetAudioUrlAsync(string albumTitle, int? trackNumber, string songTitle);
    
    /// <summary>
    /// Checks if an audio file exists in storage
    /// </summary>
    /// <param name="albumTitle">The album title (used as folder name)</param>
    /// <param name="trackNumber">The track number</param>
    /// <param name="songTitle">The song title</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> AudioFileExistsAsync(string albumTitle, int? trackNumber, string songTitle);
}
