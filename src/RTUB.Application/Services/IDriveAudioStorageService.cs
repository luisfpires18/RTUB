using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of audio storage service using iDrive e2 (S3-compatible)
/// </summary>
public class IDriveAudioStorageService : IAudioStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour

    public IDriveAudioStorageService(IConfiguration configuration)
    {
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
            throw new InvalidOperationException(
                "iDrive e2 credentials not configured. Set IDRIVE_ACCESS_KEY and IDRIVE_SECRET_KEY environment variables.");
        }

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{endpoint}",
            ForcePathStyle = true // Required for S3-compatible services
        };

        _s3Client = new AmazonS3Client(credentials, config);
    }

    public async Task<string?> GetAudioUrlAsync(int songId, string fileName)
    {
        try
        {
            // Generate object key - use song ID and sanitized file name
            var objectKey = GetObjectKey(songId, fileName);

            // Check if file exists first
            var exists = await AudioFileExistsAsync(songId, fileName);
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
        catch (Exception ex)
        {
            // Log error (consider using ILogger here)
            Console.WriteLine($"Error generating audio URL: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> AudioFileExistsAsync(int songId, string fileName)
    {
        try
        {
            var objectKey = GetObjectKey(songId, fileName);
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
        catch
        {
            return false;
        }
    }

    private string GetObjectKey(int songId, string fileName)
    {
        // Clean the filename and create object key
        // Format: songs/{songId}/{cleanFileName}
        var cleanFileName = SanitizeFileName(fileName);
        return $"songs/{songId}/{cleanFileName}";
    }

    private string SanitizeFileName(string fileName)
    {
        // Remove or replace invalid characters
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        
        // Ensure extension is present (default to .mp3 if not)
        if (!Path.HasExtension(sanitized))
        {
            sanitized += ".mp3";
        }
        
        return sanitized;
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
