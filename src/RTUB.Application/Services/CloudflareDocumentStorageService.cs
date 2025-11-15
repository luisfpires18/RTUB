using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of document storage service using Cloudflare R2 (S3-compatible)
/// Uses a shared AmazonS3Client injected via DI
/// </summary>
public class CloudflareDocumentStorageService : IDocumentStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _environment;
    private readonly ILogger<CloudflareDocumentStorageService> _logger;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour

    public CloudflareDocumentStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareDocumentStorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger;
        _environment = hostEnvironment.EnvironmentName;

        // Get Cloudflare R2 configuration
        _bucketName = configuration["Cloudflare:R2:Bucket"];

        if (string.IsNullOrEmpty(_bucketName))
        {
            var errorMsg = "Cloudflare R2 bucket name not configured. Set Cloudflare:R2:Bucket.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }
    }

    public async Task<string?> GetDocumentUrlAsync(string documentPath)
    {
        try
        {
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
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = documentPath
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
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

    public async Task<List<string>> ListFoldersAsync(string prefix = "docs/")
    {
        try
        {
            // Add environment to prefix (e.g., "docs/" becomes "docs/Production/" or "docs/Development/")
            var environmentPrefix = $"{prefix}{_environment}/";
            
            var folders = new HashSet<string>();
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = environmentPrefix,
                Delimiter = "/"
            };

            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(request);
                
                // Add common prefixes (folders)
                foreach (var commonPrefix in response.CommonPrefixes)
                {
                    // Extract folder name from prefix (e.g., "docs/Production/General/" -> "General")
                    var folderName = commonPrefix.TrimEnd('/').Substring(environmentPrefix.Length);
                    if (!string.IsNullOrEmpty(folderName))
                    {
                        folders.Add(folderName);
                    }
                }

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);

            return folders.OrderBy(f => f).ToList();
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error listing folders. Bucket: '{BucketName}', Prefix: '{Prefix}', ErrorCode: {ErrorCode}, Message: {Message}", 
                _bucketName, prefix, ex.ErrorCode, ex.Message);
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing folders with prefix: {Prefix}", prefix);
            return new List<string>();
        }
    }

    public async Task<List<DocumentMetadata>> ListDocumentsInFolderAsync(string folderPath)
    {
        try
        {
            var documents = new List<DocumentMetadata>();
            
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
            {
                folderPath += "/";
            }

            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = folderPath,
                Delimiter = "/" // Only get files in this folder, not subfolders
            };

            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(request);
                
                foreach (var obj in response.S3Objects)
                {
                    // Skip the folder marker itself
                    if (obj.Key.EndsWith("/"))
                        continue;

                    var fileName = Path.GetFileName(obj.Key);
                    var extension = Path.GetExtension(obj.Key);

                    documents.Add(new DocumentMetadata
                    {
                        FileName = fileName,
                        FilePath = obj.Key,
                        SizeBytes = obj.Size ?? 0,
                        LastModified = obj.LastModified ?? DateTime.UtcNow,
                        Extension = extension
                    });
                }

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);

            return documents.OrderBy(d => d.FileName).ToList();
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error listing documents in folder. Bucket: '{BucketName}', FolderPath: '{FolderPath}', ErrorCode: {ErrorCode}, Message: {Message}", 
                _bucketName, folderPath, ex.ErrorCode, ex.Message);
            return new List<DocumentMetadata>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing documents in folder: {FolderPath}", folderPath);
            return new List<DocumentMetadata>();
        }
    }

    public async Task<string> UploadDocumentAsync(string folderPath, string fileName, Stream fileStream, string contentType)
    {
        try
        {
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
            {
                folderPath += "/";
            }

            var documentPath = folderPath + fileName;

            _logger.LogInformation("Attempting to upload document to bucket '{Bucket}' with key: {DocumentPath}", _bucketName, documentPath);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = documentPath,
                InputStream = fileStream,
                ContentType = contentType,
                UseChunkEncoding = false, // Required for Cloudflare R2 compatibility
                DisablePayloadSigning = true // Disable checksum calculation for non-seekable streams
            };

            var response = await _s3Client.PutObjectAsync(request);
            
            _logger.LogInformation("Successfully uploaded document: {DocumentPath}. Response status: {StatusCode}", 
                documentPath, response.HttpStatusCode);
            return documentPath;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading document. Bucket: '{BucketName}', Key: '{Path}', ErrorCode: {ErrorCode}, StatusCode: {StatusCode}, Message: {Message}", 
                _bucketName, folderPath + fileName, ex.ErrorCode, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Failed to upload document '{fileName}' to '{folderPath}' in bucket '{_bucketName}'. Error: {ex.ErrorCode} - {ex.Message}. Please verify your Cloudflare R2 bucket permissions.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading document: {FileName} to {FolderPath}", fileName, folderPath);
            throw;
        }
    }

    public async Task CreateFolderAsync(string folderPath)
    {
        try
        {
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
            {
                folderPath += "/";
            }

            _logger.LogInformation("Attempting to create folder in bucket '{Bucket}' with key: {FolderPath}", _bucketName, folderPath);

            // Create an empty object with "/" suffix to represent a folder
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = folderPath,
                InputStream = new MemoryStream(),
                ContentType = "application/x-directory",
                UseChunkEncoding = false // Required for Cloudflare R2 compatibility
            };

            var response = await _s3Client.PutObjectAsync(request);
            
            _logger.LogInformation("Successfully created folder: {FolderPath}. Response status: {StatusCode}", 
                folderPath, response.HttpStatusCode);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error creating folder. Bucket: '{BucketName}', Key: '{FolderPath}', ErrorCode: {ErrorCode}, StatusCode: {StatusCode}, Message: {Message}", 
                _bucketName, folderPath, ex.ErrorCode, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Failed to create folder '{folderPath}' in bucket '{_bucketName}'. Error: {ex.ErrorCode} - {ex.Message}. Please verify your Cloudflare R2 bucket permissions.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating folder: {FolderPath}", folderPath);
            throw;
        }
    }

    public async Task<long> GetFileSizeAsync(string documentPath)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = documentPath
            };

            var response = await _s3Client.GetObjectMetadataAsync(request);
            return response.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Document not found when getting file size: {DocumentPath}", documentPath);
            return 0;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error getting file size. Bucket: '{BucketName}', Path: '{DocumentPath}', ErrorCode: {ErrorCode}, Message: {Message}", 
                _bucketName, documentPath, ex.ErrorCode, ex.Message);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting file size for: {DocumentPath}", documentPath);
            return 0;
        }
    }
}
