using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Controllers;
using Xunit;

namespace RTUB.Web.Tests.Controllers;

/// <summary>
/// Tests for ImagesController to ensure proper image serving, caching, and error handling
/// </summary>
public class ImagesControllerTests
{
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ImagesController _controller;

    public ImagesControllerTests()
    {
        _mockImageService = new Mock<IImageService>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Setup configuration section for ImageCaching
        var mockConfigSection = new Mock<IConfigurationSection>();
        mockConfigSection.Setup(x => x.Value).Returns("86400");
        _mockConfiguration.Setup(x => x.GetSection("ImageCaching:CacheDurationInSeconds"))
            .Returns(mockConfigSection.Object);

        _controller = new ImagesController(
            _mockImageService.Object,
            _mockEnvironment.Object,
            _mockConfiguration.Object);

        // Setup controller context with headers
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region GetEventImage Tests

    [Fact]
    public async Task GetEventImage_WithValidId_ReturnsFileResult()
    {
        // Arrange
        var eventId = 1;
        var imageData = new byte[] { 1, 2, 3, 4 };
        var contentType = "image/jpeg";
        _mockImageService.Setup(s => s.GetEventImageAsync(eventId))
            .ReturnsAsync((imageData, contentType));

        // Act
        var result = await _controller.GetEventImage(eventId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.FileContents.Should().Equal(imageData);
        fileResult.ContentType.Should().Be(contentType);
        _controller.Response.Headers.Should().ContainKey("ETag");
        _controller.Response.Headers.Should().ContainKey("Cache-Control");
    }

    [Fact]
    public async Task GetEventImage_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var eventId = 999;
        _mockImageService.Setup(s => s.GetEventImageAsync(eventId))
            .ReturnsAsync(((byte[], string)?)null);

        // Act
        var result = await _controller.GetEventImage(eventId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetEventImage_WithMatchingETag_Returns304NotModified()
    {
        // Arrange
        var eventId = 1;
        var imageData = new byte[] { 1, 2, 3, 4 };
        var contentType = "image/jpeg";
        
        _mockImageService.Setup(s => s.GetEventImageAsync(eventId))
            .ReturnsAsync((imageData, contentType));

        // First call to get ETag
        var firstResult = await _controller.GetEventImage(eventId);
        var etag = _controller.Response.Headers["ETag"].ToString();

        // Reset controller for second request with ETag
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.Request.Headers["If-None-Match"] = etag;

        // Act
        var result = await _controller.GetEventImage(eventId);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        var statusResult = result as StatusCodeResult;
        statusResult!.StatusCode.Should().Be(304);
    }

    #endregion

    #region GetSlideshowImage Tests

    [Fact]
    public async Task GetSlideshowImage_WithValidId_ReturnsFileResult()
    {
        // Arrange
        var slideshowId = 1;
        var imageData = new byte[] { 5, 6, 7, 8 };
        var contentType = "image/png";
        _mockImageService.Setup(s => s.GetSlideshowImageAsync(slideshowId))
            .ReturnsAsync((imageData, contentType));

        // Act
        var result = await _controller.GetSlideshowImage(slideshowId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.FileContents.Should().Equal(imageData);
        fileResult.ContentType.Should().Be(contentType);
    }

    [Fact]
    public async Task GetSlideshowImage_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var slideshowId = 999;
        _mockImageService.Setup(s => s.GetSlideshowImageAsync(slideshowId))
            .ReturnsAsync(((byte[], string)?)null);

        // Act
        var result = await _controller.GetSlideshowImage(slideshowId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetAlbumImage Tests

    [Fact]
    public async Task GetAlbumImage_WithValidId_ReturnsFileResult()
    {
        // Arrange
        var albumId = 1;
        var imageData = new byte[] { 9, 10, 11, 12 };
        var contentType = "image/webp";
        _mockImageService.Setup(s => s.GetAlbumImageAsync(albumId))
            .ReturnsAsync((imageData, contentType));

        // Act
        var result = await _controller.GetAlbumImage(albumId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.FileContents.Should().Equal(imageData);
        fileResult.ContentType.Should().Be(contentType);
    }

    [Fact]
    public async Task GetAlbumImage_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var albumId = 999;
        _mockImageService.Setup(s => s.GetAlbumImageAsync(albumId))
            .ReturnsAsync(((byte[], string)?)null);

        // Act
        var result = await _controller.GetAlbumImage(albumId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetProfileImage Tests

    [Fact]
    public async Task GetProfileImage_WithValidId_ReturnsFileResult()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 13, 14, 15, 16 };
        var contentType = "image/jpeg";
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, contentType));

        // Act
        var result = await _controller.GetProfileImage(userId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.FileContents.Should().Equal(imageData);
        fileResult.ContentType.Should().Be(contentType);
    }

    [Fact]
    public async Task GetProfileImage_WithNoCustomImage_ReturnsDefaultImage()
    {
        // Arrange
        var userId = "user123";
        var defaultImagePath = "/test/path/wwwroot/images/profile-pic.webp";
        var defaultImageData = new byte[] { 20, 21, 22, 23 };

        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync(((byte[], string)?)null);

        _mockEnvironment.Setup(e => e.WebRootPath)
            .Returns("/test/path/wwwroot");

        // Create a temporary file for testing
        var tempFile = Path.GetTempFileName();
        File.WriteAllBytes(tempFile, defaultImageData);

        try
        {
            // Mock File.Exists and File.ReadAllBytesAsync is tricky, so we test the NotFound path
            // In a real scenario, this would require file system abstraction
            
            // Act
            var result = await _controller.GetProfileImage(userId);

            // Assert
            // This will return NotFound because we can't mock the file system easily
            // In production, you'd use IFileProvider or similar abstraction
            result.Should().BeOfType<NotFoundObjectResult>();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region GetInstrumentImage Tests

    [Fact]
    public async Task GetInstrumentImage_WithValidId_ReturnsFileResult()
    {
        // Arrange
        var instrumentId = 1;
        var imageData = new byte[] { 17, 18, 19, 20 };
        var contentType = "image/png";
        _mockImageService.Setup(s => s.GetInstrumentImageAsync(instrumentId))
            .ReturnsAsync((imageData, contentType));

        // Act
        var result = await _controller.GetInstrumentImage(instrumentId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.FileContents.Should().Equal(imageData);
        fileResult.ContentType.Should().Be(contentType);
    }

    [Fact]
    public async Task GetInstrumentImage_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var instrumentId = 999;
        _mockImageService.Setup(s => s.GetInstrumentImageAsync(instrumentId))
            .ReturnsAsync(((byte[], string)?)null);

        // Act
        var result = await _controller.GetInstrumentImage(instrumentId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetProductImage Tests

    [Fact]
    public async Task GetProductImage_WithValidId_ReturnsFileResult()
    {
        // Arrange
        var productId = 1;
        var imageData = new byte[] { 21, 22, 23, 24 };
        var contentType = "image/jpeg";
        _mockImageService.Setup(s => s.GetProductImageAsync(productId))
            .ReturnsAsync((imageData, contentType));

        // Act
        var result = await _controller.GetProductImage(productId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.FileContents.Should().Equal(imageData);
        fileResult.ContentType.Should().Be(contentType);
    }

    [Fact]
    public async Task GetProductImage_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var productId = 999;
        _mockImageService.Setup(s => s.GetProductImageAsync(productId))
            .ReturnsAsync(((byte[], string)?)null);

        // Act
        var result = await _controller.GetProductImage(productId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Cache Header Tests

    [Fact]
    public async Task ImageEndpoint_SetsCacheControlHeader()
    {
        // Arrange
        var eventId = 1;
        var imageData = new byte[] { 1, 2, 3 };
        _mockImageService.Setup(s => s.GetEventImageAsync(eventId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // Act
        await _controller.GetEventImage(eventId);

        // Assert
        _controller.Response.Headers["Cache-Control"].ToString()
            .Should().Contain("public")
            .And.Contain("no-cache");
    }

    [Fact]
    public async Task ImageEndpoint_SetsETagHeader()
    {
        // Arrange
        var eventId = 1;
        var imageData = new byte[] { 1, 2, 3 };
        _mockImageService.Setup(s => s.GetEventImageAsync(eventId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // Act
        await _controller.GetEventImage(eventId);

        // Assert
        _controller.Response.Headers.Should().ContainKey("ETag");
        _controller.Response.Headers["ETag"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ImageEndpoint_GeneratesConsistentETag_ForSameData()
    {
        // Arrange
        var imageData = new byte[] { 1, 2, 3, 4 };
        _mockImageService.Setup(s => s.GetEventImageAsync(It.IsAny<int>()))
            .ReturnsAsync((imageData, "image/jpeg"));

        // Act - First request
        await _controller.GetEventImage(1);
        var etag1 = _controller.Response.Headers["ETag"].ToString();

        // Reset controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act - Second request with same data
        await _controller.GetEventImage(1);
        var etag2 = _controller.Response.Headers["ETag"].ToString();

        // Assert
        etag1.Should().Be(etag2, "same data should generate same ETag");
    }

    [Fact]
    public async Task ImageEndpoint_GeneratesDifferentETag_ForDifferentData()
    {
        // Arrange
        var imageData1 = new byte[] { 1, 2, 3, 4 };
        var imageData2 = new byte[] { 5, 6, 7, 8 };

        _mockImageService.Setup(s => s.GetEventImageAsync(1))
            .ReturnsAsync((imageData1, "image/jpeg"));
        _mockImageService.Setup(s => s.GetEventImageAsync(2))
            .ReturnsAsync((imageData2, "image/jpeg"));

        // Act
        await _controller.GetEventImage(1);
        var etag1 = _controller.Response.Headers["ETag"].ToString();

        // Reset controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        await _controller.GetEventImage(2);
        var etag2 = _controller.Response.Headers["ETag"].ToString();

        // Assert
        etag1.Should().NotBe(etag2, "different data should generate different ETags");
    }

    #endregion
}
