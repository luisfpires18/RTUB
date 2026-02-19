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
/// Unit tests for CloudflareAudioStorageService
/// </summary>
public class CloudflareAudioStorageServiceTests
{
    private readonly Mock<ILogger<CloudflareAudioStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<IHostEnvironment> _mockHostEnvironment;

    public CloudflareAudioStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<CloudflareAudioStorageService>>();
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
        Action act = () => new CloudflareAudioStorageService(null!, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithoutBucket_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns((string?)null);

        // Act & Assert
        Action act = () => new CloudflareAudioStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_InitializesSuccessfully()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");

        // Act
        var service = new CloudflareAudioStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task AudioFileExistsAsync_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        var service = new CloudflareAudioStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse());

        // Act
        var result = await service.AudioFileExistsAsync("Test Album", 1, "Test Song");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AudioFileExistsAsync_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        var service = new CloudflareAudioStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(notFoundException);

        // Act
        var result = await service.AudioFileExistsAsync("Test Album", 1, "Nonexistent Song");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAudioUrlAsync_WhenFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        var service = new CloudflareAudioStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(notFoundException);

        // Act
        var result = await service.GetAudioUrlAsync("Test Album", 1, "Nonexistent Song");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Boémios e Trovadores", 1, "Arre Burra", "albums/boemios_e_trovadores/arre_burra.mp3")]
    [InlineData("Boémios e Trovadores", null, "Noites Presentes", "albums/boemios_e_trovadores/noites_presentes.mp3")]
    [InlineData("01. Test Album", 2, "02. Test Song", "albums/01_test_album/02_test_song.mp3")]
    [InlineData("Test & Album", null, "Test / Song", "albums/test_album/test_song.mp3")]
    [InlineData("Álbum com Acentos", 5, "Canção Especial", "albums/album_com_acentos/cancao_especial.mp3")]
    public async Task GetObjectKey_NormalizesCorrectly(string albumTitle, int? trackNumber, string songTitle, string expectedKey)
    {
        // Arrange
        _mockConfiguration.Setup(c => c["Cloudflare:R2:Bucket"]).Returns("rtub");
        var service = new CloudflareAudioStorageService(_mockS3Client.Object, _mockConfiguration.Object, _mockHostEnvironment.Object, _mockLogger.Object);

        // Attempt to get URL (will check the key logged)
        var notFoundException = new AmazonS3Exception("Not found")
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockS3Client.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(notFoundException);

        // Act
        await service.GetAudioUrlAsync(albumTitle, trackNumber, songTitle);

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
