using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;
using RTUB.Application.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of lyric PDF storage service using Cloudflare R2 (S3-compatible)
/// Uses a shared AmazonS3Client injected via DI
/// </summary>
public class CloudflareLyricStorageService : BaseCloudflareStorageService<CloudflareLyricStorageService>, ILyricStorageService
{
    private readonly int _urlExpirationMinutes;
    private readonly IReferenceStorageService? _referenceStorage;

    public CloudflareLyricStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareLyricStorageService> logger,
        IOptions<StorageOptions>? storageOptions = null,
        IReferenceStorageService? referenceStorage = null)
        : base(s3Client, configuration, hostEnvironment, logger)
    {
        _urlExpirationMinutes = storageOptions?.Value.UrlExpirationMinutes ?? 60;
        _referenceStorage = referenceStorage;
    }

    // lyrics/ keys carry no environment segment, so a DEV bucket does not hold the production
    // PDFs a cloned database points at. Current bucket first, read-only production reference
    // second; the reference is unconfigured in production, so this is a no-op there.
    public async Task<string?> GetLyricPdfUrlAsync(string albumTitle, string songTitle)
    {
        var objectKey = GetObjectKey(albumTitle, songTitle);

        var headerOverrides = new ResponseHeaderOverrides
        {
            ContentType = "application/pdf"
        };

        var url = await GeneratePreSignedUrlAsync(objectKey, _urlExpirationMinutes, headerOverrides);
        if (url != null || _referenceStorage == null)
        {
            return url;
        }

        return await _referenceStorage.GetPreSignedUrlAsync(
            objectKey, _urlExpirationMinutes, contentType: "application/pdf");
    }

    public async Task<bool> LyricPdfExistsAsync(string albumTitle, string songTitle)
    {
        var objectKey = GetObjectKey(albumTitle, songTitle);

        return await ObjectExistsAsync(objectKey)
            || (_referenceStorage != null && await _referenceStorage.ObjectExistsAsync(objectKey));
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
