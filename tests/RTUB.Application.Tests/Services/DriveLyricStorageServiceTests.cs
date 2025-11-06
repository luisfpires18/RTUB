using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Tests for DriveLyricStorageService
/// </summary>
public class DriveLyricStorageServiceTests
{
    private readonly Mock<ILogger<DriveLyricStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public DriveLyricStorageServiceTests()
    {
        _mockLogger = new Mock<ILogger<DriveLyricStorageService>>();
        _mockConfiguration = new Mock<IConfiguration>();
    }

    [Fact]
    public void Constructor_WithoutCredentials_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns((string?)null);

        // Clear environment variables to ensure they don't interfere
        var originalAccessKey = Environment.GetEnvironmentVariable("IDRIVE_ACCESS_KEY");
        var originalSecretKey = Environment.GetEnvironmentVariable("IDRIVE_SECRET_KEY");
        
        try
        {
            Environment.SetEnvironmentVariable("IDRIVE_ACCESS_KEY", null);
            Environment.SetEnvironmentVariable("IDRIVE_SECRET_KEY", null);

            // Act & Assert
            Action act = () => new DriveLyricStorageService(_mockConfiguration.Object, _mockLogger.Object);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*credentials not configured*");
        }
        finally
        {
            // Restore environment variables
            Environment.SetEnvironmentVariable("IDRIVE_ACCESS_KEY", originalAccessKey);
            Environment.SetEnvironmentVariable("IDRIVE_SECRET_KEY", originalSecretKey);
        }
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
        DriveLyricStorageService? service = null;
        try
        {
            service = new DriveLyricStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert - Should initialize successfully
            service.Should().NotBeNull();
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Fact]
    public void Constructor_WithDefaultValues_UsesDefaults()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");
        _mockConfiguration.Setup(c => c["IDrive:Endpoint"]).Returns((string?)null);
        _mockConfiguration.Setup(c => c["IDrive:Bucket"]).Returns((string?)null);

        // Act
        DriveLyricStorageService? service = null;
        try
        {
            service = new DriveLyricStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Assert - Should initialize successfully with defaults
            service.Should().NotBeNull();
        }
        finally
        {
            service?.Dispose();
        }
    }

    [Theory]
    [InlineData("Boémios e Trovadores", "Noites Presentes", "lyrics/boemios_e_trovadores/noites_presentes.pdf")]
    [InlineData("01. Test Album", "02. Test Song", "lyrics/01_test_album/02_test_song.pdf")]
    [InlineData("Test & Album", "Test / Song", "lyrics/test_album/test_song.pdf")]
    [InlineData("Álbum com Acentos", "Canção Especial", "lyrics/album_com_acentos/cancao_especial.pdf")]
    public async Task GetObjectKey_NormalizesCorrectly(string albumTitle, string songTitle, string expectedKey)
    {
        // This test verifies the key generation pattern through the GetLyricPdfUrlAsync method's logging
        // Arrange
        _mockConfiguration.Setup(c => c["IDrive:AccessKey"]).Returns("test-access-key");
        _mockConfiguration.Setup(c => c["IDrive:SecretKey"]).Returns("test-secret-key");

        DriveLyricStorageService? service = null;
        try
        {
            service = new DriveLyricStorageService(_mockConfiguration.Object, _mockLogger.Object);

            // Act - Attempt to get URL (will fail but will log the key)
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
        finally
        {
            service?.Dispose();
        }
    }
}
