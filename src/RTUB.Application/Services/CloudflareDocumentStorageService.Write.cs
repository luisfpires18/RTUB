// Cloudflare documents: upload, folders, copy, delete and audit log.
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public partial class CloudflareDocumentStorageService
{
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

    public async Task<string?> GetDocumentETagAsync(string documentPath)
    {
        try
        {
            return (await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = _bucketName, Key = documentPath })).ETag;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task CopyDocumentAsync(string sourcePath, string targetPath)
    {
        await CopyObjectAsync(sourcePath, targetPath);
        await CreateAuditLogAsync("Created", Path.GetFileName(targetPath), $"Copied from {sourcePath} to {targetPath}");
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