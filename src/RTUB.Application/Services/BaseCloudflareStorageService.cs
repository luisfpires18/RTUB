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
/// Base class for Cloudflare R2 (S3-compatible) storage services.
/// Provides common functionality for uploading, retrieving, and deleting images.
/// </summary>
public abstract class BaseCloudflareStorageService : IDisposable
{
    protected readonly IAmazonS3 S3Client;
    protected readonly string BucketName;
    protected readonly string Environment;
    protected readonly string AccountId;
    protected readonly string PublicDomain;
    protected readonly ILogger Logger;

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

    protected BaseCloudflareStorageService(IConfiguration configuration, ILogger logger)
    {
        Logger = logger;

        // Get environment name
        Environment = configuration["ASPNETCORE_ENVIRONMENT"] 
            ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        // Get Cloudflare R2 credentials from configuration
        var accessKey = configuration["Cloudflare:R2:AccessKeyId"];
        var secretKey = configuration["Cloudflare:R2:SecretAccessKey"];
        var accountId = configuration["Cloudflare:R2:AccountId"];
        var bucketName = configuration["Cloudflare:R2:Bucket"];
        var publicDomain = configuration["Cloudflare:R2:PublicDomain"]; // e.g., "images.yourdomain.com"

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            var errorMsg = $"Cloudflare R2 credentials not configured for {EntityName} storage. Set Cloudflare:R2:AccessKeyId and Cloudflare:R2:SecretAccessKey.";
            Logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(accountId))
        {
            var errorMsg = $"Cloudflare R2 account ID not configured for {EntityName} storage. Set Cloudflare:R2:AccountId.";
            Logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(bucketName))
        {
            var errorMsg = $"Cloudflare R2 bucket name not configured for {EntityName} storage. Set Cloudflare:R2:Bucket.";
            Logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        BucketName = bucketName;
        AccountId = accountId;
        PublicDomain = publicDomain ?? string.Empty;

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true // Required for S3-compatible services
        };

        S3Client = new AmazonS3Client(credentials, config);
    }

    /// <summary>
    /// Processes an image (resize, convert to WebP) and uploads it to Cloudflare R2.
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
            // Note: Cloudflare R2 doesn't require ACLs - public access is controlled via domain settings
        };

        await S3Client.PutObjectAsync(putRequest);
        
        Logger.LogInformation("Successfully uploaded {EntityName} image to Cloudflare R2 as WebP: {ObjectKey}, Size: {Size} bytes", 
            EntityName, objectKey, fileSize);
        
        // Return public URL using custom domain if configured, otherwise use R2.dev subdomain
        var publicUrl = GetPublicUrl(objectKey);
        return publicUrl;
    }

    /// <summary>
    /// Gets the public URL for an image.
    /// If the input is already a full URL, returns it as-is.
    /// Otherwise, constructs the URL from the filename.
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
            // Check if the input is already a full URL
            if (filenameOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                filenameOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Already a full URL, return it as-is
                return Task.FromResult<string?>(filenameOrUrl);
            }
            
            // It's a filename, construct the full URL
            var objectKey = GetObjectKey(filenameOrUrl);
            var publicUrl = GetPublicUrl(objectKey);
            
            return Task.FromResult<string?>(publicUrl);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error generating {EntityName} image URL for filename: {FilenameOrUrl}", EntityName, filenameOrUrl);
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Constructs the public URL for an object key.
    /// Uses custom domain if configured, otherwise falls back to R2.dev subdomain.
    /// </summary>
    private string GetPublicUrl(string objectKey)
    {
        if (!string.IsNullOrEmpty(PublicDomain))
        {
            // Use custom domain (e.g., https://images.yourdomain.com/path/to/file.webp)
            return $"https://{PublicDomain}/{objectKey}";
        }
        else
        {
            // Use R2.dev public subdomain (bucket must have public access enabled)
            // Format: https://pub-<hash>.r2.dev/path/to/file.webp
            // Note: User needs to enable public access and provide the pub domain
            Logger.LogWarning("Cloudflare:R2:PublicDomain not configured. Images may not be accessible.");
            return $"https://pub-{BucketName}.r2.dev/{objectKey}";
        }
    }

    /// <summary>
    /// Deletes an image from Cloudflare R2 storage.
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
            
            Logger.LogInformation("Successfully deleted {EntityName} image from Cloudflare R2: {ObjectKey}", EntityName, objectKey);
            
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
    /// Checks if an image exists in Cloudflare R2 storage.
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
    /// Gets the full object key including path prefix and environment.
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
