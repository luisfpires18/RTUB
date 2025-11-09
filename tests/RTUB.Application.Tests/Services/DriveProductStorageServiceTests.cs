using FluentAssertions;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RTUB.Application.Services;
using Microsoft.AspNetCore.Http;

namespace RTUB.Application.Tests.Services;

public class DriveProductStorageServiceTests : IDisposable
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<DriveProductStorageService>> _loggerMock;
    private DriveProductStorageService? _service;

    public DriveProductStorageServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<DriveProductStorageService>>();

        // Setup default configuration
        _configurationMock.Setup(x => x["IDrive:WriteAccessKey"]).Returns("test-write-access-key");
        _configurationMock.Setup(x => x["IDrive:WriteSecretKey"]).Returns("test-write-secret-key");
        _configurationMock.Setup(x => x["IDrive:Endpoint"]).Returns("test.endpoint.com");
        _configurationMock.Setup(x => x["IDrive:Bucket"]).Returns("test-bucket");
    }

    [Fact]
    public void Constructor_WithMissingAccessKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _configurationMock.Setup(x => x["IDrive:WriteAccessKey"]).Returns((string?)null);

        // Act & Assert
        var act = () => new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials not configured*");
    }

    [Fact]
    public void Constructor_WithMissingSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange
        _configurationMock.Setup(x => x["IDrive:WriteSecretKey"]).Returns((string?)null);

        // Act & Assert
        var act = () => new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials not configured*");
    }

    [Fact]
    public void Constructor_WithMissingBucket_ThrowsInvalidOperationException()
    {
        // Arrange
        _configurationMock.Setup(x => x["IDrive:Bucket"]).Returns((string?)null);

        // Act & Assert
        var act = () => new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithMissingEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        _configurationMock.Setup(x => x["IDrive:Endpoint"]).Returns((string?)null);

        // Act & Assert
        var act = () => new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint not configured*");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_CreatesService()
    {
        // Act
        var act = () => new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task UploadImageAsync_WithNullFile_ThrowsArgumentException()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var act = async () => await _service.UploadImageAsync((IFormFile)null!, 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*File is required*");
    }

    [Fact]
    public async Task UploadImageAsync_WithNullImageData_ThrowsArgumentException()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var act = async () => await _service.UploadImageAsync((byte[])null!, 1, "image/jpeg");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Image data is required*");
    }

    [Fact]
    public async Task UploadImageAsync_WithEmptyImageData_ThrowsArgumentException()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var act = async () => await _service.UploadImageAsync(Array.Empty<byte>(), 1, "image/jpeg");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Image data is required*");
    }

    [Fact]
    public async Task GetImageUrlAsync_WithNullFilename_ReturnsNull()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await _service.GetImageUrlAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetImageUrlAsync_WithEmptyFilename_ReturnsNull()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await _service.GetImageUrlAsync(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetImageUrlAsync_WithValidFilename_ReturnsPublicUrl()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);
        var filename = "product_1_20241108000000.webp";

        // Act
        var result = await _service.GetImageUrlAsync(filename);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("test.endpoint.com");
        result.Should().Contain("test-bucket");
        result.Should().Contain(filename);
        result.Should().StartWith("https://");
        result.Should().NotContain("Expires="); // No expiration - public URL
        result.Should().NotContain("Signature="); // No signature - public URL
    }

    [Fact]
    public async Task DeleteImageAsync_WithNullFilename_ReturnsFalse()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await _service.DeleteImageAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteImageAsync_WithEmptyFilename_ReturnsFalse()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await _service.DeleteImageAsync(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ImageExistsAsync_WithNullFilename_ReturnsFalse()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await _service.ImageExistsAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ImageExistsAsync_WithEmptyFilename_ReturnsFalse()
    {
        // Arrange
        _service = new DriveProductStorageService(_configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await _service.ImageExistsAsync(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}
