using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Implementation of document storage service using Cloudflare R2 (S3-compatible)
/// Uses a shared AmazonS3Client injected via DI
/// </summary>
public class CloudflareDocumentService : ICloudflareDocumentService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _environment;
    private readonly ILogger<CloudflareDocumentService> _logger;

    public CloudflareDocumentService(
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CloudflareDocumentService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger;

        // Get Cloudflare R2 configuration
        var bucketName = configuration["Cloudflare:R2:Bucket"];

        if (string.IsNullOrEmpty(bucketName))
        {
            var errorMsg = "Cloudflare R2 bucket name not configured. Set Cloudflare:R2:Bucket.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _bucketName = bucketName;
        _environment = hostEnvironment.EnvironmentName;
    }

    public async Task<List<CloudflareFolder>> GetFoldersAsync(string rootPath)
    {
        try
        {
            // Ensure root path ends with /
            if (!rootPath.EndsWith("/"))
            {
                rootPath += "/";
            }

            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = rootPath,
                Delimiter = "/"
            };

            var response = await _s3Client.ListObjectsV2Async(request);
            var folders = new List<CloudflareFolder>();

            // Process common prefixes (folders)
            foreach (var prefix in response.CommonPrefixes)
            {
                // Extract folder name from prefix
                var folderName = prefix.TrimEnd('/').Split('/').Last();
                
                // Count files in folder
                var fileCount = await CountFilesInFolderAsync(prefix);

                folders.Add(new CloudflareFolder
                {
                    Name = folderName,
                    Path = prefix,
                    FileCount = fileCount
                });
            }

            return folders.OrderBy(f => f.Name).ToList();
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error getting folders from {RootPath}. ErrorCode: {ErrorCode}, Message: {Message}",
                rootPath, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting folders from {RootPath}", rootPath);
            throw;
        }
    }

    public async Task<List<CloudflareDocument>> GetDocumentsInFolderAsync(string folderPath)
    {
        try
        {
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
            {
                folderPath += "/";
            }

            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = folderPath,
                Delimiter = "/"
            };

            var response = await _s3Client.ListObjectsV2Async(request);
            var documents = new List<CloudflareDocument>();

            // Process objects (files) - exclude the folder itself
            foreach (var obj in response.S3Objects.Where(o => !o.Key.EndsWith("/")))
            {
                var fileName = obj.Key.Split('/').Last();
                var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

                documents.Add(new CloudflareDocument
                {
                    Name = fileName,
                    Path = obj.Key,
                    Extension = extension,
                    Size = obj.Size ?? 0,
                    UploadedDate = obj.LastModified ?? DateTime.UtcNow
                });
            }

            return documents.OrderBy(d => d.Name).ToList();
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error getting documents from folder {FolderPath}. ErrorCode: {ErrorCode}, Message: {Message}",
                folderPath, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting documents from folder {FolderPath}", folderPath);
            throw;
        }
    }

    public async Task<string> UploadDocumentAsync(Stream fileStream, string fileName, string folderPath)
    {
        try
        {
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
            {
                folderPath += "/";
            }

            // Sanitize filename - remove any path separators
            fileName = Path.GetFileName(fileName);
            
            var objectKey = $"{folderPath}{fileName}";

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = fileStream,
                ContentType = GetContentType(fileName),
                UseChunkEncoding = false
            };

            // Add metadata
            putRequest.Metadata.Add("x-amz-meta-uploaded-at", DateTime.UtcNow.ToString("o"));
            putRequest.Metadata.Add("x-amz-meta-environment", _environment);

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Successfully uploaded document {FileName} to {FolderPath}", fileName, folderPath);
                return objectKey;
            }
            else
            {
                var errorMsg = $"Failed to upload document. Status code: {response.HttpStatusCode}";
                _logger.LogError(errorMsg);
                throw new Exception(errorMsg);
            }
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading document {FileName} to {FolderPath}. ErrorCode: {ErrorCode}, Message: {Message}",
                fileName, folderPath, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading document {FileName} to {FolderPath}", fileName, folderPath);
            throw;
        }
    }

    public async Task<string> GetDocumentUrlAsync(string documentPath)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = documentPath,
                Expires = DateTime.UtcNow.AddHours(1) // URL valid for 1 hour
            };

            var url = await _s3Client.GetPreSignedURLAsync(request);
            return url;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error getting presigned URL for {DocumentPath}. ErrorCode: {ErrorCode}, Message: {Message}",
                documentPath, ex.ErrorCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting presigned URL for {DocumentPath}", documentPath);
            throw;
        }
    }

    public async Task<bool> CreateFolderAsync(string folderPath)
    {
        try
        {
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
            {
                folderPath += "/";
            }

            // Create an empty object to represent the folder
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = folderPath,
                ContentBody = string.Empty,
                UseChunkEncoding = false
            };

            var response = await _s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger.LogInformation("Successfully created folder {FolderPath}", folderPath);
                return true;
            }

            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error creating folder {FolderPath}. ErrorCode: {ErrorCode}, Message: {Message}",
                folderPath, ex.ErrorCode, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating folder {FolderPath}", folderPath);
            return false;
        }
    }

    /// <summary>
    /// Helper method to count files in a folder
    /// </summary>
    private async Task<int> CountFilesInFolderAsync(string folderPath)
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = folderPath,
                Delimiter = "/"
            };

            var response = await _s3Client.ListObjectsV2Async(request);
            // Count only files, not subfolders or the folder marker itself
            return response.S3Objects.Count(o => !o.Key.EndsWith("/"));
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Helper method to determine content type based on file extension
    /// </summary>
    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }
}
