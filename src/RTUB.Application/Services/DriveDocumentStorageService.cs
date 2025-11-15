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
            var folders = new HashSet<string>();
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                Delimiter = "/"
            };

            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(request);
                
                // Add common prefixes (folders)
                foreach (var commonPrefix in response.CommonPrefixes)
                {
                    // Extract folder name from prefix (e.g., "docs/General/" -> "General")
                    var folderName = commonPrefix.TrimEnd('/').Substring(prefix.Length);
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

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = documentPath,
                InputStream = fileStream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(request);
            
            _logger.LogInformation("Successfully uploaded document: {DocumentPath}", documentPath);
            return documentPath;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error uploading document. Bucket: '{BucketName}', Path: '{Path}', ErrorCode: {ErrorCode}, Message: {Message}", 
                _bucketName, folderPath + fileName, ex.ErrorCode, ex.Message);
            throw;
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

            // Create an empty object with "/" suffix to represent a folder
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = folderPath,
                InputStream = new MemoryStream(),
                ContentType = "application/x-directory"
            };

            await _s3Client.PutObjectAsync(request);
            
            _logger.LogInformation("Successfully created folder: {FolderPath}", folderPath);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 error creating folder. Bucket: '{BucketName}', Path: '{FolderPath}', ErrorCode: {ErrorCode}, Message: {Message}", 
                _bucketName, folderPath, ex.ErrorCode, ex.Message);
            throw;
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

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
