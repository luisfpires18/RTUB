using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class SongYouTubeUrlTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var songId = 1;
        var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        var description = "Original music video";

        // Act
        var songYouTubeUrl = SongYouTubeUrl.Create(songId, url, description);

        // Assert
        songYouTubeUrl.Should().NotBeNull();
        songYouTubeUrl.SongId.Should().Be(songId);
        songYouTubeUrl.Url.Should().Be(url);
        songYouTubeUrl.Description.Should().Be(description);
    }

    [Fact]
    public void Create_WithoutDescription_ShouldCreateInstance()
    {
        // Arrange
        var songId = 1;
        var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        // Act
        var songYouTubeUrl = SongYouTubeUrl.Create(songId, url);

        // Assert
        songYouTubeUrl.Should().NotBeNull();
        songYouTubeUrl.SongId.Should().Be(songId);
        songYouTubeUrl.Url.Should().Be(url);
        songYouTubeUrl.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyUrl_ShouldThrowException(string? emptyUrl)
    {
        // Arrange
        var songId = 1;

        // Act
        var act = () => SongYouTubeUrl.Create(songId, emptyUrl!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*O URL não pode estar vazio*");
    }

    [Fact]
    public void UpdateUrl_WithValidData_ShouldUpdateProperties()
    {
        // Arrange
        var songYouTubeUrl = SongYouTubeUrl.Create(1, "https://youtube.com/old");
        var newUrl = "https://youtube.com/new";
        var newDescription = "Updated video";

        // Act
        songYouTubeUrl.UpdateUrl(newUrl, newDescription);

        // Assert
        songYouTubeUrl.Url.Should().Be(newUrl);
        songYouTubeUrl.Description.Should().Be(newDescription);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateUrl_WithEmptyUrl_ShouldThrowException(string? emptyUrl)
    {
        // Arrange
        var songYouTubeUrl = SongYouTubeUrl.Create(1, "https://youtube.com/old");

        // Act
        var act = () => songYouTubeUrl.UpdateUrl(emptyUrl!, "desc");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*O URL não pode estar vazio*");
    }
}
