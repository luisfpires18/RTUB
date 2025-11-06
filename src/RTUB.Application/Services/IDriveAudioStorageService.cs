using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using System.Text;
using System.Globalization;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of audio storage service using iDrive e2 (S3-compatible)
/// </summary>
public class IDriveAudioStorageService : IAudioStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string? _bucketName;
    private readonly ILogger<IDriveAudioStorageService> _logger;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour

    public IDriveAudioStorageService(IConfiguration configuration, ILogger<IDriveAudioStorageService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Initializing IDriveAudioStorageService");
        
        // Get credentials from environment variables or configuration
        var accessKey = configuration["IDrive:AccessKey"];
        var secretKey = configuration["IDrive:SecretKey"];
        var endpoint = configuration["IDrive:Endpoint"];
        _bucketName = configuration["IDrive:Bucket"];

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            var errorMsg = "iDrive e2 credentials not configured. Set environment variables.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{endpoint}",
            ForcePathStyle = true // Required for S3-compatible services
        };

        _s3Client = new AmazonS3Client(credentials, config);
        _logger.LogInformation("IDriveAudioStorageService initialized with bucket: {BucketName}", _bucketName);
    }

    public async Task<string?> GetAudioUrlAsync(string albumTitle, int? trackNumber, string songTitle)
    {
        try
        {
            // Generate object key using album title as folder
            var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);
            _logger.LogInformation("Requesting audio URL for bucket '{BucketName}', key: '{ObjectKey}'", _bucketName, objectKey);

            // Check if file exists first
            var exists = await AudioFileExistsAsync(albumTitle, trackNumber, songTitle);
            if (!exists)
            {
                _logger.LogWarning("Cannot generate URL - audio file not found for key: {ObjectKey}", objectKey);
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
            _logger.LogInformation("Successfully generated pre-signed URL for: {ObjectKey}", objectKey);
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
            _logger.LogInformation("Checking if audio file exists in bucket '{BucketName}' with key: '{ObjectKey}'", _bucketName, objectKey);
            
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.GetObjectMetadataAsync(request);
            _logger.LogDebug("Audio file exists: {ObjectKey}", objectKey);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var objectKey = GetObjectKey(albumTitle, trackNumber, songTitle);
            _logger.LogWarning("Audio file not found (404) in bucket '{BucketName}' with key: '{ObjectKey}'", _bucketName, objectKey);
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
        var normalizedAlbum = NormalizeForS3Key(albumTitle);
        var normalizedSong = NormalizeForS3Key(songTitle);
        
        // Construct the full key path: album_folder/song_name.mp3
        var objectKey = $"{normalizedAlbum}/{normalizedSong}.mp3";
        
        _logger.LogDebug("Constructed object key: {ObjectKey} from album: {AlbumTitle}, track: {TrackNumber}, song: {SongTitle}", 
            objectKey, albumTitle, trackNumber, songTitle);
        
        return objectKey;
    }
    
    private string NormalizeForS3Key(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        
        // Remove accents/diacritics
        var normalizedString = input.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
        
        var result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        
        // Convert to lowercase
        result = result.ToLowerInvariant();
        
        // Replace spaces and special characters with underscores
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[^a-z0-9]+", "_");
        
        // Remove leading/trailing underscores
        result = result.Trim('_');
        
        return result;
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
        _logger.LogInformation("IDriveAudioStorageService disposed");
    }
}
