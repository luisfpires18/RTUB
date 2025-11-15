using Amazon.S3;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
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

    public CloudflareDocumentStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<CloudflareDocumentStorageService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockS3Client = new Mock<IAmazonS3>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();
        _mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
    }

    [Fact]
    public void Constructor_WithoutBucketName_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns((string?)null);

        // Act & Assert
        Action act = () => new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithBucketName_InitializesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");

        // Act
        var service = new CloudflareDocumentStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullS3Client_ThrowsArgumentNullException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("test-bucket");

        // Act & Assert
        Action act = () => new CloudflareDocumentStorageService(null!, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
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
}
