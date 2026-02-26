using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of document storage service using Cloudflare R2 (S3-compatible)
/// Uses a shared AmazonS3Client injected via DI
/// </summary>
public class CloudflareDocumentStorageService : BaseCloudflareStorageService<CloudflareDocumentStorageService>, IDocumentStorageService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuditContext _auditContext;
    private readonly int _urlExpirationMinutes;
    private const int S3_MAX_DELETE_BATCH_SIZE = 1000; // S3 allows max 1000 objects per delete batch

    public CloudflareDocumentStorageService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareDocumentStorageService> logger,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuditContext auditContext,
        IOptions<StorageOptions>? storageOptions = null)
        : base(s3Client, configuration, hostEnvironment, logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _auditContext = auditContext ?? throw new ArgumentNullException(nameof(auditContext));
        _urlExpirationMinutes = storageOptions?.Value.UrlExpirationMinutes ?? 60;
    }

    public async Task<string?> GetDocumentUrlAsync(string documentPath, bool forceDownload = false)
    {
        ResponseHeaderOverrides? headerOverrides;

        if (forceDownload)
        {
            var fileName = Path.GetFileName(documentPath);
            headerOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{fileName}\""
            };
        }
        else
        {
            headerOverrides = new ResponseHeaderOverrides
            {
                ContentType = "application/pdf"
            };
        }

        return await GeneratePreSignedUrlAsync(documentPath, _urlExpirationMinutes, headerOverrides);
    }

    public async Task<bool> DocumentExistsAsync(string documentPath)
    {
        return await ObjectExistsAsync(documentPath);
    }

    public async Task<List<string>> ListFoldersAsync(string prefix = "docs/")
    {
        try
        {
            // Add environment to prefix (e.g., "docs/" becomes "docs/Production/" or "docs/Development/")
            var environmentPrefix = $"{prefix}{_environment}/";

            var commonPrefixes = await ListCommonPrefixesAsync(environmentPrefix);

            // Extract folder names from prefixes
            var folders = new List<string>();
            foreach (var commonPrefix in commonPrefixes)
            {
                // Extract folder name from prefix (e.g., "docs/Production/General/" -> "General")
                var folderName = commonPrefix.TrimEnd('/').Substring(environmentPrefix.Length);
                if (!string.IsNullOrEmpty(folderName))
                {
                    folders.Add(folderName);
                }
            }

            return folders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing folders");
            return [];
        }
    }

    public async Task<List<string>> ListSubfoldersAsync(string folderPath)
    {
        try
        {
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
            {
                folderPath += "/";
            }

            var commonPrefixes = await ListCommonPrefixesAsync(folderPath);

            // Extract folder names from prefixes
            var folders = new List<string>();
            foreach (var commonPrefix in commonPrefixes)
            {
                // Ensure the prefix is long enough before extracting folder name
                var trimmedPrefix = commonPrefix.TrimEnd('/');
                if (trimmedPrefix.Length > folderPath.Length)
                {
                    var folderName = trimmedPrefix.Substring(folderPath.Length);
                    if (!string.IsNullOrEmpty(folderName))
                    {
                        folders.Add(folderName);
                    }
                }
            }

            return folders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing subfolders in {FolderPath}", folderPath);
            return [];
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

                // S3Objects can be null if the folder doesn't exist
                if (response.S3Objects != null)
                {
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
                }

                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);

            return documents.OrderBy(d => d.FileName).ToList();
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to list documents in {FolderPath}", folderPath);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents in {FolderPath}", folderPath);
            return [];
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

            // Ensure folder exists before uploading (only create if it doesn't exist)
            var folderExists = await ObjectExistsAsync(folderPath);
            if (!folderExists)
            {
                await CreateFolderAsync(folderPath);
            }

            var documentPath = folderPath + fileName;

            await PutObjectAsync(documentPath, fileStream, contentType, request =>
            {
                request.UseChunkEncoding = false; // Required for Cloudflare R2 compatibility
                request.DisablePayloadSigning = true; // Disable checksum calculation for non-seekable streams
            });

            // Create audit log
            await CreateAuditLogAsync("Created", fileName, $"Uploaded to {documentPath}");

            return documentPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document {FileName}", fileName);
            throw new InvalidOperationException($"Failed to upload document '{fileName}'", ex);
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

            // Check if folder already exists to avoid concurrent request rate limiting
            var exists = await ObjectExistsAsync(folderPath);
            if (exists)
            {
                return; // Folder already exists, no need to create
            }

            var folderName = folderPath.TrimEnd('/').Split('/').Last();

            // Create an empty object with "/" suffix to represent a folder
            await PutObjectAsync(folderPath, new MemoryStream(), "application/x-directory", request =>
            {
                request.UseChunkEncoding = false; // Required for Cloudflare R2 compatibility
            });

            // Create audit log
            await CreateAuditLogAsync("Created", folderName, $"Created folder {folderPath}", entityType: "Folder");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating folder {FolderPath}", folderPath);
            throw new InvalidOperationException($"Failed to create folder '{folderPath}'", ex);
        }
    }

    public async Task<long> GetFileSizeAsync(string documentPath)
    {
        return await GetObjectSizeAsync(documentPath);
    }

    public async Task DeleteDocumentAsync(string documentPath)
    {
        try
        {
            var fileName = Path.GetFileName(documentPath);

            await DeleteObjectAsync(documentPath);

            // Create audit log
            await CreateAuditLogAsync("Deleted", fileName, $"Deleted from {documentPath}", isCritical: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentPath}", documentPath);
            throw new InvalidOperationException($"Failed to delete document '{documentPath}'", ex);
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
            var objects = await ListObjectsAsync(folderPath);
            var objectKeys = objects.Select(obj => obj.Key).ToList();

            // Delete all objects in batches
            await DeleteObjectsBatchAsync(objectKeys, S3_MAX_DELETE_BATCH_SIZE);

            // Create audit log
            await CreateAuditLogAsync("Deleted", folderName, $"Deleted folder {folderPath} ({objectKeys.Count} files)", isCritical: true, entityType: "Folder");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting folder {FolderPath}", folderPath);
            throw new InvalidOperationException($"Failed to delete folder '{folderPath}'", ex);
        }
    }

    private async Task CreateAuditLogAsync(string action, string entityDisplayName, string changes, bool isCritical = false, string entityType = "Document")
    {
        try
        {
            using var auditCtx = _contextFactory.CreateDbContext();
            auditCtx.AuditLogs.Add(new AuditLog
            {
                EntityType = entityType,
                EntityId = null,
                Action = action,
                UserId = _auditContext.UserId,
                UserName = _auditContext.UserName,
                Timestamp = DateTime.UtcNow,
                Changes = changes,
                EntityDisplayName = entityDisplayName,
                IsCriticalAction = isCritical
            });
            await auditCtx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit log failed");
        }
    }
}
