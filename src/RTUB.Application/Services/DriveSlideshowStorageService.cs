using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of slideshow storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveSlideshowStorageService : ISlideshowStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<DriveSlideshowStorageService> _logger;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour
    private const string SlideshowPathPrefix = "images/slideshows/";

    public DriveSlideshowStorageService(IConfiguration configuration, ILogger<DriveSlideshowStorageService> logger)
    {
        _logger = logger;

        // Get write credentials from environment variables or configuration
        // Slideshow storage requires write access to upload/delete images
        var accessKey = configuration["IDrive:WriteAccessKey"];
        var secretKey = configuration["IDrive:WriteSecretKey"];
        var endpoint = configuration["IDrive:Endpoint"];
        var bucketName = configuration["IDrive:Bucket"];

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            var errorMsg = "iDrive e2 write credentials not configured. Set IDrive:WriteAccessKey and IDrive:WriteSecretKey.";
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

    public async Task<string> UploadImageAsync(IFormFile file, int slideshowId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        try
        {
            var filename = GenerateFilename(slideshowId, file.ContentType);
            var objectKey = GetObjectKey(filename);

            using var stream = file.OpenReadStream();
            
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = file.ContentType,
                CannedACL = S3CannedACL.PublicRead // Make publicly readable
            };

            await _s3Client.PutObjectAsync(putRequest);
            
            _logger.LogInformation("Successfully uploaded slideshow image to S3: {ObjectKey}", objectKey);
            
            return filename;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading slideshow image for slideshow ID: {SlideshowId}. ErrorCode: {ErrorCode}, Message: {Message}", 
                slideshowId, ex.ErrorCode, ex.Message);
            throw new InvalidOperationException($"Failed to upload slideshow image: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading slideshow image for slideshow ID: {SlideshowId}", slideshowId);
            throw new InvalidOperationException($"Failed to upload slideshow image: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadImageAsync(byte[] imageData, int slideshowId, string contentType)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data is required", nameof(imageData));
        }

        if (string.IsNullOrEmpty(contentType))
        {
            throw new ArgumentException("Content type is required", nameof(contentType));
        }

        try
        {
            var filename = GenerateFilename(slideshowId, contentType);
            var objectKey = GetObjectKey(filename);

            using var stream = new MemoryStream(imageData);
            
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                CannedACL = S3CannedACL.PublicRead // Make publicly readable
            };

            await _s3Client.PutObjectAsync(putRequest);
            
            _logger.LogInformation("Successfully uploaded slideshow image to S3: {ObjectKey}", objectKey);
            
            return filename;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading slideshow image for slideshow ID: {SlideshowId}. ErrorCode: {ErrorCode}, Message: {Message}", 
                slideshowId, ex.ErrorCode, ex.Message);
            throw new InvalidOperationException($"Failed to upload slideshow image: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading slideshow image for slideshow ID: {SlideshowId}", slideshowId);
            throw new InvalidOperationException($"Failed to upload slideshow image: {ex.Message}", ex);
        }
    }

    public async Task<string?> GetImageUrlAsync(string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return null;
        }

        try
        {
            var objectKey = GetObjectKey(filename);

            // Check if file exists first
            var exists = await ImageExistsAsync(filename);
            if (!exists)
            {
                _logger.LogWarning("Slideshow image not found in S3: {Filename}", filename);
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
            _logger.LogError(ex, "S3 error generating slideshow image URL for filename: {Filename}. ErrorCode: {ErrorCode}, Message: {Message}", 
                filename, ex.ErrorCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating slideshow image URL for filename: {Filename}", filename);
            return null;
        }
    }

    public async Task<bool> DeleteImageAsync(string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return false;
        }

        try
        {
            var objectKey = GetObjectKey(filename);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest);
            
            _logger.LogInformation("Successfully deleted slideshow image from S3: {ObjectKey}", objectKey);
            
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error deleting slideshow image: {Filename}. ErrorCode: {ErrorCode}, Message: {Message}", 
                filename, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting slideshow image: {Filename}", filename);
            return false;
        }
    }

    public async Task<bool> ImageExistsAsync(string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return false;
        }

        try
        {
            var objectKey = GetObjectKey(filename);

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
            _logger.LogError(ex, "S3 error checking if slideshow image exists: {Filename}. ErrorCode: {ErrorCode}, Message: {Message}", 
                filename, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking if slideshow image exists: {Filename}", filename);
            return false;
        }
    }

    private string GetObjectKey(string filename)
    {
        return $"{SlideshowPathPrefix}{filename}";
    }

    private string GenerateFilename(int slideshowId, string contentType)
    {
        // Extract file extension from content type
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".jpg" // Default to jpg
        };

        // Generate filename with timestamp to ensure uniqueness
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"slideshow_{slideshowId}_{timestamp}{extension}";
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
