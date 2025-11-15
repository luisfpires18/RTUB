using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for CloudflareDocumentService
/// </summary>
public class CloudflareDocumentServiceTests
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;
    private readonly Mock<ILogger<CloudflareDocumentService>> _mockLogger;

    public CloudflareDocumentServiceTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();
        _mockLogger = new Mock<ILogger<CloudflareDocumentService>>();

        // Setup default configuration
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");
        _mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Test");
    }

    [Fact]
    public void Constructor_WithoutBucketName_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns((string?)null);

        // Act & Assert
        Action act = () => new CloudflareDocumentService(
            _mockS3Client.Object,
            _mockConfiguration.Object,
            _mockHostEnvironment.Object,
            _mockLogger.Object);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithNullS3Client_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new CloudflareDocumentService(
            null!,
            _mockConfiguration.Object,
            _mockHostEnvironment.Object,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_InitializesSuccessfully()
    {
        // Act
        var service = new CloudflareDocumentService(
            _mockS3Client.Object,
            _mockConfiguration.Object,
            _mockHostEnvironment.Object,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFoldersAsync_ReturnsFoldersOrderedByName()
    {
        // Arrange
        var service = CreateService();
        var response = new ListObjectsV2Response
        {
            CommonPrefixes = new List<string>
            {
                "docs/ZFolder/",
                "docs/AFolder/",
                "docs/MFolder/"
            }
        };

        _mockS3Client
            .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Mock count for each folder
        _mockS3Client
            .Setup(s => s.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.Prefix.Contains("AFolder")), default))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = new List<S3Object>
                {
                    new S3Object { Key = "docs/AFolder/file1.pdf", Size = 1000 }
                }
            });

        _mockS3Client
            .Setup(s => s.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.Prefix.Contains("MFolder")), default))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects = new List<S3Object>
                {
                    new S3Object { Key = "docs/MFolder/file1.pdf", Size = 1000 },
                    new S3Object { Key = "docs/MFolder/file2.docx", Size = 2000 }
                }
            });

        _mockS3Client
            .Setup(s => s.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.Prefix.Contains("ZFolder")), default))
            .ReturnsAsync(new ListObjectsV2Response { S3Objects = new List<S3Object>() });

        // Act
        var result = await service.GetFoldersAsync("docs/");

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("AFolder");
        result[1].Name.Should().Be("MFolder");
        result[2].Name.Should().Be("ZFolder");
    }

    [Fact]
    public async Task GetDocumentsInFolderAsync_ReturnsDocumentsOrderedByName()
    {
        // Arrange
        var service = CreateService();
        var now = DateTime.UtcNow;
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new S3Object
                {
                    Key = "docs/test/document2.pdf",
                    Size = 2000,
                    LastModified = now
                },
                new S3Object
                {
                    Key = "docs/test/document1.docx",
                    Size = 1000,
                    LastModified = now.AddDays(-1)
                },
                new S3Object
                {
                    Key = "docs/test/", // Folder marker - should be excluded
                    Size = 0,
                    LastModified = now
                }
            }
        };

        _mockS3Client
            .Setup(s => s.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var result = await service.GetDocumentsInFolderAsync("docs/test/");

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("document1.docx");
        result[0].Extension.Should().Be("docx");
        result[0].Size.Should().Be(1000);
        result[1].Name.Should().Be("document2.pdf");
        result[1].Extension.Should().Be("pdf");
    }

    [Fact]
    public async Task UploadDocumentAsync_SanitizesFileName()
    {
        // Arrange
        var service = CreateService();
        var memoryStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var response = new PutObjectResponse
        {
            HttpStatusCode = System.Net.HttpStatusCode.OK
        };

        _mockS3Client
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(response);

        // Act
        var result = await service.UploadDocumentAsync(memoryStream, "../../malicious.pdf", "docs/test/");

        // Assert
        result.Should().Be("docs/test/malicious.pdf");
    }

    [Theory]
    [InlineData("test.pdf", "application/pdf")]
    [InlineData("test.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("test.txt", "text/plain")]
    [InlineData("test.csv", "text/csv")]
    [InlineData("test.unknown", "application/octet-stream")]
    public async Task UploadDocumentAsync_SetsCorrectContentType(string fileName, string expectedContentType)
    {
        // Arrange
        var service = CreateService();
        var memoryStream = new MemoryStream(new byte[] { 1, 2, 3 });
        PutObjectRequest? capturedRequest = null;

        _mockS3Client
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        // Act
        await service.UploadDocumentAsync(memoryStream, fileName, "docs/test/");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ContentType.Should().Be(expectedContentType);
    }

    [Fact]
    public async Task GetDocumentUrlAsync_ReturnsPresignedUrl()
    {
        // Arrange
        var service = CreateService();
        var expectedUrl = "https://example.com/presigned-url";

        _mockS3Client
            .Setup(s => s.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await service.GetDocumentUrlAsync("docs/test/document.pdf");

        // Assert
        result.Should().Be(expectedUrl);
    }

    [Fact]
    public async Task CreateFolderAsync_CreatesEmptyObject()
    {
        // Arrange
        var service = CreateService();
        PutObjectRequest? capturedRequest = null;

        _mockS3Client
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        // Act
        var result = await service.CreateFolderAsync("docs/newfolder");

        // Assert
        result.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Key.Should().Be("docs/newfolder/");
        capturedRequest.ContentBody.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFolderAsync_HandlesFailureGracefully()
    {
        // Arrange
        var service = CreateService();

        _mockS3Client
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("Test error"));

        // Act
        var result = await service.CreateFolderAsync("docs/newfolder");

        // Assert
        result.Should().BeFalse();
    }

    private CloudflareDocumentService CreateService()
    {
        return new CloudflareDocumentService(
            _mockS3Client.Object,
            _mockConfiguration.Object,
            _mockHostEnvironment.Object,
            _mockLogger.Object);
    }
}
