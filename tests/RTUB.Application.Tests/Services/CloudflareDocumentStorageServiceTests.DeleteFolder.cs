using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Moq;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

// CloudflareDocumentStorageServiceTests: Delete Folder tests.
public partial class CloudflareDocumentStorageServiceTests
{
    [Fact]
    public async Task DeleteFolderAsync_WithValidPath_DeletesAllObjectsInFolder()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _fixture.CreateContextFactory(), _auditContext);

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
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _fixture.CreateContextFactory(), _auditContext);

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
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _fixture.CreateContextFactory(), _auditContext);

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
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object, _fixture.CreateContextFactory(), _auditContext);

        _mockS3Client.Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ThrowsAsync(new AmazonS3Exception("Access denied"));

        // Act
        Func<Task> act = async () => await service.DeleteFolderAsync("docs/Development/TestFolder");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to delete folder*");
    }
}
