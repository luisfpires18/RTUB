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
/// Tests for avatar optimization features in ImagesController
/// Validates cache-busting, immutable caching, and conditional requests
/// </summary>
public class AvatarOptimizationTests
{
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ImagesController _controller;

    public AvatarOptimizationTests()
    {
        _mockImageService = new Mock<IImageService>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Setup configuration
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

    #region Cache-Control Headers

    [Fact]
    public async Task GetProfileImage_WithVersionParameter_ReturnsImmutableCacheControl()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3, 4 };
        var contentType = "image/jpeg";
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, contentType));

        // Add version query parameter to simulate versioned URL
        _controller.Request.QueryString = new QueryString("?v=1");

        // Act
        var result = await _controller.GetProfileImage(userId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        _controller.Response.Headers["Cache-Control"].ToString()
            .Should().Contain("public")
            .And.Contain("max-age=31536000")
            .And.Contain("immutable");
    }

    [Fact]
    public async Task GetProfileImage_WithoutVersionParameter_ReturnsNoCacheCacheControl()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3, 4 };
        var contentType = "image/jpeg";
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, contentType));

        // No version parameter
        _controller.Request.QueryString = QueryString.Empty;

        // Act
        var result = await _controller.GetProfileImage(userId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        _controller.Response.Headers["Cache-Control"].ToString()
            .Should().Contain("public")
            .And.Contain("no-cache");
    }

    #endregion

    #region ETag Support

    [Fact]
    public async Task GetProfileImage_IncludesETagHeader()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3, 4 };
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // Act
        await _controller.GetProfileImage(userId);

        // Assert
        _controller.Response.Headers.Should().ContainKey("ETag");
        _controller.Response.Headers["ETag"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetProfileImage_WithMatchingETag_Returns304()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3, 4 };
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // First request to get ETag
        await _controller.GetProfileImage(userId);
        var etag = _controller.Response.Headers["ETag"].ToString();

        // Reset controller for second request
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.Request.Headers["If-None-Match"] = etag;

        // Act
        var result = await _controller.GetProfileImage(userId);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        (result as StatusCodeResult)!.StatusCode.Should().Be(304);
    }

    #endregion

    #region Last-Modified Support

    [Fact]
    public async Task GetProfileImage_IncludesLastModifiedHeader()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3, 4 };
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // Act
        await _controller.GetProfileImage(userId);

        // Assert
        _controller.Response.Headers.Should().ContainKey("Last-Modified");
        _controller.Response.Headers["Last-Modified"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetProfileImage_WithIfModifiedSince_Returns304WhenNotModified()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3, 4 };
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // First request to get Last-Modified
        await _controller.GetProfileImage(userId);
        var lastModified = _controller.Response.Headers["Last-Modified"].ToString();

        // Reset controller for conditional request
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.Request.Headers["If-Modified-Since"] = lastModified;

        // Act
        var result = await _controller.GetProfileImage(userId);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        (result as StatusCodeResult)!.StatusCode.Should().Be(304);
    }

    #endregion

    #region Consistent ETags

    [Fact]
    public async Task GetProfileImage_GeneratesConsistentETag_ForSameImage()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3, 4, 5 };
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // First request
        await _controller.GetProfileImage(userId);
        var etag1 = _controller.Response.Headers["ETag"].ToString();

        // Reset controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Second request
        await _controller.GetProfileImage(userId);
        var etag2 = _controller.Response.Headers["ETag"].ToString();

        // Assert
        etag1.Should().Be(etag2, "same image data should produce consistent ETags");
        etag1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetProfileImage_GeneratesDifferentETag_WhenImageChanges()
    {
        // Arrange
        var userId = "user123";
        var imageData1 = new byte[] { 1, 2, 3, 4, 5 };
        var imageData2 = new byte[] { 6, 7, 8, 9, 10 };
        
        _mockImageService.SetupSequence(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData1, "image/jpeg"))
            .ReturnsAsync((imageData2, "image/jpeg"));

        // First request with original image
        await _controller.GetProfileImage(userId);
        var etag1 = _controller.Response.Headers["ETag"].ToString();

        // Reset controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Second request with updated image
        await _controller.GetProfileImage(userId);
        var etag2 = _controller.Response.Headers["ETag"].ToString();

        // Assert
        etag1.Should().NotBe(etag2, "different image data should produce different ETags");
    }

    #endregion

    #region Version-Based Cache Busting

    [Fact]
    public async Task GetProfileImage_VersionParameter_EnablesImmutableCaching()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3 };
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, "image/jpeg"));

        _controller.Request.QueryString = new QueryString("?v=2");

        // Act
        await _controller.GetProfileImage(userId);

        // Assert
        var cacheControl = _controller.Response.Headers["Cache-Control"].ToString();
        cacheControl.Should().Contain("immutable");
        cacheControl.Should().Contain("max-age=31536000"); // 1 year in seconds
    }

    [Fact]
    public async Task GetProfileImage_DifferentVersions_TreatedAsDifferentResources()
    {
        // This test verifies that version parameter enables aggressive caching
        // Different versions should be cached separately by the browser
        
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3 };
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // Version 1
        _controller.Request.QueryString = new QueryString("?v=1");
        await _controller.GetProfileImage(userId);
        var cacheControl1 = _controller.Response.Headers["Cache-Control"].ToString();

        // Reset
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Version 2
        _controller.Request.QueryString = new QueryString("?v=2");
        await _controller.GetProfileImage(userId);
        var cacheControl2 = _controller.Response.Headers["Cache-Control"].ToString();

        // Assert both use immutable caching
        cacheControl1.Should().Contain("immutable");
        cacheControl2.Should().Contain("immutable");
    }

    #endregion

    #region Bandwidth Optimization

    [Fact]
    public async Task GetProfileImage_304Response_SavesBandwidth()
    {
        // Arrange
        var userId = "user123";
        var imageData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // 10 bytes
        
        _mockImageService.Setup(s => s.GetProfileImageAsync(userId))
            .ReturnsAsync((imageData, "image/jpeg"));

        // First request
        var firstResult = await _controller.GetProfileImage(userId);
        var etag = _controller.Response.Headers["ETag"].ToString();

        // Reset for conditional request
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.Request.Headers["If-None-Match"] = etag;

        // Act - Second request with matching ETag
        var result = await _controller.GetProfileImage(userId);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        (result as StatusCodeResult)!.StatusCode.Should().Be(304);
        
        // 304 response has no body, saving bandwidth
        firstResult.Should().BeOfType<FileContentResult>();
        (firstResult as FileContentResult)!.FileContents.Length.Should().Be(10);
    }

    #endregion
}
