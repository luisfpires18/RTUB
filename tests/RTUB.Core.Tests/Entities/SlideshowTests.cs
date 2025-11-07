using FluentAssertions;
using RTUB.Core.Entities;
using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Tests.Entities;

public class SlideshowTests
{
    [Fact]
    public void Create_WithValidData_CreatesSlideshow()
    {
        // Arrange
        var title = "Test Slideshow";
        var order = 1;
        var description = "Test description";
        var intervalMs = 5000;

        // Act
        var slideshow = Slideshow.Create(title, order, description, intervalMs);

        // Assert
        slideshow.Should().NotBeNull();
        slideshow.Title.Should().Be(title);
        slideshow.Order.Should().Be(order);
        slideshow.Description.Should().Be(description);
        slideshow.IntervalMs.Should().Be(intervalMs);
        slideshow.IsActive.Should().BeTrue(); // Default is true
    }

    [Fact]
    public void Create_WithMinimalData_CreatesSlideshow()
    {
        // Act
        var slideshow = Slideshow.Create("Title", 1);

        // Assert
        slideshow.Title.Should().Be("Title");
        slideshow.Order.Should().Be(1);
        slideshow.Description.Should().BeEmpty();
        slideshow.IntervalMs.Should().Be(5000); // Default
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Act
        var act = () => Slideshow.Create("", 1);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Title cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceTitle_ThrowsArgumentException()
    {
        // Act
        var act = () => Slideshow.Create("   ", 1);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Title cannot be empty*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Create_WithInvalidOrder_ThrowsArgumentException(int order)
    {
        // Act
        var act = () => Slideshow.Create("Title", order);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Order must be positive*");
    }

    [Theory]
    [InlineData(999)]
    [InlineData(0)]
    [InlineData(10001)]
    [InlineData(20000)]
    public void Create_WithInvalidInterval_ThrowsArgumentException(int interval)
    {
        // Act
        var act = () => Slideshow.Create("Title", 1, "", interval);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Interval must be between 1000ms and 10000ms*");
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10000)]
    public void Create_WithValidInterval_CreatesSlideshow(int interval)
    {
        // Act
        var slideshow = Slideshow.Create("Title", 1, "", interval);

        // Assert
        slideshow.IntervalMs.Should().Be(interval);
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesSlideshow()
    {
        // Arrange
        var slideshow = Slideshow.Create("Original", 1);
        var newTitle = "Updated Title";
        var newDescription = "Updated Description";
        var newOrder = 5;
        var newInterval = 7000;

        // Act
        slideshow.UpdateDetails(newTitle, newDescription, newOrder, newInterval);

        // Assert
        slideshow.Title.Should().Be(newTitle);
        slideshow.Description.Should().Be(newDescription);
        slideshow.Order.Should().Be(newOrder);
        slideshow.IntervalMs.Should().Be(newInterval);
    }

    [Fact]
    public void UpdateDetails_WithInvalidData_ThrowsArgumentException()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);

        // Act
        var act = () => slideshow.UpdateDetails("", "Description", 1, 5000);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetImage_WithValidData_SetsImage()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);
        var url = "http://example.com/image.jpg";

        // Act
        slideshow.SetImage(url);

        // Assert
        slideshow.ImageUrl.Should().Be(url);
    }

    [Fact]
    public void SetImage_WithEmptyUrl_SetsEmptyUrl()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);
        slideshow.SetImage("http://test.com/image.jpg");

        // Act
        slideshow.SetImage("");

        // Assert
        slideshow.ImageUrl.Should().BeEmpty();
    }

    [Fact]
    public void Activate_SetsIsActiveToTrue()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);
        slideshow.Deactivate();

        // Act
        slideshow.Activate();

        // Assert
        slideshow.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);

        // Act
        slideshow.Deactivate();

        // Assert
        slideshow.IsActive.Should().BeFalse();
    }

    [Fact]
    public void GetImageSource_WithImageUrl_ReturnsImageUrl()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);
        var url = "http://example.com/image.jpg";
        slideshow.SetImage(url);

        // Act
        var source = slideshow.GetImageSource();

        // Assert
        source.Should().Be(url);
    }

    [Fact]
    public void GetImageSource_WithoutImage_ReturnsEmptyString()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);

        // Act
        var source = slideshow.GetImageSource();

        // Assert
        source.Should().BeEmpty();
    }

    [Fact]
    public void ImageSrc_Property_ReturnsGetImageSource()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);
        slideshow.SetImage("http://example.com/image.jpg");

        // Act
        var source = slideshow.ImageSrc;

        // Assert
        source.Should().Be("http://example.com/image.jpg");
    }

    [Fact]
    public void Validate_WithoutImage_ReturnsNoErrors()
    {
        // Arrange - Image URL is now optional to allow creating slideshows without images
        var slideshow = Slideshow.Create("Title", 1);
        var validationContext = new ValidationContext(slideshow);

        // Act
        var results = slideshow.Validate(validationContext).ToList();

        // Assert - No validation errors, images can be uploaded later via admin interface
        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithImageUrl_ReturnsNoErrors()
    {
        // Arrange
        var slideshow = Slideshow.Create("Title", 1);
        slideshow.SetImage("http://example.com/image.jpg");
        var validationContext = new ValidationContext(slideshow);

        // Act
        var results = slideshow.Validate(validationContext).ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Slideshow_InheritsFromBaseEntity()
    {
        // Arrange & Act
        var slideshow = Slideshow.Create("Title", 1);

        // Assert
        slideshow.Should().BeAssignableTo<BaseEntity>();
    }

    [Fact]
    public void Slideshow_ImplementsIValidatableObject()
    {
        // Arrange & Act
        var slideshow = Slideshow.Create("Title", 1);

        // Assert
        slideshow.Should().BeAssignableTo<IValidatableObject>();
    }
}
