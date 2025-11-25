using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;
using RTUB.Application.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of audio storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveAudioStorageService : BaseDriveStorageService<DriveAudioStorageService>, IAudioStorageService
{
    private readonly int _urlExpirationMinutes;

    public DriveAudioStorageService(
        IConfiguration configuration, 
        ILogger<DriveAudioStorageService> logger,
        IOptions<StorageOptions>? storageOptions = null)
        : base(configuration, logger)
    {
        _urlExpirationMinutes = storageOptions?.Value.UrlExpirationMinutes ?? 60;
    }

    public async Task<string?> GetAudioUrlAsync(string albumTitle, int? trackNumber, string songTitle)
    {
        var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);
        return await GeneratePreSignedUrlAsync(objectKey, _urlExpirationMinutes);
    }

    public async Task<bool> AudioFileExistsAsync(string albumTitle, int? trackNumber, string songTitle)
    {
        var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);
        return await ObjectExistsAsync(objectKey);
    }

    private string GetObjectKey(string albumTitle, int? trackNumber, string songTitle)
    {
        // Normalize names to match bucket structure
        // Example: "Boémios e Trovadores" -> "boemios_e_trovadores"
        // Example: "01. Noites Presentes" -> "noites_presentes"
        
        // Ensure no leading/trailing whitespace
        albumTitle = albumTitle?.Trim() ?? string.Empty;
        songTitle = songTitle?.Trim() ?? string.Empty;
        
        // Normalize album and song names
        var normalizedAlbum = S3KeyNormalizer.NormalizeForS3Key(albumTitle);
        var normalizedSong = S3KeyNormalizer.NormalizeForS3Key(songTitle);

        // Construct the full key path: albums/album_folder/song_name.mp3
        var objectKey = $"albums/{normalizedAlbum}/{normalizedSong}.mp3";

        return objectKey;
    }
}
