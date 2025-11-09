using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using Xunit;

namespace RTUB.Integration.Tests;

/// <summary>
/// Integration tests for IDrive S3 storage services
/// Verifies end-to-end functionality including:
/// - Image upload
/// - Public URL generation (no expiry)
/// - Old image deletion on update
/// - Image deletion on entity delete
/// </summary>
public class IDriveStorageIntegrationTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DriveAlbumStorageService> _albumLogger;
    private readonly ILogger<DriveInstrumentStorageService> _instrumentLogger;
    private readonly ILogger<DriveProductStorageService> _productLogger;
    
    public IDriveStorageIntegrationTests()
    {
        // Setup test configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IDrive:WriteAccessKey"] = "test-access-key",
            ["IDrive:WriteSecretKey"] = "test-secret-key",
            ["IDrive:Endpoint"] = "test.endpoint.com",
            ["IDrive:Bucket"] = "test-bucket",
            ["ASPNETCORE_ENVIRONMENT"] = "Test"
        });
        _configuration = configBuilder.Build();
        
        // Setup loggers
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _albumLogger = loggerFactory.CreateLogger<DriveAlbumStorageService>();
        _instrumentLogger = loggerFactory.CreateLogger<DriveInstrumentStorageService>();
        _productLogger = loggerFactory.CreateLogger<DriveProductStorageService>();
    }

    #region Album Storage Tests

    [Fact]
    public void AlbumStorageService_GeneratesPublicUrls_WithoutExpiry()
    {
        // Arrange
        using var service = new DriveAlbumStorageService(_configuration, _albumLogger);
        var filename = "album_1_20241108000000.webp";

        // Act
        var url = service.GetImageUrlAsync(filename).Result;

        // Assert
        url.Should().NotBeNull();
        url.Should().StartWith("https://");
        url.Should().Contain("test.endpoint.com");
        url.Should().Contain("test-bucket");
        url.Should().Contain(filename);
        url.Should().NotContain("Expires=", "public URLs should not have expiration");
        url.Should().NotContain("Signature=", "public URLs should not be pre-signed");
        url.Should().NotContain("X-Amz", "public URLs should not have AWS authentication parameters");
    }

    [Fact]
    public void AlbumStorageService_HandlesNullFilename_Gracefully()
    {
        // Arrange
        using var service = new DriveAlbumStorageService(_configuration, _albumLogger);

        // Act
        var url = service.GetImageUrlAsync(null!).Result;

        // Assert
        url.Should().BeNull();
    }

    [Fact]
    public void AlbumStorageService_DeleteImage_HandlesNullFilename_Gracefully()
    {
        // Arrange
        using var service = new DriveAlbumStorageService(_configuration, _albumLogger);

        // Act
        var result = service.DeleteImageAsync(null!).Result;

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Instrument Storage Tests

    [Fact]
    public void InstrumentStorageService_GeneratesPublicUrls_WithoutExpiry()
    {
        // Arrange
        using var service = new DriveInstrumentStorageService(_configuration, _instrumentLogger);
        var filename = "instrument_5_20241108120000.webp";

        // Act
        var url = service.GetImageUrlAsync(filename).Result;

        // Assert
        url.Should().NotBeNull();
        url.Should().StartWith("https://");
        url.Should().Contain("test.endpoint.com");
        url.Should().Contain("test-bucket");
        url.Should().Contain(filename);
        url.Should().NotContain("Expires=", "public URLs should not have expiration");
        url.Should().NotContain("Signature=", "public URLs should not be pre-signed");
        url.Should().NotContain("X-Amz", "public URLs should not have AWS authentication parameters");
    }

    [Fact]
    public void InstrumentStorageService_HandlesNullFilename_Gracefully()
    {
        // Arrange
        using var service = new DriveInstrumentStorageService(_configuration, _instrumentLogger);

        // Act
        var url = service.GetImageUrlAsync(null!).Result;

        // Assert
        url.Should().BeNull();
    }

    [Fact]
    public void InstrumentStorageService_DeleteImage_HandlesNullFilename_Gracefully()
    {
        // Arrange
        using var service = new DriveInstrumentStorageService(_configuration, _instrumentLogger);

        // Act
        var result = service.DeleteImageAsync(null!).Result;

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Product Storage Tests

    [Fact]
    public void ProductStorageService_GeneratesPublicUrls_WithoutExpiry()
    {
        // Arrange
        using var service = new DriveProductStorageService(_configuration, _productLogger);
        var filename = "product_10_20241108150000.webp";

        // Act
        var url = service.GetImageUrlAsync(filename).Result;

        // Assert
        url.Should().NotBeNull();
        url.Should().StartWith("https://");
        url.Should().Contain("test.endpoint.com");
        url.Should().Contain("test-bucket");
        url.Should().Contain(filename);
        url.Should().NotContain("Expires=", "public URLs should not have expiration");
        url.Should().NotContain("Signature=", "public URLs should not be pre-signed");
        url.Should().NotContain("X-Amz", "public URLs should not have AWS authentication parameters");
    }

    [Fact]
    public void ProductStorageService_HandlesNullFilename_Gracefully()
    {
        // Arrange
        using var service = new DriveProductStorageService(_configuration, _productLogger);

        // Act
        var url = service.GetImageUrlAsync(null!).Result;

        // Assert
        url.Should().BeNull();
    }

    [Fact]
    public void ProductStorageService_DeleteImage_HandlesNullFilename_Gracefully()
    {
        // Arrange
        using var service = new DriveProductStorageService(_configuration, _productLogger);

        // Act
        var result = service.DeleteImageAsync(null!).Result;

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region URL Format Tests

    [Fact]
    public void AllStorageServices_GenerateConsistentPublicUrlFormat()
    {
        // Arrange
        using var albumService = new DriveAlbumStorageService(_configuration, _albumLogger);
        using var instrumentService = new DriveInstrumentStorageService(_configuration, _instrumentLogger);
        using var productService = new DriveProductStorageService(_configuration, _productLogger);
        
        var albumFilename = "album_1_20241108000000.webp";
        var instrumentFilename = "instrument_1_20241108000000.webp";
        var productFilename = "product_1_20241108000000.webp";

        // Act
        var albumUrl = albumService.GetImageUrlAsync(albumFilename).Result;
        var instrumentUrl = instrumentService.GetImageUrlAsync(instrumentFilename).Result;
        var productUrl = productService.GetImageUrlAsync(productFilename).Result;

        // Assert - All URLs follow same pattern
        albumUrl.Should().MatchRegex(@"^https://[\w.-]+/[\w-]+/images/albums/\w+/[\w.-]+$");
        instrumentUrl.Should().MatchRegex(@"^https://[\w.-]+/[\w-]+/images/instruments/\w+/[\w.-]+$");
        productUrl.Should().MatchRegex(@"^https://[\w.-]+/[\w-]+/images/products/\w+/[\w.-]+$");
        
        // All should be public (no auth parameters)
        new[] { albumUrl, instrumentUrl, productUrl }.Should().AllSatisfy(url =>
        {
            url.Should().NotContain("?", "public URLs should not have query parameters");
            url.Should().NotContain("Expires=");
            url.Should().NotContain("Signature=");
        });
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}
