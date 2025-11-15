using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

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
    private readonly ApplicationDbContext _context;
    private readonly AuditContext _auditContext;
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour
    private const int S3_MAX_DELETE_BATCH_SIZE = 1000; // S3 allows max 1000 objects per delete batch

    public CloudflareDocumentStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareDocumentStorageService> logger,
        ApplicationDbContext context,
        AuditContext auditContext)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _auditContext = auditContext ?? throw new ArgumentNullException(nameof(auditContext));
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

    public async Task<string?> GetDocumentUrlAsync(string documentPath, bool forceDownload = false)
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

            // Generate pre-signed URL
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = documentPath,
                Expires = DateTime.UtcNow.AddMinutes(_urlExpirationMinutes)
            };

            // If forceDownload is true, set content-disposition to attachment
            // Otherwise, set content-type for PDF viewing
            if (forceDownload)
            {
                var fileName = Path.GetFileName(documentPath);
                request.ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentDisposition = $"attachment; filename=\"{fileName}\""
                };
            }
            else
            {
                request.ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentType = "application/pdf"
                };
            }

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
            _logger.LogError(ex, "Failed to check document existence {DocumentPath}", documentPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking document {DocumentPath}", documentPath);
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
            _logger.LogError(ex, "Failed to list folders");
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing folders");
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
            _logger.LogError(ex, "Failed to list documents in {FolderPath}", folderPath);
            return new List<DocumentMetadata>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents in {FolderPath}", folderPath);
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

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = documentPath,
                InputStream = fileStream,
                ContentType = contentType,
                UseChunkEncoding = false, // Required for Cloudflare R2 compatibility
                DisablePayloadSigning = true // Disable checksum calculation for non-seekable streams
            };

            await _s3Client.PutObjectAsync(request);

            // Create audit log
            await CreateAuditLogAsync("Created", fileName, $"Uploaded to {documentPath}");

            return documentPath;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document {FileName}", fileName);
            throw new InvalidOperationException($"Failed to upload document '{fileName}'", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document {FileName}", fileName);
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

            var folderName = folderPath.TrimEnd('/').Split('/').Last();

            // Create an empty object with "/" suffix to represent a folder
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = folderPath,
                InputStream = new MemoryStream(),
                ContentType = "application/x-directory",
                UseChunkEncoding = false // Required for Cloudflare R2 compatibility
            };

            await _s3Client.PutObjectAsync(request);

            // Create audit log
            await CreateAuditLogAsync("Created", folderName, $"Created folder {folderPath}");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder {FolderPath}", folderPath);
            throw new InvalidOperationException($"Failed to create folder '{folderPath}'", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating folder {FolderPath}", folderPath);
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
            _logger.LogError(ex, "Failed to get file size {DocumentPath}", documentPath);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file size {DocumentPath}", documentPath);
            return 0;
        }
    }

    public async Task DeleteDocumentAsync(string documentPath)
    {
        try
        {
            var fileName = Path.GetFileName(documentPath);

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = documentPath
            };

            await _s3Client.DeleteObjectAsync(request);

            // Create audit log
            await CreateAuditLogAsync("Deleted", fileName, $"Deleted from {documentPath}", isCritical: true);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentPath}", documentPath);
            throw new InvalidOperationException($"Failed to delete document '{documentPath}'", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentPath}", documentPath);
            throw;
        }
    }

    public async Task DeleteFolderAsync(string folderPath)
    {
        try
        {
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
            {
                folderPath += "/";
            }

            var folderName = folderPath.TrimEnd('/').Split('/').Last();

            // List all objects in the folder
            var objectsToDelete = new List<string>();
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = folderPath
            };

            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(request);
                
                foreach (var obj in response.S3Objects)
                {
                    objectsToDelete.Add(obj.Key);
                }

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);

            // Delete all objects in batches (S3 allows max 1000 per batch)
            for (int i = 0; i < objectsToDelete.Count; i += S3_MAX_DELETE_BATCH_SIZE)
            {
                var batch = objectsToDelete.Skip(i).Take(S3_MAX_DELETE_BATCH_SIZE).ToList();
                
                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = batch.Select(key => new KeyVersion { Key = key }).ToList()
                };

                await _s3Client.DeleteObjectsAsync(deleteRequest);
            }

            // Create audit log
            await CreateAuditLogAsync("Deleted", folderName, $"Deleted folder {folderPath} ({objectsToDelete.Count} files)", isCritical: true);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to delete folder {FolderPath}", folderPath);
            throw new InvalidOperationException($"Failed to delete folder '{folderPath}'", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting folder {FolderPath}", folderPath);
            throw;
        }
    }

    private async Task CreateAuditLogAsync(string action, string entityDisplayName, string changes, bool isCritical = false)
    {
        try
        {
            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Document",
                EntityId = null,
                Action = action,
                UserId = _auditContext.UserId,
                UserName = _auditContext.UserName ?? "Unknown",
                Timestamp = DateTime.UtcNow,
                Changes = changes,
                EntityDisplayName = entityDisplayName,
                IsCriticalAction = isCritical
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit log failed");
        }
    }
}
