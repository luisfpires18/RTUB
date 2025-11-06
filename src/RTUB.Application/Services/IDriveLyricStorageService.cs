using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of lyric PDF storage service using iDrive e2 (S3-compatible)
/// </summary>
public class IDriveLyricStorageService : ILyricStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<IDriveLyricStorageService> _logger;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour

    public IDriveLyricStorageService(IConfiguration configuration, ILogger<IDriveLyricStorageService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Initializing IDriveLyricStorageService");
        
        // Get credentials from environment variables or configuration
        var accessKey = Environment.GetEnvironmentVariable("IDRIVE_ACCESS_KEY") 
                        ?? configuration["IDrive:AccessKey"];
        var secretKey = Environment.GetEnvironmentVariable("IDRIVE_SECRET_KEY") 
                        ?? configuration["IDrive:SecretKey"];
        var endpoint = Environment.GetEnvironmentVariable("IDRIVE_ENDPOINT") 
                       ?? configuration["IDrive:Endpoint"] 
                       ?? "s3.eu-west-4.idrivee2.com";
        _bucketName = Environment.GetEnvironmentVariable("IDRIVE_BUCKET") 
                      ?? configuration["IDrive:Bucket"] 
                      ?? "rtub";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            var errorMsg = "iDrive e2 credentials not configured. Set IDRIVE_ACCESS_KEY and IDRIVE_SECRET_KEY environment variables.";
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
        _logger.LogInformation("IDriveLyricStorageService initialized with bucket: {BucketName}", _bucketName);
    }

    public async Task<string?> GetLyricPdfUrlAsync(string albumTitle, string songTitle)
    {
        try
        {
            // Generate object key using album title as folder
            var objectKey = GetObjectKey(albumTitle, songTitle);
            _logger.LogInformation("Requesting lyric PDF URL for bucket '{BucketName}', key: '{ObjectKey}'", _bucketName, objectKey);

            // Check if file exists first
            var exists = await LyricPdfExistsAsync(albumTitle, songTitle);
            if (!exists)
            {
                _logger.LogWarning("Cannot generate URL - lyric PDF file not found for key: {ObjectKey}", objectKey);
                return null;
            }

            // Generate pre-signed URL with content type for PDF
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddMinutes(_urlExpirationMinutes),
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentType = "application/pdf"
                }
            };

            var url = _s3Client.GetPreSignedURL(request);
            _logger.LogInformation("Successfully generated pre-signed URL for lyric PDF: {ObjectKey}", objectKey);
            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error generating lyric PDF URL for album: {AlbumTitle}, song: {SongTitle}. ErrorCode: {ErrorCode}, Message: {Message}", 
                albumTitle, songTitle, ex.ErrorCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating lyric PDF URL for album: {AlbumTitle}, song: {SongTitle}", albumTitle, songTitle);
            return null;
        }
    }

    public async Task<bool> LyricPdfExistsAsync(string albumTitle, string songTitle)
    {
        try
        {
            var objectKey = GetObjectKey(albumTitle, songTitle);
            _logger.LogInformation("Checking if lyric PDF file exists in bucket '{BucketName}' with key: '{ObjectKey}'", _bucketName, objectKey);
            
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.GetObjectMetadataAsync(request);
            _logger.LogDebug("Lyric PDF file exists: {ObjectKey}", objectKey);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var objectKey = GetObjectKey(albumTitle, songTitle);
            _logger.LogWarning("Lyric PDF file not found (404) in bucket '{BucketName}' with key: '{ObjectKey}'", _bucketName, objectKey);
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            var objectKey = GetObjectKey(albumTitle, songTitle);
            _logger.LogError(ex, "S3 error checking lyric PDF file existence. Bucket: '{BucketName}', Key: '{ObjectKey}', ErrorCode: {ErrorCode}, Message: {Message}", 
                _bucketName, objectKey, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            var objectKey = GetObjectKey(albumTitle, songTitle);
            _logger.LogError(ex, "Unexpected error checking if lyric PDF file exists: {ObjectKey}", objectKey);
            return false;
        }
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
        
        _logger.LogDebug("Constructed object key: {ObjectKey} from album: {AlbumTitle}, song: {SongTitle}", 
            objectKey, albumTitle, songTitle);
        
        return objectKey;
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
        _logger.LogInformation("IDriveLyricStorageService disposed");
    }
}
