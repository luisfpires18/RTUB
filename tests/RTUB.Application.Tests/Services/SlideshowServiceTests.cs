using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public class SlideshowServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IImageStorageService> _imageStorageServiceMock;
    private readonly SlideshowService _service;

    public SlideshowServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _imageStorageServiceMock = new Mock<IImageStorageService>();
        _service = new SlideshowService(_context, _imageStorageServiceMock.Object);
    }

    [Fact]
    public async Task CreateSlideshowAsync_WithValidData_CreatesSlideshow()
    {
        // Arrange
        var title = "Welcome Slideshow";
        var order = 1;
        var description = "Homepage slideshow";
        var intervalMs = 5000;

        // Act
        var slideshow = await _service.CreateSlideshowAsync(title, order, description, intervalMs);

        // Assert
        slideshow.Should().NotBeNull();
        slideshow.Title.Should().Be(title);
        slideshow.Order.Should().Be(order);
        slideshow.Description.Should().Be(description);
        slideshow.IntervalMs.Should().Be(intervalMs);
        slideshow.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetSlideshowByIdAsync_WithExistingId_ReturnsSlideshow()
    {
        // Arrange
        var slideshow = await _service.CreateSlideshowAsync("Test", 1);

        // Act
        var result = await _service.GetSlideshowByIdAsync(slideshow.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(slideshow.Id);
    }

    [Fact]
    public async Task GetSlideshowByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Act
        var result = await _service.GetSlideshowByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllSlideshowsAsync_ReturnsAllSlideshows()
    {
        // Arrange
        await _service.CreateSlideshowAsync("Slide 1", 1);
        await _service.CreateSlideshowAsync("Slide 2", 2);
        await _service.CreateSlideshowAsync("Slide 3", 3);

        // Act
        var slideshows = (await _service.GetAllSlideshowsAsync()).ToList();

        // Assert
        slideshows.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActiveSlideshowsAsync_ReturnsOnlyActiveSlideshows()
    {
        // Arrange
        var slide1 = await _service.CreateSlideshowAsync("Active 1", 2);
        var slide2 = await _service.CreateSlideshowAsync("Inactive", 1);
        var slide3 = await _service.CreateSlideshowAsync("Active 2", 3);
        
        await _service.DeactivateSlideshowAsync(slide2.Id);

        // Act
        var activeSlideshows = (await _service.GetActiveSlideshowsAsync()).ToList();

        // Assert
        activeSlideshows.Should().HaveCount(2);
        activeSlideshows.Should().Contain(s => s.Id == slide1.Id);
        activeSlideshows.Should().Contain(s => s.Id == slide3.Id);
        activeSlideshows.Should().NotContain(s => s.Id == slide2.Id);
    }

    [Fact]
    public async Task GetActiveSlideshowsAsync_ReturnsInOrderByOrderProperty()
    {
        // Arrange
        await _service.CreateSlideshowAsync("Third", 3);
        await _service.CreateSlideshowAsync("First", 1);
        await _service.CreateSlideshowAsync("Second", 2);

        // Act
        var slideshows = (await _service.GetActiveSlideshowsAsync()).ToList();

        // Assert
        slideshows.Should().HaveCount(3);
        slideshows[0].Order.Should().Be(1);
        slideshows[1].Order.Should().Be(2);
        slideshows[2].Order.Should().Be(3);
    }

    [Fact]
    public async Task UpdateSlideshowAsync_WithValidData_UpdatesSlideshow()
    {
        // Arrange
        var slideshow = await _service.CreateSlideshowAsync("Original", 1, "Old desc", 3000);

        // Act
        await _service.UpdateSlideshowAsync(slideshow.Id, "Updated", "New desc", 2, 4000);

        // Assert
        var updated = await _service.GetSlideshowByIdAsync(slideshow.Id);
        updated!.Title.Should().Be("Updated");
        updated.Description.Should().Be("New desc");
        updated.Order.Should().Be(2);
        updated.IntervalMs.Should().Be(4000);
    }

    [Fact]
    public async Task UpdateSlideshowAsync_WithNonExistentId_ThrowsException()
    {
        // Act
        var act = async () => await _service.UpdateSlideshowAsync(999, "Test", "Test", 1, 5000);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Slideshow with ID 999 not found");
    }



    [Fact]
    public async Task ActivateSlideshowAsync_ActivatesSlideshow()
    {
        // Arrange
        var slideshow = await _service.CreateSlideshowAsync("Test", 1);
        await _service.DeactivateSlideshowAsync(slideshow.Id);

        // Act
        await _service.ActivateSlideshowAsync(slideshow.Id);

        // Assert
        var activated = await _service.GetSlideshowByIdAsync(slideshow.Id);
        activated!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateSlideshowAsync_WithNonExistentId_ThrowsException()
    {
        // Act
        var act = async () => await _service.ActivateSlideshowAsync(999);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Slideshow with ID 999 not found");
    }

    [Fact]
    public async Task DeactivateSlideshowAsync_DeactivatesSlideshow()
    {
        // Arrange
        var slideshow = await _service.CreateSlideshowAsync("Test", 1);

        // Act
        await _service.DeactivateSlideshowAsync(slideshow.Id);

        // Assert
        var deactivated = await _service.GetSlideshowByIdAsync(slideshow.Id);
        deactivated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateSlideshowAsync_WithNonExistentId_ThrowsException()
    {
        // Act
        var act = async () => await _service.DeactivateSlideshowAsync(999);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Slideshow with ID 999 not found");
    }

    [Fact]
    public async Task DeleteSlideshowAsync_WithExistingId_DeletesSlideshow()
    {
        // Arrange
        var slideshow = await _service.CreateSlideshowAsync("Test", 1);

        // Act
        await _service.DeleteSlideshowAsync(slideshow.Id);

        // Assert
        var deleted = await _service.GetSlideshowByIdAsync(slideshow.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSlideshowAsync_WithNonExistentId_ThrowsException()
    {
        // Act
        var act = async () => await _service.DeleteSlideshowAsync(999);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Slideshow with ID 999 not found");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
