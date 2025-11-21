using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services.Storage;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of document storage service using iDrive e2 (S3-compatible)
/// </summary>
public class DriveDocumentStorageService : BaseDriveStorageService<DriveDocumentStorageService>, IDocumentStorageService
{
    private readonly int _urlExpirationMinutes = 60; // URL expires after 1 hour

    public DriveDocumentStorageService(IConfiguration configuration, ILogger<DriveDocumentStorageService> logger)
        : base(configuration, logger)
    {
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
            var commonPrefixes = await ListCommonPrefixesAsync(prefix);
            
            // Extract folder names from prefixes
            var folders = new List<string>();
            foreach (var commonPrefix in commonPrefixes)
            {
                // Extract folder name from prefix (e.g., "docs/General/" -> "General")
                var folderName = commonPrefix.TrimEnd('/').Substring(prefix.Length);
                if (!string.IsNullOrEmpty(folderName))
                {
                    folders.Add(folderName);
                }
            }

            return folders;
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
        // Ensure folder path ends with /
        if (!folderPath.EndsWith("/"))
        {
            folderPath += "/";
        }

        var documentPath = folderPath + fileName;

        try
        {
            await PutObjectAsync(documentPath, fileStream, contentType);
            
            return documentPath;
        }
        catch (AmazonS3Exception ex)
        {
            // Add IDrive-specific context to S3 exceptions
            _logger.LogError(ex, "S3 error uploading document: {FileName} to {FolderPath}. Bucket: {Bucket}, ErrorCode: {ErrorCode}", 
                fileName, folderPath, _bucketName, ex.ErrorCode);
            throw new InvalidOperationException($"Failed to upload document '{fileName}' to '{folderPath}' in bucket '{_bucketName}'. Please verify that your IDrive credentials have write permissions to this bucket. Error: {ex.ErrorCode}", ex);
        }
    }

    public async Task CreateFolderAsync(string folderPath)
    {
        // Ensure folder path ends with /
        if (!folderPath.EndsWith("/"))
        {
            folderPath += "/";
        }

        try
        {
            // Create an empty object with "/" suffix to represent a folder
            await PutObjectAsync(folderPath, new MemoryStream(), "application/x-directory");
        }
        catch (AmazonS3Exception ex)
        {
            // Add IDrive-specific context to S3 exceptions
            _logger.LogError(ex, "S3 error creating folder: {FolderPath}. Bucket: {Bucket}, ErrorCode: {ErrorCode}", 
                folderPath, _bucketName, ex.ErrorCode);
            throw new InvalidOperationException($"Failed to create folder '{folderPath}' in bucket '{_bucketName}'. Please verify that your IDrive credentials have write permissions to this bucket. Error: {ex.ErrorCode}", ex);
        }
    }

    public async Task<long> GetFileSizeAsync(string documentPath)
    {
        return await GetObjectSizeAsync(documentPath);
    }

    public Task DeleteDocumentAsync(string documentPath)
    {
        throw new NotImplementedException("Delete operations are not supported for DriveDocumentStorageService. Use CloudflareDocumentStorageService instead.");
    }

    public Task DeleteFolderAsync(string folderPath)
    {
        throw new NotImplementedException("Delete operations are not supported for DriveDocumentStorageService. Use CloudflareDocumentStorageService instead.");
    }
}
