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
/// Unit tests for CloudflareImageStorageService
/// </summary>
public class CloudflareImageStorageServiceTests
{
    private readonly Mock<ILogger<CloudflareImageStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;

    public CloudflareImageStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<CloudflareImageStorageService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockS3Client = new Mock<IAmazonS3>();
        _mockHostEnvironment = new Mock<IHostEnvironment>();
        
        // Setup default environment name
        _mockHostEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
    }

    [Fact]
    public void Constructor_WithNullS3Client_ThrowsArgumentNullException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:PublicUrl"]).Returns("https://pub-test.r2.dev");

        // Act & Assert
        Action act = () => new CloudflareImageStorageService(null!, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithoutPublicUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:PublicUrl"]).Returns((string?)null);

        // Act & Assert
        Action act = () => new CloudflareImageStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*public URL not configured*");
    }

    [Fact]
    public void Constructor_WithoutBucket_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["Cloudflare:R2:PublicUrl"]).Returns("https://pub-test.r2.dev");

        // Act & Assert
        Action act = () => new CloudflareImageStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_InitializesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:PublicUrl"]).Returns("https://pub-test.r2.dev");

        // Act
        var service = new CloudflareImageStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();

        // Verify initialization logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_WithEmptyUrl_LogsWarningAndReturns()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:PublicUrl"]).Returns("https://pub-test.r2.dev");

        var service = new CloudflareImageStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

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

    [Fact]
    public async Task ImageExistsAsync_WithEmptyUrl_ReturnsFalse()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:PublicUrl"]).Returns("https://pub-test.r2.dev");

        var service = new CloudflareImageStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        // Act
        var result = await service.ImageExistsAsync("");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ImageExistsAsync_WithInvalidUrl_ReturnsFalse(string? invalidUrl)
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:PublicUrl"]).Returns("https://pub-test.r2.dev");

        var service = new CloudflareImageStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        // Act
        var result = await service.ImageExistsAsync(invalidUrl!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Constructor_LogsInitializationMessage()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        _mockConfiguration.Setup(c => c["Cloudflare:R2:PublicUrl"]).Returns("https://pub-test.r2.dev");

        // Act
        var service = new CloudflareImageStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

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
}
