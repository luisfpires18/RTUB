using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

namespace RTUB.Application.Services;

/// <summary>
/// Base class for iDrive e2 (S3-compatible) storage services.
/// Provides common functionality for uploading, retrieving, and deleting images.
/// </summary>
public abstract class BaseDriveStorageService : IDisposable
{
    protected readonly IAmazonS3 S3Client;
    protected readonly string BucketName;
    protected readonly string Environment;
    protected readonly string Endpoint;
    protected readonly ILogger Logger;
    protected readonly int UrlExpirationHours; // Pre-signed URL expiration in hours

    /// <summary>
    /// Path prefix for this storage service (e.g., "images/profile/", "images/events/")
    /// </summary>
    protected abstract string PathPrefix { get; }

    /// <summary>
    /// Maximum image width in pixels
    /// </summary>
    protected abstract int MaxImageWidth { get; }

    /// <summary>
    /// Maximum image height in pixels
    /// </summary>
    protected abstract int MaxImageHeight { get; }

    /// <summary>
    /// WebP quality (0-100, where 100 is best quality)
    /// </summary>
    protected abstract int WebPQuality { get; }

    /// <summary>
    /// Entity name for logging purposes (e.g., "profile", "event", "slideshow")
    /// </summary>
    protected abstract string EntityName { get; }

    protected BaseDriveStorageService(IConfiguration configuration, ILogger logger)
    {
        Logger = logger;

        // Get environment name
        Environment = configuration["ASPNETCORE_ENVIRONMENT"] 
            ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        // Get write credentials from environment variables or configuration
        var accessKey = configuration["IDrive:WriteAccessKey"];
        var secretKey = configuration["IDrive:WriteSecretKey"];
        var endpoint = configuration["IDrive:Endpoint"];
        var bucketName = configuration["IDrive:Bucket"];
        
        // Get URL expiration in hours (default: 24 hours)
        UrlExpirationHours = configuration.GetValue<int>("IDrive:UrlExpirationHours", 24);

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            var errorMsg = $"iDrive e2 write credentials not configured for {EntityName} storage. Set IDrive:WriteAccessKey and IDrive:WriteSecretKey.";
            Logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(bucketName))
        {
            var errorMsg = $"iDrive e2 bucket name not configured for {EntityName} storage.";
            Logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(endpoint))
        {
            var errorMsg = $"iDrive e2 endpoint not configured for {EntityName} storage.";
            Logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        BucketName = bucketName;
        Endpoint = endpoint;

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{endpoint}",
            ForcePathStyle = true // Required for S3-compatible services
        };

        S3Client = new AmazonS3Client(credentials, config);
    }

    /// <summary>
    /// Processes an image (resize, convert to WebP) and uploads it to S3.
    /// Returns the full public URL of the uploaded image.
    /// </summary>
    protected async Task<string> ProcessAndUploadImageAsync(Stream inputStream, string filename)
    {
        using var image = await Image.LoadAsync(inputStream);
        
        // Resize if image is too large while maintaining aspect ratio
        if (image.Width > MaxImageWidth || image.Height > MaxImageHeight)
        {
            var originalWidth = image.Width;
            var originalHeight = image.Height;
            
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(MaxImageWidth, MaxImageHeight),
                Mode = ResizeMode.Max // Maintain aspect ratio
            }));
            
            Logger.LogInformation("Resized {EntityName} image from {OriginalWidth}x{OriginalHeight} to {NewWidth}x{NewHeight}", 
                EntityName, originalWidth, originalHeight, image.Width, image.Height);
        }

        var objectKey = GetObjectKey(filename);

        // Convert to WebP
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
            BucketName = BucketName,
            Key = objectKey,
            InputStream = outputStream,
            ContentType = "image/webp"
            // Note: Not using CannedACL.PublicRead - access is controlled via pre-signed URLs
        };

        await S3Client.PutObjectAsync(putRequest);
        
        Logger.LogInformation("Successfully uploaded {EntityName} image to S3 as WebP: {ObjectKey}, Size: {Size} bytes", 
            EntityName, objectKey, fileSize);
        
        // Return the filename (not a URL) - URLs are generated on-demand via GetImageUrlAsync
        return filename;
    }

    /// <summary>
    /// Gets a pre-signed URL for an image that expires after the configured time.
    /// Handles both filenames and legacy full URLs stored in the database.
    /// Returns null if filename is null or empty.
    /// </summary>
    public Task<string?> GetImageUrlAsync(string filenameOrUrl)
    {
        if (string.IsNullOrEmpty(filenameOrUrl))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            // Check if this is already a full URL (legacy data)
            if (filenameOrUrl.StartsWith("http://") || filenameOrUrl.StartsWith("https://"))
            {
                // Extract filename from URL
                // URL format: https://endpoint/bucket/path/to/file.webp
                var uri = new Uri(filenameOrUrl);
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                
                // Skip bucket name and reconstruct the object key
                if (pathParts.Length > 1)
                {
                    // The object key is everything after the bucket name
                    var legacyObjectKey = string.Join("/", pathParts.Skip(1));
                    
                    // Generate pre-signed URL
                    var legacyRequest = new GetPreSignedUrlRequest
                    {
                        BucketName = BucketName,
                        Key = legacyObjectKey,
                        Expires = DateTime.UtcNow.AddHours(UrlExpirationHours)
                    };

                    var legacyPreSignedUrl = S3Client.GetPreSignedURL(legacyRequest);
                    
                    Logger.LogDebug("Generated pre-signed URL for {EntityName} image from legacy URL: {Original}, expires in {Hours} hours", 
                        EntityName, filenameOrUrl, UrlExpirationHours);
                    
                    return Task.FromResult<string?>(legacyPreSignedUrl);
                }
                else
                {
                    Logger.LogWarning("Could not extract object key from legacy URL: {Url}", filenameOrUrl);
                    return Task.FromResult<string?>(null);
                }
            }
            
            // This is a filename, generate object key and pre-signed URL
            var objectKey = GetObjectKey(filenameOrUrl);
            
            var presignRequest = new GetPreSignedUrlRequest
            {
                BucketName = BucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddHours(UrlExpirationHours)
            };

            var presignedUrl = S3Client.GetPreSignedURL(presignRequest);
            
            Logger.LogDebug("Generated pre-signed URL for {EntityName} image: {Filename}, expires in {Hours} hours", 
                EntityName, filenameOrUrl, UrlExpirationHours);
            
            return Task.FromResult<string?>(presignedUrl);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating pre-signed URL for {EntityName} image: {FilenameOrUrl}", EntityName, filenameOrUrl);
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Deletes an image from S3 storage.
    /// </summary>
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
                BucketName = BucketName,
                Key = objectKey
            };

            await S3Client.DeleteObjectAsync(deleteRequest);
            
            Logger.LogInformation("Successfully deleted {EntityName} image from S3: {ObjectKey}", EntityName, objectKey);
            
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            Logger.LogError(ex, "S3 error deleting {EntityName} image: {Filename}. ErrorCode: {ErrorCode}, Message: {Message}", 
                EntityName, filename, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error deleting {EntityName} image: {Filename}", EntityName, filename);
            return false;
        }
    }

    /// <summary>
    /// Checks if an image exists in S3 storage.
    /// </summary>
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
                BucketName = BucketName,
                Key = objectKey
            };

            await S3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            Logger.LogError(ex, "S3 error checking if {EntityName} image exists: {Filename}. ErrorCode: {ErrorCode}, Message: {Message}", 
                EntityName, filename, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error checking if {EntityName} image exists: {Filename}", EntityName, filename);
            return false;
        }
    }

    /// <summary>
    /// Gets the full S3 object key including path prefix and environment.
    /// </summary>
    protected string GetObjectKey(string filename)
    {
        return $"{PathPrefix}{Environment}/{filename}";
    }

    public void Dispose()
    {
        S3Client?.Dispose();
    }
}
