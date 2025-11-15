using Amazon.S3;
using Amazon.S3.Model;
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
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns("s3.eu-west-4.idrivee2.com");
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns("test-bucket");

        // Act
        var service = new DriveDocumentStorageService(_mockConfiguration.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");
        var service = new DriveDocumentStorageService(_mockConfiguration.Object, _mockLogger.Object);

        // Act & Assert - should not throw
        service.Dispose();
        service.Dispose();
    }
}
