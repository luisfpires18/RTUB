using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of product storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveProductStorageService : BaseDriveStorageService, IProductStorageService
{
    protected override string PathPrefix => "images/products/";
    protected override int MaxImageWidth => 800;
    protected override int MaxImageHeight => 800;
    protected override int WebPQuality => 85;
    protected override string EntityName => "product";

    public DriveProductStorageService(IConfiguration configuration, ILogger<DriveProductStorageService> logger)
        : base(configuration, logger)
    {
    }

    public async Task<string> UploadImageAsync(IFormFile file, int productId)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        try
        {
            using var inputStream = file.OpenReadStream();
            var filename = GenerateFilename(productId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading product image for product ID: {ProductId}", productId);
            throw new InvalidOperationException($"Failed to upload product image: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadImageAsync(byte[] imageData, int productId, string contentType)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data is required", nameof(imageData));
        }

        try
        {
            using var inputStream = new MemoryStream(imageData);
            var filename = GenerateFilename(productId, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading product image for product ID: {ProductId}", productId);
            throw new InvalidOperationException($"Failed to upload product image: {ex.Message}", ex);
        }
    }

    private string GenerateFilename(int productId, string contentType)
    {
        var extension = ".webp";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"product_{productId}_{timestamp}{extension}";
    }
}
