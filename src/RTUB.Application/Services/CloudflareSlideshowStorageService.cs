using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of slideshow storage service using Cloudflare R2 (S3-compatible)
/// </summary>
public class CloudflareSlideshowStorageService : BaseCloudflareStorageService, ISlideshowStorageService
{
    protected override string PathPrefix => "images/slideshows/";
    protected override int MaxImageWidth => 1920;
    protected override int MaxImageHeight => 1080;
    protected override int WebPQuality => 90;
    protected override string EntityName => "slideshow";

    public CloudflareSlideshowStorageService(IConfiguration configuration, ILogger<CloudflareSlideshowStorageService> logger)
        : base(configuration, logger)
    {
    }

    public async Task<string> UploadImageAsync(IFormFile file, int slideshowId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        try
        {
            using var inputStream = file.OpenReadStream();
            var filename = GenerateFilename(slideshowId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading slideshow image for slideshow ID: {SlideshowId}", slideshowId);
            throw new InvalidOperationException($"Failed to upload slideshow image: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadImageAsync(byte[] imageData, int slideshowId, string contentType)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data is required", nameof(imageData));
        }

        try
        {
            using var inputStream = new MemoryStream(imageData);
            var filename = GenerateFilename(slideshowId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading slideshow image for slideshow ID: {SlideshowId}", slideshowId);
            throw new InvalidOperationException($"Failed to upload slideshow image: {ex.Message}", ex);
        }
    }

    private string GenerateFilename(int slideshowId, string contentType)
    {
        var extension = ".webp";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"slideshow_{slideshowId}_{timestamp}{extension}";
    }
}
