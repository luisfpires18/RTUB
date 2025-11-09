using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of album storage service using Cloudflare R2 (S3-compatible)
/// </summary>
public class CloudflareAlbumStorageService : BaseCloudflareStorageService, IAlbumStorageService
{
    protected override string PathPrefix => "images/albums/";
    protected override int MaxImageWidth => 800;
    protected override int MaxImageHeight => 800;
    protected override int WebPQuality => 85;
    protected override string EntityName => "album";

    public CloudflareAlbumStorageService(IConfiguration configuration, ILogger<CloudflareAlbumStorageService> logger)
        : base(configuration, logger)
    {
    }

    public async Task<string> UploadImageAsync(IFormFile file, int albumId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        try
        {
            using var inputStream = file.OpenReadStream();
            var filename = GenerateFilename(albumId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading album image for album ID: {AlbumId}", albumId);
            throw new InvalidOperationException($"Failed to upload album image: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadImageAsync(byte[] imageData, int albumId, string contentType)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data is required", nameof(imageData));
        }

        try
        {
            using var inputStream = new MemoryStream(imageData);
            var filename = GenerateFilename(albumId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading album image for album ID: {AlbumId}", albumId);
            throw new InvalidOperationException($"Failed to upload album image: {ex.Message}", ex);
        }
    }

    private string GenerateFilename(int albumId, string contentType)
    {
        var extension = ".webp";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"album_{albumId}_{timestamp}{extension}";
    }
}
