using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

public class ImageServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly IMemoryCache _cache;
    private readonly ImageService _service;

    public ImageServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        // Mock UserManager
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new ImageService(_context, _mockUserManager.Object, _cache);
    }

    [Fact]
    public async Task GetEventImageAsync_ReturnsNull_EventsUseS3Storage()
    {
        // Arrange
        var eventItem = Event.Create("Test Event", DateTime.Now, "Location", EventType.Festival);
        _context.Events.Add(eventItem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetEventImageAsync(eventItem.Id);

        // Assert - Events now use S3 storage, so this should return null
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEventImageAsync_ReturnsNull_WhenEventHasNoImage()
    {
        // Arrange
        var eventItem = Event.Create("Test Event", DateTime.Now, "Location", EventType.Festival);
        _context.Events.Add(eventItem);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetEventImageAsync(eventItem.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEventImageAsync_AlwaysReturnsNull_EventsUseS3()
    {
        // Arrange
        var eventItem = Event.Create("Test Event", DateTime.Now, "Location", EventType.Festival);
        _context.Events.Add(eventItem);
        await _context.SaveChangesAsync();

        // Act - First call
        await _service.GetEventImageAsync(eventItem.Id);
        
        // Second call should also return null
        var result = await _service.GetEventImageAsync(eventItem.Id);

        // Assert - Events use S3 storage now
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAlbumImageAsync_ReturnsImage_WhenAlbumHasImage()
    {
        // Arrange
        var album = Album.Create("Test Album", 2023);
        album.CoverImageData = new byte[] { 10, 11, 12 };
        album.CoverImageContentType = "image/png";
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAlbumImageAsync(album.Id);

        // Assert
        result.Should().NotBeNull();
        result.Value.Data.Should().BeEquivalentTo(new byte[] { 10, 11, 12 });
        result.Value.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task GetProfileImageAsync_ReturnsImage_WhenUserHasImage()
    {
        // Arrange
        var user = new ApplicationUser 
        { 
            Id = "user123",
            UserName = "testuser",
            ProfilePictureData = new byte[] { 13, 14, 15 },
            ProfilePictureContentType = "image/jpeg"
        };

        _mockUserManager.Setup(x => x.FindByIdAsync("user123"))
            .ReturnsAsync(user);

        // Act
        var result = await _service.GetProfileImageAsync("user123");

        // Assert
        result.Should().NotBeNull();
        result.Value.Data.Should().BeEquivalentTo(new byte[] { 13, 14, 15 });
        result.Value.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task GetProfileImageAsync_ReturnsNull_WhenUserNotFound()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync("nonexistent"))
            .ReturnsAsync((ApplicationUser)null);

        // Act
        var result = await _service.GetProfileImageAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInstrumentImageAsync_ReturnsImage_WhenInstrumentHasImage()
    {
        // Arrange
        var instrument = Instrument.Create("Strings", "Guitar", InstrumentCondition.Good);
        instrument.ImageData = new byte[] { 16, 17, 18 };
        instrument.ImageContentType = "image/png";
        _context.Instruments.Add(instrument);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetInstrumentImageAsync(instrument.Id);

        // Assert
        result.Should().NotBeNull();
        result.Value.Data.Should().BeEquivalentTo(new byte[] { 16, 17, 18 });
        result.Value.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task GetProductImageAsync_ReturnsImage_WhenProductHasImage()
    {
        // Arrange
        var product = Product.Create("T-Shirt", "Merchandise", 15.99m);
        product.ImageData = new byte[] { 19, 20, 21 };
        product.ImageContentType = "image/png";
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetProductImageAsync(product.Id);

        // Assert
        result.Should().NotBeNull();
        result.Value.Data.Should().BeEquivalentTo(new byte[] { 19, 20, 21 });
        result.Value.ContentType.Should().Be("image/png");
    }

    [Fact]
    public void InvalidateEventImageCache_RemovesCachedImage()
    {
        // Arrange
        var cacheKey = "event-image-1";
        _cache.Set(cacheKey, (new byte[] { 1, 2, 3 }, "image/png"));

        // Act
        _service.InvalidateEventImageCache(1);

        // Assert
        _cache.TryGetValue(cacheKey, out var _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateSlideshowImageCache_RemovesCachedImage()
    {
        // Arrange
        var cacheKey = "slideshow-image-2";
        _cache.Set(cacheKey, (new byte[] { 1, 2, 3 }, "image/png"));

        // Act
        _service.InvalidateSlideshowImageCache(2);

        // Assert
        _cache.TryGetValue(cacheKey, out var _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateAlbumImageCache_RemovesCachedImage()
    {
        // Arrange
        var cacheKey = "album-image-3";
        _cache.Set(cacheKey, (new byte[] { 1, 2, 3 }, "image/png"));

        // Act
        _service.InvalidateAlbumImageCache(3);

        // Assert
        _cache.TryGetValue(cacheKey, out var _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateProfileImageCache_RemovesCachedImage()
    {
        // Arrange
        var cacheKey = "profile-image-user123";
        _cache.Set(cacheKey, (new byte[] { 1, 2, 3 }, "image/png"));

        // Act
        _service.InvalidateProfileImageCache("user123");

        // Assert
        _cache.TryGetValue(cacheKey, out var _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateInstrumentImageCache_RemovesCachedImage()
    {
        // Arrange
        var cacheKey = "instrument-image-4";
        _cache.Set(cacheKey, (new byte[] { 1, 2, 3 }, "image/png"));

        // Act
        _service.InvalidateInstrumentImageCache(4);

        // Assert
        _cache.TryGetValue(cacheKey, out var _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateProductImageCache_RemovesCachedImage()
    {
        // Arrange
        var cacheKey = "product-image-5";
        _cache.Set(cacheKey, (new byte[] { 1, 2, 3 }, "image/png"));

        // Act
        _service.InvalidateProductImageCache(5);

        // Assert
        _cache.TryGetValue(cacheKey, out var _).Should().BeFalse();
    }

    public void Dispose()
    {
        _context?.Dispose();
        _cache?.Dispose();
    }
}
