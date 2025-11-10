using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for CloudflareImageStorageService
/// </summary>
public class CloudflareImageStorageServiceTests
{
    private readonly Mock<ILogger<CloudflareImageStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public CloudflareImageStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<CloudflareImageStorageService>>();
        _mockConfiguration = new Mock<IConfiguration>();
    }

    [Fact]
    public void Constructor_WithoutAccessKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        // Act & Assert
        Action act = () => new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials not configured*");
    }

    [Fact]
    public void Constructor_WithoutSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        // Act & Assert
        Action act = () => new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials not configured*");
    }

    [Fact]
    public void Constructor_WithoutAccountId_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        // Act & Assert
        Action act = () => new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*account ID not configured*");
    }

    [Fact]
    public void Constructor_WithoutBucket_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns((string?)null);

        // Act & Assert
        Action act = () => new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_InitializesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        // Act
        CloudflareImageStorageService? service = null;
        try
        {
            service = new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();

            // Verify initialization logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("initialized for bucket")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Fact]
    public async Task DeleteImageAsync_WithEmptyUrl_LogsWarningAndReturns()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        CloudflareImageStorageService? service = null;
        try
        {
            service = new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            await service.DeleteImageAsync("");

            // Assert - Should log warning
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("empty URL")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Fact]
    public async Task ImageExistsAsync_WithEmptyUrl_ReturnsFalse()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        CloudflareImageStorageService? service = null;
        try
        {
            service = new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await service.ImageExistsAsync("");

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ImageExistsAsync_WithInvalidUrl_ReturnsFalse(string? invalidUrl)
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        CloudflareImageStorageService? service = null;
        try
        {
            service = new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Act
            var result = await service.ImageExistsAsync(invalidUrl!);

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Fact]
    public void Dispose_DisposesS3Client()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        var service = new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);

        // Act & Assert - Should not throw
        Action act = () => service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_LogsInitializationMessage()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccessKeyId"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:SecretAccessKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:AccountId"]).Returns("test-account-id");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        // Act
        CloudflareImageStorageService? service = null;
        try
        {
            service = new CloudflareImageStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert - Verify logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cloudflare R2 image storage service initialized")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            service?.Dispose();
        }
    }
}
