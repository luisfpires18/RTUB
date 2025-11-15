using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for CloudflareDocumentStorageService
/// </summary>
public class CloudflareDocumentStorageServiceTests
{
    private readonly Mock<ILogger<CloudflareDocumentStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly Mock<ApplicationDbContext> _mockContext;
    private readonly Mock<AuditContext> _mockAuditContext;

    public CloudflareDocumentStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<CloudflareDocumentStorageService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockS3Client = new Mock<IAmazonS3>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();
        _mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        _mockContext = new Mock<ApplicationDbContext>();
        _mockAuditContext = new Mock<AuditContext>();
    }

    [Fact]
    public void Constructor_WithoutBucketName_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns((string?)null);

        // Act & Assert
        Action act = () => new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithBucketName_InitializesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");

        // Act
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullS3Client_ThrowsArgumentNullException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");

        // Act & Assert
        Action act = () => new CloudflareDocumentStorageService(null!, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DocumentMetadata_Properties_CanBeSetAndGet()
    {
        // Arrange
        var metadata = new RTUB.Application.Interfaces.DocumentMetadata
        {
            FileName = "test.pdf",
            FilePath = "docs/test/test.pdf",
            SizeBytes = 1024,
            LastModified = DateTime.UtcNow,
            Extension = ".pdf"
        };

        // Assert
        metadata.FileName.Should().Be("test.pdf");
        metadata.FilePath.Should().Be("docs/test/test.pdf");
        metadata.SizeBytes.Should().Be(1024);
        metadata.Extension.Should().Be(".pdf");
        metadata.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DeleteDocumentAsync_WithValidPath_DeletesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        var deleteResponse = new DeleteObjectResponse
        {
            HttpStatusCode = System.Net.HttpStatusCode.NoContent
        };
        
        _mockS3Client.Setup(s => s.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
            .ReturnsAsync(deleteResponse);

        // Act
        await service.DeleteDocumentAsync("docs/test/document.pdf");

        // Assert
        _mockS3Client.Verify(s => s.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Key == "docs/test/document.pdf"), 
            default), Times.Once);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WithS3Exception_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        _mockS3Client.Setup(s => s.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("Access denied"));

        // Act
        Func<Task> act = async () => await service.DeleteDocumentAsync("docs/test/document.pdf");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to delete document*");
    }

    [Fact]
    public async Task DocumentExistsAsync_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse());

        // Act
        var result = await service.DocumentExistsAsync("docs/test/document.pdf");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DocumentExistsAsync_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = System.Net.HttpStatusCode.NotFound
        };
        
        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(notFoundException);

        // Act
        var result = await service.DocumentExistsAsync("docs/test/nonexistent.pdf");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetFileSizeAsync_WhenFileExists_ReturnsSize()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        var metadataResponse = new GetObjectMetadataResponse
        {
            ContentLength = 2048
        };
        
        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(metadataResponse);

        // Act
        var size = await service.GetFileSizeAsync("docs/test/document.pdf");

        // Assert
        size.Should().Be(2048);
    }

    [Fact]
    public async Task GetFileSizeAsync_WhenFileDoesNotExist_ReturnsZero()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = System.Net.HttpStatusCode.NotFound
        };
        
        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(notFoundException);

        // Act
        var size = await service.GetFileSizeAsync("docs/test/nonexistent.pdf");

        // Assert
        size.Should().Be(0);
    }

    [Fact]
    public async Task DeleteFolderAsync_WithValidPath_DeletesAllObjectsInFolder()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        var listResponse = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new S3Object { Key = "docs/Development/TestFolder/" },
                new S3Object { Key = "docs/Development/TestFolder/document1.pdf" },
                new S3Object { Key = "docs/Development/TestFolder/document2.pdf" }
            },
            IsTruncated = false
        };
        
        _mockS3Client.Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(listResponse);
        
        var deleteResponse = new DeleteObjectsResponse
        {
            DeletedObjects = new List<DeletedObject>
            {
                new DeletedObject { Key = "docs/Development/TestFolder/" },
                new DeletedObject { Key = "docs/Development/TestFolder/document1.pdf" },
                new DeletedObject { Key = "docs/Development/TestFolder/document2.pdf" }
            }
        };
        
        _mockS3Client.Setup(s => s.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), default))
            .ReturnsAsync(deleteResponse);

        // Act
        await service.DeleteFolderAsync("docs/Development/TestFolder");

        // Assert
        _mockS3Client.Verify(s => s.ListObjectsV2Async(
            It.Is<ListObjectsV2Request>(r => 
                r.BucketName == "test-bucket" && 
                r.Prefix == "docs/Development/TestFolder/"), 
            default), Times.Once);
        
        _mockS3Client.Verify(s => s.DeleteObjectsAsync(
            It.Is<DeleteObjectsRequest>(r => 
                r.BucketName == "test-bucket" && 
                r.Objects.Count == 3), 
            default), Times.Once);
    }

    [Fact]
    public async Task DeleteFolderAsync_WithTrailingSlash_DeletesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        var listResponse = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object> { new S3Object { Key = "docs/Development/TestFolder/" } },
            IsTruncated = false
        };
        
        _mockS3Client.Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(listResponse);
        
        var deleteResponse = new DeleteObjectsResponse
        {
            DeletedObjects = new List<DeletedObject> { new DeletedObject { Key = "docs/Development/TestFolder/" } }
        };
        
        _mockS3Client.Setup(s => s.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), default))
            .ReturnsAsync(deleteResponse);

        // Act
        await service.DeleteFolderAsync("docs/Development/TestFolder/");

        // Assert
        _mockS3Client.Verify(s => s.ListObjectsV2Async(
            It.Is<ListObjectsV2Request>(r => r.Prefix == "docs/Development/TestFolder/"), 
            default), Times.Once);
    }

    [Fact]
    public async Task DeleteFolderAsync_WithEmptyFolder_DeletesOnlyFolderMarker()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        var listResponse = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object> { new S3Object { Key = "docs/Development/EmptyFolder/" } },
            IsTruncated = false
        };
        
        _mockS3Client.Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(listResponse);
        
        var deleteResponse = new DeleteObjectsResponse
        {
            DeletedObjects = new List<DeletedObject> { new DeletedObject { Key = "docs/Development/EmptyFolder/" } }
        };
        
        _mockS3Client.Setup(s => s.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), default))
            .ReturnsAsync(deleteResponse);

        // Act
        await service.DeleteFolderAsync("docs/Development/EmptyFolder");

        // Assert
        _mockS3Client.Verify(s => s.DeleteObjectsAsync(
            It.Is<DeleteObjectsRequest>(r => r.Objects.Count == 1), 
            default), Times.Once);
    }

    [Fact]
    public async Task DeleteFolderAsync_WithS3Exception_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _mockContext.Object, _mockAuditContext.Object);
        
        _mockS3Client.Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ThrowsAsync(new AmazonS3Exception("Access denied"));

        // Act
        Func<Task> act = async () => await service.DeleteFolderAsync("docs/Development/TestFolder");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to delete folder*");
    }
}

