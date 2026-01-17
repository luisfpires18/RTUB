using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using System.Net;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for CloudflareLyricStorageService
/// </summary>
public class CloudflareLyricStorageServiceTests
{
    private readonly Mock<ILogger<CloudflareLyricStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;

    public CloudflareLyricStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<CloudflareLyricStorageService>>();
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

        // Act & Assert
        Action act = () => new CloudflareLyricStorageService(null!, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithoutBucket_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns((string?)null);

        // Act & Assert
        Action act = () => new CloudflareLyricStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_InitializesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        // Act
        var service = new CloudflareLyricStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task LyricPdfExistsAsync_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        var service = new CloudflareLyricStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse());

        // Act
        var result = await service.LyricPdfExistsAsync("Test Album", "Test Song");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task LyricPdfExistsAsync_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        var service = new CloudflareLyricStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(notFoundException);

        // Act
        var result = await service.LyricPdfExistsAsync("Test Album", "Nonexistent Song");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetLyricPdfUrlAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        var service = new CloudflareLyricStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(notFoundException);

        // Act
        var result = await service.GetLyricPdfUrlAsync("Test Album", "Nonexistent Song");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Boémios e Trovadores", "Arre Burra", "lyrics/boemios_e_trovadores/arre_burra.pdf")]
    [InlineData("Boémios e Trovadores", "Noites Presentes", "lyrics/boemios_e_trovadores/noites_presentes.pdf")]
    [InlineData("01. Test Album", "02. Test Song", "lyrics/01_test_album/02_test_song.pdf")]
    [InlineData("Test & Album", "Test / Song", "lyrics/test_album/test_song.pdf")]
    [InlineData("Álbum com Acentos", "Canção Especial", "lyrics/album_com_acentos/cancao_especial.pdf")]
    public async Task GetObjectKey_NormalizesCorrectly(string albumTitle, string songTitle, string expectedKey)
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        var service = new CloudflareLyricStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        // Attempt to get URL (will check the key logged)
        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(notFoundException);

        // Act
        await service.GetLyricPdfUrlAsync(albumTitle, songTitle);

        // Assert - Verify the correct key was logged
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedKey)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
