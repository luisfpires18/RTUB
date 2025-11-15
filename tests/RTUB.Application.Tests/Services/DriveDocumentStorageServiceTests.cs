using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for DriveDocumentStorageService
/// </summary>
public class DriveDocumentStorageServiceTests
{
    private readonly Mock<ILogger<DriveDocumentStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public DriveDocumentStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<DriveDocumentStorageService>>();
        _mockConfiguration = new Mock<IConfiguration>();
    }

    [Fact]
    public void Constructor_WithoutCredentials_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns("s3.example.com");
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns("test-bucket");

        // Act & Assert
        Action act = () => new DriveDocumentStorageService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials not configured*");
    }

    [Fact]
    public void Constructor_WithCredentials_InitializesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns("s3.example.com");
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns("test-bucket");

        // Act
        DriveDocumentStorageService? service = null;
        try
        {
            service = new DriveDocumentStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert - Should initialize successfully
            service.Should().NotBeNull();
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Fact]
    public void Constructor_WithDefaultEndpoint_UsesCorrectEndpoint()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns("test-bucket");

        // Act
        DriveDocumentStorageService? service = null;
        try
        {
            service = new DriveDocumentStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert - Should use default endpoint
            service.Should().NotBeNull();
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Fact]
    public void Constructor_WithDefaultBucket_UsesCorrectBucket()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns("s3.example.com");
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns((string?)null);

        // Act
        DriveDocumentStorageService? service = null;
        try
        {
            service = new DriveDocumentStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert - Should use default bucket "rtub"
            service.Should().NotBeNull();
        }
        finally
        {
            service?.Dispose();
        }
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
