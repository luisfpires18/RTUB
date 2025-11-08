using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of instrument storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveInstrumentStorageService : IInstrumentStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<DriveInstrumentStorageService> _logger;
    private readonly string _environment;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour
    private const string InstrumentPathPrefix = "images/instruments/";
    private const int WebPQuality = 85; // High quality WebP (0-100, where 100 is best quality)
    private const int MaxImageWidth = 800; // Max width for instrument images
    private const int MaxImageHeight = 800; // Max height for instrument images

    public DriveInstrumentStorageService(IConfiguration configuration, ILogger<DriveInstrumentStorageService> logger)
    {
        _logger = logger;

        // Get environment name
        _environment = configuration["ASPNETCORE_ENVIRONMENT"] 
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        // Get write credentials from environment variables or configuration
        // Instrument storage requires write access to upload/delete images
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

    public async Task<string> UploadImageAsync(IFormFile file, int instrumentId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        try
        {
            // Convert image to WebP format
            using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);
            
            // Resize if image is too large while maintaining aspect ratio
            if (image.Width > MaxImageWidth || image.Height > MaxImageHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxImageWidth, MaxImageHeight),
                    Mode = ResizeMode.Max // Maintain aspect ratio
                }));
                _logger.LogInformation("Resized instrument image from {OriginalWidth}x{OriginalHeight} to {NewWidth}x{NewHeight}", 
                    image.Width, image.Height, image.Width, image.Height);
            }

            // Generate filename with .webp extension
            var filename = GenerateFilename(instrumentId, "image/webp");
            var objectKey = GetObjectKey(filename);

            // Convert to WebP with high quality
            using var outputStream = new MemoryStream();
            var encoder = new WebpEncoder 
            { 
                Quality = WebPQuality,
                FileFormat = WebpFileFormatType.Lossy // Use lossy compression for better size reduction
            };
            await image.SaveAsync(outputStream, encoder);
            outputStream.Position = 0;

            // Capture the size before upload
            var fileSize = outputStream.Length;

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = outputStream,
                ContentType = "image/webp",
                CannedACL = S3CannedACL.PublicRead // Make publicly readable
            };

            await _s3Client.PutObjectAsync(putRequest);
            
            _logger.LogInformation("Successfully uploaded instrument image to S3 as WebP: {ObjectKey}, Size: {Size} bytes", 
                objectKey, fileSize);
            
            return filename;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading instrument image for instrument ID: {InstrumentId}. ErrorCode: {ErrorCode}, Message: {Message}", 
                instrumentId, ex.ErrorCode, ex.Message);
            throw new InvalidOperationException($"Failed to upload instrument image: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading instrument image for instrument ID: {InstrumentId}", instrumentId);
            throw new InvalidOperationException($"Failed to upload instrument image: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadImageAsync(byte[] imageData, int instrumentId, string contentType)
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
            // Convert image to WebP format
            using var inputStream = new MemoryStream(imageData);
            using var image = await Image.LoadAsync(inputStream);
            
            // Resize if image is too large while maintaining aspect ratio
            if (image.Width > MaxImageWidth || image.Height > MaxImageHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxImageWidth, MaxImageHeight),
                    Mode = ResizeMode.Max // Maintain aspect ratio
                }));
                _logger.LogInformation("Resized instrument image from {OriginalWidth}x{OriginalHeight} to {NewWidth}x{NewHeight}", 
                    image.Width, image.Height, image.Width, image.Height);
            }

            // Generate filename with .webp extension
            var filename = GenerateFilename(instrumentId, "image/webp");
            var objectKey = GetObjectKey(filename);

            // Convert to WebP with high quality
            using var outputStream = new MemoryStream();
            var encoder = new WebpEncoder 
            { 
                Quality = WebPQuality,
                FileFormat = WebpFileFormatType.Lossy // Use lossy compression for better size reduction
            };
            await image.SaveAsync(outputStream, encoder);
            outputStream.Position = 0;

            // Capture the size before upload
            var fileSize = outputStream.Length;

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = outputStream,
                ContentType = "image/webp",
                CannedACL = S3CannedACL.PublicRead // Make publicly readable
            };

            await _s3Client.PutObjectAsync(putRequest);
            
            _logger.LogInformation("Successfully uploaded instrument image to S3 as WebP: {ObjectKey}, Size: {Size} bytes", 
                objectKey, fileSize);
            
            return filename;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading instrument image for instrument ID: {InstrumentId}. ErrorCode: {ErrorCode}, Message: {Message}", 
                instrumentId, ex.ErrorCode, ex.Message);
            throw new InvalidOperationException($"Failed to upload instrument image: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading instrument image for instrument ID: {InstrumentId}", instrumentId);
            throw new InvalidOperationException($"Failed to upload instrument image: {ex.Message}", ex);
        }
    }

    public Task<string?> GetImageUrlAsync(string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var objectKey = GetObjectKey(filename);

            // Generate pre-signed URL
            // Note: Pre-signed URLs are generated without checking file existence first.
            // This avoids permission issues and the URL will return 404 if file doesn't exist.
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddMinutes(_urlExpirationMinutes)
            };

            var url = _s3Client.GetPreSignedURL(request);
            return Task.FromResult<string?>(url);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error generating instrument image URL for filename: {Filename}. ErrorCode: {ErrorCode}, Message: {Message}", 
                filename, ex.ErrorCode, ex.Message);
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating instrument image URL for filename: {Filename}", filename);
            return Task.FromResult<string?>(null);
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
            
            _logger.LogInformation("Successfully deleted instrument image from S3: {ObjectKey}", objectKey);
            
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error deleting instrument image: {Filename}. ErrorCode: {ErrorCode}, Message: {Message}", 
                filename, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting instrument image: {Filename}", filename);
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
            _logger.LogError(ex, "S3 error checking if instrument image exists: {Filename}. ErrorCode: {ErrorCode}, Message: {Message}", 
                filename, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking if instrument image exists: {Filename}", filename);
            return false;
        }
    }

    private string GetObjectKey(string filename)
    {
        return $"{InstrumentPathPrefix}{_environment}/{filename}";
    }

    private string GenerateFilename(int instrumentId, string contentType)
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
        return $"instrument_{instrumentId}_{timestamp}{extension}";
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
