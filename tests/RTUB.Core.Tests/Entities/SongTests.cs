using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Song entity
/// </summary>
public class SongTests
{
    [Fact]
    public void Create_WithValidData_CreatesSong()
    {
        // Arrange
        var title = "Beautiful Song";
        var albumId = 1;
        var trackNumber = 5;

        // Act
        var result = Song.Create(title, albumId, trackNumber);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.AlbumId.Should().Be(albumId);
        result.TrackNumber.Should().Be(trackNumber);
    }

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var title = "";
        var albumId = 1;

        // Act & Assert
        var act = () => Song.Create(title, albumId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*título da música*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesSong()
    {
        // Arrange
        var song = Song.Create("Original Title", 1);
        var newTitle = "Updated Title";
        var trackNumber = 3;
        var lyricAuthor = "John Doe";
        var musicAuthor = "Jane Smith";
        var adaptation = "RTUB";
        var duration = 240;

        // Act
        song.UpdateDetails(newTitle, trackNumber, lyricAuthor, musicAuthor, adaptation, duration);

        // Assert
        song.Title.Should().Be(newTitle);
        song.TrackNumber.Should().Be(trackNumber);
        song.LyricAuthor.Should().Be(lyricAuthor);
        song.MusicAuthor.Should().Be(musicAuthor);
        song.Adaptation.Should().Be(adaptation);
        song.Duration.Should().Be(duration);
    }

    [Fact]
    public void SetLyrics_UpdatesLyrics()
    {
        // Arrange
        var song = Song.Create("Test Song", 1);
        var lyrics = "These are the lyrics...";

        // Act
        song.SetLyrics(lyrics);

        // Assert
        song.Lyrics.Should().Be(lyrics);
    }

    [Fact]
    public void SetSpotifyUrl_UpdatesUrl()
    {
        // Arrange
        var song = Song.Create("Test Song", 1);
        var spotifyUrl = "https://open.spotify.com/track/abc123";

        // Act
        song.SetSpotifyUrl(spotifyUrl);

        // Assert
        song.SpotifyUrl.Should().Be(spotifyUrl);
    }
}
