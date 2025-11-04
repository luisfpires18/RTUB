using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Album entity
/// </summary>
public class AlbumTests
{
    [Fact]
    public void Create_WithValidData_CreatesAlbum()
    {
        // Arrange
        var title = "Greatest Hits";
        var year = 2020;
        var description = "Our best songs";

        // Act
        var result = Album.Create(title, year, description);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.Year.Should().Be(year);
        result.Description.Should().Be(description);
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var title = "";
        var year = 2020;

        // Act & Assert
        var act = () => Album.Create(title, year);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*título*");
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2100)]
    public void Create_WithInvalidYear_ThrowsArgumentException(int year)
    {
        // Arrange
        var title = "Test Album";

        // Act & Assert
        var act = () => Album.Create(title, year);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*inválido*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesAlbum()
    {
        // Arrange
        var album = Album.Create("Original Title", 2020);
        var newTitle = "Updated Title";
        var newYear = 2021;
        var newDescription = "Updated description";

        // Act
        album.UpdateDetails(newTitle, newYear, newDescription);

        // Assert
        album.Title.Should().Be(newTitle);
        album.Year.Should().Be(newYear);
        album.Description.Should().Be(newDescription);
    }

    [Fact]
    public void SetCoverImage_SetsImageCorrectly()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        var imageData = new byte[] { 1, 2, 3 };
        var contentType = "image/jpeg";
        var url = "https://example.com/cover.jpg";

        // Act
        album.SetCoverImage(imageData, contentType, url);

        // Assert
        album.CoverImageData.Should().BeEquivalentTo(imageData);
        album.CoverImageContentType.Should().Be(contentType);
        album.CoverImageUrl.Should().Be(url);
    }

    [Fact]
    public void GetCoverImageSource_WithImageData_ReturnsApiPath()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        album.Id = 123;
        album.SetCoverImage(new byte[] { 1, 2, 3 }, "image/jpeg");

        // Act
        var result = album.GetCoverImageSource();

        // Assert
        result.Should().Be("/api/images/album/123");
    }

    [Fact]
    public void GetCoverImageSource_WithUrl_ReturnsUrl()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        var url = "https://example.com/cover.jpg";
        album.SetCoverImage(null, null, url);

        // Act
        var result = album.GetCoverImageSource();

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public void GetCoverImageSource_WithNoImage_ReturnsEmpty()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);

        // Act
        var result = album.GetCoverImageSource();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CoverImageSrc_UsesGetCoverImageSource()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        album.Id = 456;
        album.SetCoverImage(new byte[] { 1, 2, 3 }, "image/png");

        // Act & Assert
        album.CoverImageSrc.Should().Be(album.GetCoverImageSource());
    }
}
