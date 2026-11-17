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
}
