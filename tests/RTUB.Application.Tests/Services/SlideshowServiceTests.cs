using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

public class SlideshowServiceTests
{
    private readonly Mock<ISlideshowRepository> _mockSlideshowRepository;
    private readonly Mock<IImageStorageService> _imageStorageServiceMock;
    private readonly SlideshowService _service;

    public SlideshowServiceTests()
    {
        _mockSlideshowRepository = new Mock<ISlideshowRepository>();
        _imageStorageServiceMock = new Mock<IImageStorageService>();
        _service = new SlideshowService(_mockSlideshowRepository.Object, _imageStorageServiceMock.Object);
    }

    [Fact]
    public async Task CreateSlideshowAsync_WithValidData_CreatesSlideshow()
    {
        // Arrange
        var title = "Welcome Slideshow";
        var order = 1;
        var description = "Homepage slideshow";
        var intervalMs = 5000;
        var expectedSlideshow = Slideshow.Create(title, order, description, intervalMs);

        _mockSlideshowRepository.Setup(r => r.AddAsync(It.IsAny<Slideshow>()))
            .ReturnsAsync(expectedSlideshow);

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
        var slideshow = Slideshow.Create("Test", 1);
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);

        // Act
        var result = await _service.GetSlideshowByIdAsync(slideshow.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(slideshow.Id);
    }

    [Fact]
    public async Task GetSlideshowByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Slideshow?)null);

        // Act
        var result = await _service.GetSlideshowByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllSlideshowsAsync_ReturnsAllSlideshows()
    {
        // Arrange
        var slideshows = new List<Slideshow>
        {
            Slideshow.Create("Slide 1", 1),
            Slideshow.Create("Slide 2", 2),
            Slideshow.Create("Slide 3", 3)
        };
        _mockSlideshowRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(slideshows);

        // Act
        var result = (await _service.GetAllSlideshowsAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActiveSlideshowsAsync_ReturnsOnlyActiveSlideshows()
    {
        // Arrange
        var slide1 = Slideshow.Create("Active 1", 2);
        var slide3 = Slideshow.Create("Active 2", 3);
        var activeSlideshows = new List<Slideshow> { slide1, slide3 };

        _mockSlideshowRepository.Setup(r => r.GetActiveSlideshowsAsync())
            .ReturnsAsync(activeSlideshows);

        // Act
        var result = (await _service.GetActiveSlideshowsAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Id == slide1.Id);
        result.Should().Contain(s => s.Id == slide3.Id);
    }

    [Fact]
    public async Task GetActiveSlideshowsAsync_ReturnsInOrderByOrderProperty()
    {
        // Arrange
        var slideshows = new List<Slideshow>
        {
            Slideshow.Create("First", 1),
            Slideshow.Create("Second", 2),
            Slideshow.Create("Third", 3)
        };
        _mockSlideshowRepository.Setup(r => r.GetActiveSlideshowsAsync())
            .ReturnsAsync(slideshows);

        // Act
        var result = (await _service.GetActiveSlideshowsAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Order.Should().Be(1);
        result[1].Order.Should().Be(2);
        result[2].Order.Should().Be(3);
    }

    [Fact]
    public async Task GetActivePublicSlideshowsAsync_ReturnsOnlyActivePublicSlideshows()
    {
        // Arrange
        var slide1 = Slideshow.Create("Public 1", 1);
        var slide2 = Slideshow.Create("Public 2", 2);
        var publicSlideshows = new List<Slideshow> { slide1, slide2 };

        _mockSlideshowRepository.Setup(r => r.GetActivePublicSlideshowsAsync())
            .ReturnsAsync(publicSlideshows);

        // Act
        var result = (await _service.GetActivePublicSlideshowsAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Id == slide1.Id);
        result.Should().Contain(s => s.Id == slide2.Id);
    }

    [Fact]
    public async Task UpdateSlideshowAsync_WithValidData_UpdatesSlideshow()
    {
        // Arrange
        var slideshow = Slideshow.Create("Original", 1, "Old desc", 3000);
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);

        // Act
        await _service.UpdateSlideshowAsync(slideshow.Id, "Updated", "New desc", 2, 4000, true, false);

        // Assert
        slideshow.Title.Should().Be("Updated");
        slideshow.Description.Should().Be("New desc");
        slideshow.Order.Should().Be(2);
        slideshow.IntervalMs.Should().Be(4000);
        slideshow.IsExclusive.Should().BeFalse();
        _mockSlideshowRepository.Verify(r => r.UpdateAsync(slideshow), Times.Once);
    }

    [Fact]
    public async Task UpdateSlideshowAsync_WithIsExclusive_SetsIsExclusiveProperty()
    {
        // Arrange
        var slideshow = Slideshow.Create("Original", 1, "Old desc", 3000);
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);

        // Act
        await _service.UpdateSlideshowAsync(slideshow.Id, "Updated", "New desc", 2, 4000, true, true);

        // Assert
        slideshow.IsExclusive.Should().BeTrue();
        _mockSlideshowRepository.Verify(r => r.UpdateAsync(slideshow), Times.Once);
    }

    [Fact]
    public async Task UpdateSlideshowAsync_WithNonExistentId_ThrowsException()
    {
        // Arrange
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Slideshow?)null);

        // Act
        var act = async () => await _service.UpdateSlideshowAsync(999, "Test", "Test", 1, 5000, true, false);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Slideshow with ID 999 not found");
    }

    [Fact]
    public async Task UpdateSlideshowWithImageAsync_UpdatesSlideshowDetailsAndImage()
    {
        // Arrange
        var slideshow = Slideshow.Create("Original", 1, "Old desc", 3000);
        var imageUrl = "https://example.com/test-image.webp";

        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);
        _imageStorageServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _service.UpdateSlideshowWithImageAsync(slideshow.Id, "Updated", "New desc", 2, 4000, true, false, imageStream, "test.webp", "image/webp");

        // Assert
        slideshow.Title.Should().Be("Updated");
        slideshow.Description.Should().Be("New desc");
        slideshow.Order.Should().Be(2);
        slideshow.IntervalMs.Should().Be(4000);
        slideshow.IsExclusive.Should().BeFalse();
        slideshow.ImageUrl.Should().Be(imageUrl);

        // Verify image was uploaded with normalized title (not ID)
        _imageStorageServiceMock.Verify(
            x => x.UploadImageAsync(It.IsAny<Stream>(), "test.webp", "image/webp", "slideshows", "updated"),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSlideshowWithImageAsync_WithIsExclusive_SetsIsExclusiveProperty()
    {
        // Arrange
        var slideshow = Slideshow.Create("Original", 1, "Old desc", 3000);
        var imageUrl = "https://example.com/test-image.webp";

        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);
        _imageStorageServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _service.UpdateSlideshowWithImageAsync(slideshow.Id, "Updated", "New desc", 2, 4000, true, true, imageStream, "test.webp", "image/webp");

        // Assert
        slideshow.IsExclusive.Should().BeTrue();
        _mockSlideshowRepository.Verify(r => r.UpdateAsync(slideshow), Times.Once);
    }

    [Fact]
    public async Task UpdateSlideshowWithImageAsync_WithExistingImage_DeletesOldImage()
    {
        // Arrange
        var slideshow = Slideshow.Create("Test", 1);
        var oldImageUrl = "https://example.com/old-image.webp";
        var newImageUrl = "https://example.com/new-image.webp";

        // Set initial image
        slideshow.SetImage(oldImageUrl);

        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);
        _imageStorageServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(newImageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _service.UpdateSlideshowWithImageAsync(slideshow.Id, "New Title", "New desc", 2, 5000, true, false, imageStream, "test.webp", "image/webp");

        // Assert
        _imageStorageServiceMock.Verify(
            x => x.DeleteImageAsync(oldImageUrl),
            Times.Once,
            "Old image should be deleted");
    }

    [Fact]
    public async Task UpdateSlideshowWithImageAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Slideshow?)null);
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        var act = async () => await _service.UpdateSlideshowWithImageAsync(999, "Title", "Desc", 1, 5000, true, false, imageStream, "test.webp", "image/webp");
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ActivateSlideshowAsync_ActivatesSlideshow()
    {
        // Arrange
        var slideshow = Slideshow.Create("Test", 1);
        slideshow.Deactivate();
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);

        // Act
        await _service.ActivateSlideshowAsync(slideshow.Id);

        // Assert
        slideshow.IsActive.Should().BeTrue();
        _mockSlideshowRepository.Verify(r => r.UpdateAsync(slideshow), Times.Once);
    }

    [Fact]
    public async Task ActivateSlideshowAsync_WithNonExistentId_ThrowsException()
    {
        // Arrange
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Slideshow?)null);

        // Act
        var act = async () => await _service.ActivateSlideshowAsync(999);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Slideshow with ID 999 not found");
    }

    [Fact]
    public async Task DeactivateSlideshowAsync_DeactivatesSlideshow()
    {
        // Arrange
        var slideshow = Slideshow.Create("Test", 1);
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);

        // Act
        await _service.DeactivateSlideshowAsync(slideshow.Id);

        // Assert
        slideshow.IsActive.Should().BeFalse();
        _mockSlideshowRepository.Verify(r => r.UpdateAsync(slideshow), Times.Once);
    }

    [Fact]
    public async Task DeactivateSlideshowAsync_WithNonExistentId_ThrowsException()
    {
        // Arrange
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Slideshow?)null);

        // Act
        var act = async () => await _service.DeactivateSlideshowAsync(999);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Slideshow with ID 999 not found");
    }

    [Fact]
    public async Task DeleteSlideshowAsync_WithExistingId_DeletesSlideshow()
    {
        // Arrange
        var slideshow = Slideshow.Create("Test", 1);
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);

        // Act
        await _service.DeleteSlideshowAsync(slideshow.Id);

        // Assert
        _mockSlideshowRepository.Verify(r => r.DeleteAsync(It.IsAny<Slideshow>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSlideshowAsync_WithNonExistentId_ThrowsException()
    {
        // Arrange
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Slideshow?)null);

        // Act
        var act = async () => await _service.DeleteSlideshowAsync(999);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Slideshow with ID 999 not found");
    }

    [Fact]
    public async Task SetSlideshowImageAsync_SetsImageForSlideshow()
    {
        // Arrange
        var slideshow = Slideshow.Create("Test", 1);
        var imageUrl = "https://example.com/test-image.webp";

        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);
        _imageStorageServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(imageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _service.SetSlideshowImageAsync(slideshow.Id, imageStream, "test.webp", "image/webp");

        // Assert
        slideshow.ImageUrl.Should().Be(imageUrl);
        _mockSlideshowRepository.Verify(r => r.UpdateAsync(slideshow), Times.Once);
    }

    [Fact]
    public async Task SetSlideshowImageAsync_WithExistingImage_DeletesOldImage()
    {
        // Arrange
        var slideshow = Slideshow.Create("Test", 1);
        var oldImageUrl = "https://example.com/old-image.webp";
        var newImageUrl = "https://example.com/new-image.webp";

        slideshow.SetImage(oldImageUrl);

        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);
        _imageStorageServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(newImageUrl);

        // Act
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await _service.SetSlideshowImageAsync(slideshow.Id, imageStream, "test.webp", "image/webp");

        // Assert
        _imageStorageServiceMock.Verify(
            x => x.DeleteImageAsync(oldImageUrl),
            Times.Once,
            "Old image should be deleted");
        slideshow.ImageUrl.Should().Be(newImageUrl);
    }

    [Fact]
    public async Task SetSlideshowImageAsync_WithNonExistentId_ThrowsException()
    {
        // Arrange
        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Slideshow?)null);
        using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        // Act
        var act = async () => await _service.SetSlideshowImageAsync(999, imageStream, "test.webp", "image/webp");

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("Slideshow with ID 999 not found");
    }

    [Fact]
    public async Task DeleteSlideshowAsync_WithImage_DeletesImageFromStorage()
    {
        // Arrange
        var slideshow = Slideshow.Create("Test", 1);
        var imageUrl = "https://example.com/test-image.webp";
        slideshow.SetImage(imageUrl);

        _mockSlideshowRepository.Setup(r => r.GetByIdAsync(slideshow.Id))
            .ReturnsAsync(slideshow);

        // Act
        await _service.DeleteSlideshowAsync(slideshow.Id);

        // Assert
        _imageStorageServiceMock.Verify(
            x => x.DeleteImageAsync(imageUrl),
            Times.Once,
            "Image should be deleted from storage");
        _mockSlideshowRepository.Verify(r => r.DeleteAsync(slideshow), Times.Once);
    }
}
