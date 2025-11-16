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
        album.UpdateDetails(newTitle, newYear, newDescription, false);

        // Assert
        album.Title.Should().Be(newTitle);
        album.Year.Should().Be(newYear);
        album.Description.Should().Be(newDescription);
    }

    [Fact]
    public void SetCoverImage_SetsImageUrlCorrectly()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        var url = "https://example.com/cover.jpg";

        // Act
        album.SetCoverImage(url);

        // Assert
        album.ImageUrl.Should().Be(url);
    }

    [Fact]
    public void GetCoverImageSource_WithUrl_ReturnsUrl()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        var url = "https://example.com/cover.jpg";
        album.SetCoverImage(url);

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
        var url = "https://example.com/cover.jpg";
        album.SetCoverImage(url);

        // Act & Assert
        album.CoverImageSrc.Should().Be(album.GetCoverImageSource());
    }

    [Fact]
    public void Create_WithIsPrivateTrue_CreatesPrivateAlbum()
    {
        // Arrange
        var title = "Private Album";
        var year = 2020;
        var description = "This is private";
        var isPrivate = true;

        // Act
        var result = Album.Create(title, year, description, isPrivate);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.IsPrivate.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutIsPrivate_CreatesPublicAlbum()
    {
        // Arrange
        var title = "Public Album";
        var year = 2020;

        // Act
        var result = Album.Create(title, year);

        // Assert
        result.Should().NotBeNull();
        result.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_WithIsPrivate_UpdatesPrivacySetting()
    {
        // Arrange
        var album = Album.Create("Test Album", 2020);
        album.IsPrivate.Should().BeFalse();

        // Act
        album.UpdateDetails("Updated Title", 2021, "Updated desc", true);

        // Assert
        album.IsPrivate.Should().BeTrue();
        album.Title.Should().Be("Updated Title");
    }

    [Fact]
    public void UpdateDetails_ChangingFromPrivateToPublic_UpdatesCorrectly()
    {
        // Arrange
        var album = Album.Create("Private Album", 2020, "Description", true);
        album.IsPrivate.Should().BeTrue();

        // Act
        album.UpdateDetails("Public Album", 2021, "Now public", false);

        // Assert
        album.IsPrivate.Should().BeFalse();
        album.Title.Should().Be("Public Album");
    }
}
