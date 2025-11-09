using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of instrument storage service using Cloudflare R2 (S3-compatible)
/// </summary>
public class CloudflareInstrumentStorageService : BaseCloudflareStorageService, IInstrumentStorageService
{
    protected override string PathPrefix => "images/instruments/";
    protected override int MaxImageWidth => 800;
    protected override int MaxImageHeight => 800;
    protected override int WebPQuality => 85;
    protected override string EntityName => "instrument";

    public CloudflareInstrumentStorageService(IConfiguration configuration, ILogger<CloudflareInstrumentStorageService> logger)
        : base(configuration, logger)
    {
    }

    public async Task<string> UploadImageAsync(IFormFile file, int instrumentId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        try
        {
            using var inputStream = file.OpenReadStream();
            var filename = GenerateFilename(instrumentId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading instrument image for instrument ID: {InstrumentId}", instrumentId);
            throw new InvalidOperationException($"Failed to upload instrument image: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadImageAsync(byte[] imageData, int instrumentId, string contentType)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data is required", nameof(imageData));
        }

        try
        {
            using var inputStream = new MemoryStream(imageData);
            var filename = GenerateFilename(instrumentId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading instrument image for instrument ID: {InstrumentId}", instrumentId);
            throw new InvalidOperationException($"Failed to upload instrument image: {ex.Message}", ex);
        }
    }

    private string GenerateFilename(int instrumentId, string contentType)
    {
        var extension = ".webp";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"instrument_{instrumentId}_{timestamp}{extension}";
    }
}
