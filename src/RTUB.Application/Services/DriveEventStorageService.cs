using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of event storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveEventStorageService : BaseDriveStorageService, IEventStorageService
{
    protected override string PathPrefix => "images/events/";
    protected override int MaxImageWidth => 1920;
    protected override int MaxImageHeight => 1080;
    protected override int WebPQuality => 85;
    protected override string EntityName => "event";

    public DriveEventStorageService(IConfiguration configuration, ILogger<DriveEventStorageService> logger)
        : base(configuration, logger)
    {
    }

    public async Task<string> UploadImageAsync(IFormFile file, int eventId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        try
        {
            using var inputStream = file.OpenReadStream();
            var filename = GenerateFilename(eventId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading event image for event ID: {EventId}", eventId);
            throw new InvalidOperationException($"Failed to upload event image: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadImageAsync(byte[] imageData, int eventId, string contentType)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data is required", nameof(imageData));
        }

        try
        {
            using var inputStream = new MemoryStream(imageData);
            var filename = GenerateFilename(eventId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading event image for event ID: {EventId}", eventId);
            throw new InvalidOperationException($"Failed to upload event image: {ex.Message}", ex);
        }
    }

    private string GenerateFilename(int eventId, string contentType)
    {
        var extension = ".webp";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"event_{eventId}_{timestamp}{extension}";
    }
}
