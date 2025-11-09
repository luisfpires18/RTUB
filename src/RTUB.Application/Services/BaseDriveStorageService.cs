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
            ContentType = "image/webp",
            CannedACL = S3CannedACL.PublicRead // Make publicly readable
        };

        await S3Client.PutObjectAsync(putRequest);
        
        Logger.LogInformation("Successfully uploaded {EntityName} image to S3 as WebP: {ObjectKey}, Size: {Size} bytes", 
            EntityName, objectKey, fileSize);
        
        // Return direct public URL (no expiration)
        var publicUrl = $"https://{Endpoint}/{BucketName}/{objectKey}";
        return publicUrl;
    }

    /// <summary>
    /// Gets the direct public URL for an image.
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
            
            // Return direct public URL (no expiration, no pre-signing needed)
            var publicUrl = $"https://{Endpoint}/{BucketName}/{objectKey}";
            return Task.FromResult<string?>(publicUrl);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error generating {EntityName} image URL for filename: {FilenameOrUrl}", EntityName, filenameOrUrl);
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
