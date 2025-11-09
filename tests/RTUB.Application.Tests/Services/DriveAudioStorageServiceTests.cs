using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for DriveAudioStorageService
/// </summary>
public class DriveAudioStorageServiceTests
{
    private readonly Mock<ILogger<DriveAudioStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public DriveAudioStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<DriveAudioStorageService>>();
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
        Action act = () => new DriveAudioStorageService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*credentials not configured*");
    }

    [Fact]
    public void Constructor_WithoutBucketName_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns("s3.example.com");
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns((string?)null);

        // Act & Assert
        Action act = () => new DriveAudioStorageService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bucket name not configured*");
    }

    [Fact]
    public void Constructor_WithoutEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns("test-bucket");

        // Act & Assert
        Action act = () => new DriveAudioStorageService(_mockConfiguration.Object, _mockLogger.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*endpoint not configured*");
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
        DriveAudioStorageService? service = null;
        try
        {
            service = new DriveAudioStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert - Should initialize successfully
            service.Should().NotBeNull();
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Theory]
    [InlineData("Boémios e Trovadores", 1, "Arre Burra", "albums/boemios_e_trovadores/arre_burra.mp3")]
    [InlineData("Boémios e Trovadores", null, "Noites Presentes", "albums/boemios_e_trovadores/noites_presentes.mp3")]
    [InlineData("01. Test Album", 2, "02. Test Song", "albums/01_test_album/02_test_song.mp3")]
    [InlineData("Test & Album", null, "Test / Song", "albums/test_album/test_song.mp3")]
    [InlineData("Álbum com Acentos", 5, "Canção Especial", "albums/album_com_acentos/cancao_especial.mp3")]
    public async Task GetObjectKey_NormalizesCorrectly(string albumTitle, int? trackNumber, string songTitle, string expectedKey)
    {
        // This test verifies the key generation pattern through the GetAudioUrlAsync method's logging
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns("s3.example.com");
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns("test-bucket");

        DriveAudioStorageService? service = null;
        try
        {
            service = new DriveAudioStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Act - Attempt to get URL (will fail but will log the key)
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
        finally
        {
            service?.Dispose();
        }
    }
}
