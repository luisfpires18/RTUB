using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of document storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveDocumentStorageService : IDocumentStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<DriveDocumentStorageService> _logger;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour

    public DriveDocumentStorageService(IConfiguration configuration, ILogger<DriveDocumentStorageService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Initializing DriveDocumentStorageService");
        
        // Get credentials from environment variables or configuration
        var accessKey = Environment.GetEnvironmentVariable("IDRIVE_ACCESS_KEY") 
                        ?? configuration["IDrive:AccessKey"];
        var secretKey = Environment.GetEnvironmentVariable("IDRIVE_SECRET_KEY") 
                        ?? configuration["IDrive:SecretKey"];
        var endpoint = Environment.GetEnvironmentVariable("IDRIVE_ENDPOINT") 
                       ?? configuration["IDrive:Endpoint"] 
                       ?? "s3.eu-west-4.idrivee2.com";
        _bucketName = Environment.GetEnvironmentVariable("IDRIVE_BUCKET") 
                      ?? configuration["IDrive:Bucket"] 
                      ?? "rtub";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            var errorMsg = "iDrive e2 credentials not configured. Set IDRIVE_ACCESS_KEY and IDRIVE_SECRET_KEY environment variables.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{endpoint}",
            ForcePathStyle = true // Required for S3-compatible services
        };

        _s3Client = new AmazonS3Client(credentials, config);
        _logger.LogInformation("DriveDocumentStorageService initialized with bucket: {BucketName}", _bucketName);
    }

    public async Task<string?> GetDocumentUrlAsync(string documentPath)
    {
        try
        {
            _logger.LogInformation("Requesting document URL for bucket '{BucketName}', path: '{DocumentPath}'", _bucketName, documentPath);

            // Check if file exists first
            var exists = await DocumentExistsAsync(documentPath);
            if (!exists)
            {
                _logger.LogWarning("Cannot generate URL - document not found: {DocumentPath}", documentPath);
                return null;
            }

            // Generate pre-signed URL with content type for PDF
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = documentPath,
                Expires = DateTime.UtcNow.AddMinutes(_urlExpirationMinutes),
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentType = "application/pdf"
                }
            };

            var url = _s3Client.GetPreSignedURL(request);
            _logger.LogInformation("Successfully generated pre-signed URL for document: {DocumentPath}", documentPath);
            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error generating document URL for path: {DocumentPath}. ErrorCode: {ErrorCode}, Message: {Message}", 
                documentPath, ex.ErrorCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating document URL for path: {DocumentPath}", documentPath);
            return null;
        }
    }

    public async Task<bool> DocumentExistsAsync(string documentPath)
    {
        try
        {
            _logger.LogInformation("Checking if document exists in bucket '{BucketName}' with path: '{DocumentPath}'", _bucketName, documentPath);
            
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = documentPath
            };

            await _s3Client.GetObjectMetadataAsync(request);
            _logger.LogDebug("Document exists: {DocumentPath}", documentPath);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document not found (404) in bucket '{BucketName}' with path: '{DocumentPath}'", _bucketName, documentPath);
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error checking document existence. Bucket: '{BucketName}', Path: '{DocumentPath}', ErrorCode: {ErrorCode}, Message: {Message}", 
                _bucketName, documentPath, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking if document exists: {DocumentPath}", documentPath);
            return false;
        }
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
        _logger.LogInformation("DriveDocumentStorageService disposed");
    }
}
