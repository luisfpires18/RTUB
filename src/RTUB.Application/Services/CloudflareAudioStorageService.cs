using Amazon.S3;
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
/// Implementation of audio storage service using Cloudflare R2 (S3-compatible)
/// Uses a shared AmazonS3Client injected via DI
/// </summary>
public class CloudflareAudioStorageService : BaseCloudflareStorageService<CloudflareAudioStorageService>, IAudioStorageService
{
    private readonly int _urlExpirationMinutes;
    private readonly IReferenceStorageService? _referenceStorage;

    public CloudflareAudioStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareAudioStorageService> logger,
        IOptions<StorageOptions>? storageOptions = null,
        IReferenceStorageService? referenceStorage = null)
        : base(s3Client, configuration, hostEnvironment, logger)
    {
        _urlExpirationMinutes = storageOptions?.Value.UrlExpirationMinutes ?? 60;
        _referenceStorage = referenceStorage;
    }

    // albums/ keys carry no environment segment, so a DEV bucket simply does not hold the
    // production tracks a cloned database points at. The current bucket is always tried first;
    // the read-only production reference is the fallback, and is unconfigured in production.
    public async Task<string?> GetAudioUrlAsync(string albumTitle, int? trackNumber, string songTitle)
    {
        var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);

        return await GeneratePreSignedUrlAsync(objectKey, _urlExpirationMinutes)
            ?? await ReferencePreSignAsync(objectKey);
    }

    public async Task<bool> AudioFileExistsAsync(string albumTitle, int? trackNumber, string songTitle)
    {
        var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);

        return await ObjectExistsAsync(objectKey)
            || (_referenceStorage != null && await _referenceStorage.ObjectExistsAsync(objectKey));
    }

    private Task<string?> ReferencePreSignAsync(string objectKey) =>
        _referenceStorage == null
            ? Task.FromResult<string?>(null)
            : _referenceStorage.GetPreSignedUrlAsync(objectKey, _urlExpirationMinutes);

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
