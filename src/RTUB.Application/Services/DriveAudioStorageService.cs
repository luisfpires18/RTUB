using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of audio storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveAudioStorageService : IAudioStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<DriveAudioStorageService> _logger;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour

    public DriveAudioStorageService(IConfiguration configuration, ILogger<DriveAudioStorageService> logger)
    {
        _logger = logger;

        // Get credentials from environment variables or configuration
        var accessKey = configuration["IDrive:AccessKey"];
        var secretKey = configuration["IDrive:SecretKey"];
        var endpoint = configuration["IDrive:Endpoint"];
        var bucketName = configuration["IDrive:Bucket"];

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            var errorMsg = "iDrive e2 credentials not configured. Set environment variables.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(bucketName))
        {
            var errorMsg = "iDrive e2 bucket name not configured.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(endpoint))
        {
            var errorMsg = "iDrive e2 endpoint not configured.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _bucketName = bucketName;

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{endpoint}",
            ForcePathStyle = true // Required for S3-compatible services
        };

        _s3Client = new AmazonS3Client(credentials, config);
    }

    public async Task<string?> GetAudioUrlAsync(string albumTitle, int? trackNumber, string songTitle)
    {
        try
        {
            // Generate object key using album title as folder
            var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);

            // Check if file exists first
            var exists = await AudioFileExistsAsync(albumTitle, trackNumber, songTitle);
            if (!exists)
            {
                return null;
            }

            // Generate pre-signed URL
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddMinutes(_urlExpirationMinutes)
            };

            var url = _s3Client.GetPreSignedURL(request);
            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error generating audio URL for album: {AlbumTitle}, song: {SongTitle}. ErrorCode: {ErrorCode}, Message: {Message}", 
                albumTitle, songTitle, ex.ErrorCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating audio URL for album: {AlbumTitle}, song: {SongTitle}", albumTitle, songTitle);
            return null;
        }
    }

    public async Task<bool> AudioFileExistsAsync(string albumTitle, int? trackNumber, string songTitle)
    {
        try
        {
            var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);

            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);
            _logger.LogError(ex, "S3 error checking file existence. Bucket: '{BucketName}', Key: '{ObjectKey}', ErrorCode: {ErrorCode}, Message: {Message}", 
                _bucketName, objectKey, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);
            _logger.LogError(ex, "Unexpected error checking if audio file exists: {ObjectKey}", objectKey);
            return false;
        }
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

        // Construct the full key path: album_folder/song_name.mp3
        var objectKey = $"{normalizedAlbum}/{normalizedSong}.mp3";

        return objectKey;
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
