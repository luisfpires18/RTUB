using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace RTUB.Integration.Tests.Api;

/// <summary>
/// Integration tests for Slideshow functionality with IDrive S3 storage
/// </summary>
public class SlideshowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SlideshowIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Slideshow Image API Tests

    [Fact]
    public async Task SlideshowImageApi_WithInvalidId_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/images/slideshow/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SlideshowImageApi_WithValidIdButNoImage_Returns404()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var slideshowService = scope.ServiceProvider.GetRequiredService<ISlideshowService>();
        
        var createdSlideshow = await slideshowService.CreateSlideshowAsync(
            "Test Slideshow Without Image",
            1,
            "Integration test",
            5000);

        try
        {
            // Act
            var response = await _client.GetAsync($"/api/images/slideshow/{createdSlideshow.Id}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            // Cleanup
            await slideshowService.DeleteSlideshowAsync(createdSlideshow.Id);
        }
    }

    [Fact]
    public async Task SlideshowImageApi_EndpointRemoved_Returns404()
    {
        // Arrange - The /api/images/slideshow/{id} endpoint has been removed
        // as all slideshow images are now stored in IDrive S3, not in the database
        
        // Act
        var response = await _client.GetAsync($"/api/images/slideshow/1");

        // Assert - Should return 404 as the endpoint no longer exists
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Slideshow Service Integration Tests

    [Fact]
    public async Task SlideshowService_CreateAndRetrieve_WorksCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var slideshowService = scope.ServiceProvider.GetRequiredService<ISlideshowService>();

        try
        {
            // Act
            var created = await slideshowService.CreateSlideshowAsync(
                "Integration Test Slideshow",
                10,
                "Test Description",
                3000);
            var retrieved = await slideshowService.GetSlideshowByIdAsync(created.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved!.Title.Should().Be("Integration Test Slideshow");
            retrieved.Description.Should().Be("Test Description");
            retrieved.Order.Should().Be(10);
            retrieved.IntervalMs.Should().Be(3000);
            retrieved.IsActive.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            var all = await slideshowService.GetAllSlideshowsAsync();
            var toDelete = all.FirstOrDefault(s => s.Title == "Integration Test Slideshow");
            if (toDelete != null)
            {
                await slideshowService.DeleteSlideshowAsync(toDelete.Id);
            }
        }
    }

    [Fact]
    public async Task SlideshowService_ActivateDeactivate_WorksCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var slideshowService = scope.ServiceProvider.GetRequiredService<ISlideshowService>();

        try
        {
            var created = await slideshowService.CreateSlideshowAsync(
                "Activation Test Slideshow",
                1,
                "Test",
                5000);

            // Act - Activate
            await slideshowService.ActivateSlideshowAsync(created.Id);
            var afterActivation = await slideshowService.GetSlideshowByIdAsync(created.Id);

            // Assert - Activated
            afterActivation!.IsActive.Should().BeTrue();

            // Act - Deactivate
            await slideshowService.DeactivateSlideshowAsync(created.Id);
            var afterDeactivation = await slideshowService.GetSlideshowByIdAsync(created.Id);

            // Assert - Deactivated
            afterDeactivation!.IsActive.Should().BeFalse();
        }
        finally
        {
            // Cleanup
            var all = await slideshowService.GetAllSlideshowsAsync();
            var toDelete = all.FirstOrDefault(s => s.Title == "Activation Test Slideshow");
            if (toDelete != null)
            {
                await slideshowService.DeleteSlideshowAsync(toDelete.Id);
            }
        }
    }

    [Fact]
    public async Task SlideshowService_GetActiveSlideshows_ReturnsOnlyActive()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var slideshowService = scope.ServiceProvider.GetRequiredService<ISlideshowService>();

        try
        {
            var createdActive = await slideshowService.CreateSlideshowAsync(
                "Active Slideshow Test",
                1,
                "Should be returned",
                5000);
            
            var createdInactive = await slideshowService.CreateSlideshowAsync(
                "Inactive Slideshow Test",
                2,
                "Should not be returned",
                5000);
            
            // Deactivate the second one
            await slideshowService.DeactivateSlideshowAsync(createdInactive.Id);

            // Act
            var activeSlideshows = await slideshowService.GetActiveSlideshowsAsync();

            // Assert
            activeSlideshows.Should().Contain(s => s.Id == createdActive.Id);
            activeSlideshows.Should().NotContain(s => s.Id == createdInactive.Id);
        }
        finally
        {
            // Cleanup
            var all = await slideshowService.GetAllSlideshowsAsync();
            foreach (var s in all.Where(s => s.Title.Contains("Slideshow Test")))
            {
                await slideshowService.DeleteSlideshowAsync(s.Id);
            }
        }
    }

    [Fact]
    public async Task SlideshowService_GetActiveSlideshowsWithUrls_GeneratesUrls()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var slideshowService = scope.ServiceProvider.GetRequiredService<ISlideshowService>();

        try
        {
            var created = await slideshowService.CreateSlideshowAsync(
                "URL Generation Test",
                1,
                "Test",
                5000);

            // Create a minimal valid PNG image for testing
            byte[] testImageData = CreateTestPngImage();
            
            await slideshowService.SetSlideshowImageAsync(
                created.Id, 
                testImageData, 
                "image/png", 
                null);

            // Act
            var slideshowsWithUrls = await slideshowService.GetActiveSlideshowsWithUrlsAsync();
            var testSlideshow = slideshowsWithUrls.FirstOrDefault(s => s.Id == created.Id);

            // Assert
            testSlideshow.Should().NotBeNull();
            testSlideshow!.ImageSrc.Should().NotBeNullOrEmpty();
            // Should either be an API path or an S3 URL
            (testSlideshow.ImageSrc.StartsWith("/api/images/") || 
             testSlideshow.ImageSrc.StartsWith("http")).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            var all = await slideshowService.GetAllSlideshowsAsync();
            var toDelete = all.FirstOrDefault(s => s.Title == "URL Generation Test");
            if (toDelete != null)
            {
                await slideshowService.DeleteSlideshowAsync(toDelete.Id);
            }
        }
    }

    [Fact]
    public async Task SlideshowService_GetAllSlideshows_GeneratesPresignedUrls()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var slideshowService = scope.ServiceProvider.GetRequiredService<ISlideshowService>();

        try
        {
            var created = await slideshowService.CreateSlideshowAsync(
                "Pre-signed URL Test",
                1,
                "Test",
                5000);

            // Create a test image
            byte[] testImageData = CreateTestPngImage();
            
            await slideshowService.SetSlideshowImageAsync(
                created.Id, 
                testImageData, 
                "image/png", 
                null);

            // Act
            var allSlideshows = await slideshowService.GetAllSlideshowsAsync();
            var testSlideshow = allSlideshows.FirstOrDefault(s => s.Id == created.Id);

            // Assert
            testSlideshow.Should().NotBeNull();
            testSlideshow!.ImageSrc.Should().NotBeNullOrEmpty();
        }
        finally
        {
            // Cleanup
            var all = await slideshowService.GetAllSlideshowsAsync();
            var toDelete = all.FirstOrDefault(s => s.Title == "Pre-signed URL Test");
            if (toDelete != null)
            {
                await slideshowService.DeleteSlideshowAsync(toDelete.Id);
            }
        }
    }

    [Fact]
    public async Task SlideshowService_UpdateSlideshow_PreservesImage()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var slideshowService = scope.ServiceProvider.GetRequiredService<ISlideshowService>();

        try
        {
            var created = await slideshowService.CreateSlideshowAsync(
                "Update Test Slideshow",
                1,
                "Original Description",
                5000);
            
            byte[] testImageData = CreateTestPngImage();
            await slideshowService.SetSlideshowImageAsync(
                created.Id, 
                testImageData, 
                "image/png", 
                null);

            // Get the image URL before update
            var beforeUpdate = await slideshowService.GetSlideshowByIdAsync(created.Id);
            var imageUrlBeforeUpdate = beforeUpdate!.ImageUrl;

            // Act - Update title and description but not image
            await slideshowService.UpdateSlideshowAsync(
                created.Id,
                "Updated Title",
                "Updated Description",
                created.Order,
                created.IntervalMs);

            // Assert
            var afterUpdate = await slideshowService.GetSlideshowByIdAsync(created.Id);
            afterUpdate!.Title.Should().Be("Updated Title");
            afterUpdate.Description.Should().Be("Updated Description");
            afterUpdate.ImageUrl.Should().Be(imageUrlBeforeUpdate); // Image should be preserved
        }
        finally
        {
            // Cleanup
            var all = await slideshowService.GetAllSlideshowsAsync();
            var toDelete = all.FirstOrDefault(s => s.Title == "Updated Title" || s.Title == "Update Test Slideshow");
            if (toDelete != null)
            {
                await slideshowService.DeleteSlideshowAsync(toDelete.Id);
            }
        }
    }

    #endregion

    #region IDrive S3 Storage Service Tests

    [Fact]
    public void SlideshowStorageService_IsRegistered()
    {
        // Arrange & Act
        using var scope = _factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetService<ISlideshowStorageService>();

        // Assert
        storageService.Should().NotBeNull("ISlideshowStorageService should be registered in DI container");
    }

    [Fact]
    public async Task SlideshowStorageService_GetImageUrl_WithNullFilename_ReturnsNull()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<ISlideshowStorageService>();

        // Act
        var url = await storageService.GetImageUrlAsync(null!);

        // Assert
        url.Should().BeNull();
    }

    [Fact]
    public async Task SlideshowStorageService_GetImageUrl_WithEmptyFilename_ReturnsNull()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<ISlideshowStorageService>();

        // Act
        var url = await storageService.GetImageUrlAsync(string.Empty);

        // Assert
        url.Should().BeNull();
    }

    [Fact]
    public async Task SlideshowStorageService_GetImageUrl_WithValidFilename_ReturnsUrl()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<ISlideshowStorageService>();

        // Act
        var url = await storageService.GetImageUrlAsync("slideshow_1_12345.webp");

        // Assert
        url.Should().NotBeNull();
        url.Should().StartWith("https://");
        url.Should().Contain("slideshow_1_12345.webp");
    }

    [Fact]
    public async Task SlideshowStorageService_DeleteImage_WithNullFilename_ReturnsFalse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<ISlideshowStorageService>();

        // Act
        var result = await storageService.DeleteImageAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SlideshowStorageService_DeleteImage_WithEmptyFilename_ReturnsFalse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<ISlideshowStorageService>();

        // Act
        var result = await storageService.DeleteImageAsync(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a minimal valid 1x1 red image for testing using ImageSharp
    /// </summary>
    private static byte[] CreateTestPngImage()
    {
        using var image = new Image<Rgba32>(1, 1);
        image[0, 0] = Color.Red;
        
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    #endregion
}
