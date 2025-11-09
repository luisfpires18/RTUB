using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of profile storage service using Cloudflare R2 (S3-compatible)
/// </summary>
public class CloudflareProfileStorageService : BaseCloudflareStorageService, IProfileStorageService
{
    protected override string PathPrefix => "images/profile/";
    protected override int MaxImageWidth => 800;
    protected override int MaxImageHeight => 800;
    protected override int WebPQuality => 85;
    protected override string EntityName => "profile";

    public CloudflareProfileStorageService(IConfiguration configuration, ILogger<CloudflareProfileStorageService> logger)
        : base(configuration, logger)
    {
    }

    public async Task<string> UploadImageAsync(IFormFile file, string username)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is required", nameof(file));
        }

        if (string.IsNullOrEmpty(username))
        {
            throw new ArgumentException("Username is required", nameof(username));
        }

        try
        {
            using var inputStream = file.OpenReadStream();
            var filename = GenerateFilename(username, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading profile image for username: {Username}", username);
            throw new InvalidOperationException($"Failed to upload profile image: {ex.Message}", ex);
        }
    }

    public async Task<string> UploadImageAsync(byte[] imageData, string username, string contentType)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentException("Image data is required", nameof(imageData));
        }

        if (string.IsNullOrEmpty(username))
        {
            throw new ArgumentException("Username is required", nameof(username));
        }

        try
        {
            using var inputStream = new MemoryStream(imageData);
            var filename = GenerateFilename(username, "image/webp");
            return await ProcessAndUploadImageAsync(inputStream, filename);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading profile image for username: {Username}", username);
            throw new InvalidOperationException($"Failed to upload profile image: {ex.Message}", ex);
        }
    }

    private string GenerateFilename(string username, string contentType)
    {
        // Sanitize username for use in filename
        var sanitizedUsername = new string(username.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        if (string.IsNullOrEmpty(sanitizedUsername))
        {
            sanitizedUsername = "user";
        }

        var extension = ".webp";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        return $"{sanitizedUsername}_{timestamp}{extension}";
    }
}
