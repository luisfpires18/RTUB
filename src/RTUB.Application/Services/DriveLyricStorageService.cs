using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;
using RTUB.Application.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of lyric PDF storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveLyricStorageService : BaseDriveStorageService<DriveLyricStorageService>, ILyricStorageService
{
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour

    public DriveLyricStorageService(IConfiguration configuration, ILogger<DriveLyricStorageService> logger)
        : base(configuration, logger)
    {
    }

    public async Task<string?> GetLyricPdfUrlAsync(string albumTitle, string songTitle)
    {
        var objectKey = GetObjectKey(albumTitle, songTitle);
        
        var headerOverrides = new ResponseHeaderOverrides
        {
            ContentType = "application/pdf"
        };

        return await GeneratePreSignedUrlAsync(objectKey, _urlExpirationMinutes, headerOverrides);
    }

    public async Task<bool> LyricPdfExistsAsync(string albumTitle, string songTitle)
    {
        var objectKey = GetObjectKey(albumTitle, songTitle);
        return await ObjectExistsAsync(objectKey);
    }

    private string GetObjectKey(string albumTitle, string songTitle)
    {
        // Normalize names to match bucket structure
        // Example: "Boémios e Trovadores" -> "boemios_e_trovadores"
        // Example: "Noites Presentes" -> "noites_presentes"
        
        // Ensure no leading/trailing whitespace
        albumTitle = albumTitle?.Trim() ?? string.Empty;
        songTitle = songTitle?.Trim() ?? string.Empty;
        
        // Normalize album and song names
        var normalizedAlbum = S3KeyNormalizer.NormalizeForS3Key(albumTitle);
        var normalizedSong = S3KeyNormalizer.NormalizeForS3Key(songTitle);

        // Construct the full key path: lyrics/album_folder/song_name.pdf
        var objectKey = $"lyrics/{normalizedAlbum}/{normalizedSong}.pdf";

        return objectKey;
    }
}
