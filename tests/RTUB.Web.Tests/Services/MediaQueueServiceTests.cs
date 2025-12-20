using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Web.Services;

namespace RTUB.Web.Tests.Services;

/// <summary>
/// Tests for MediaQueueService - validates PWA media playback queue logic
/// Covers Next/Previous navigation and 3-second restart rule
/// </summary>
public class MediaQueueServiceTests
{
    private readonly MediaQueueService _service;

    public MediaQueueServiceTests()
    {
        _service = new MediaQueueService();
    }

    [Fact]
    public void BuildQueue_ShouldOrderSongsByTrackNumber()
    {
        // Arrange
        var songs = new List<Song>
        {
            new() { Id = 1, Title = "Song C", TrackNumber = 3 },
            new() { Id = 2, Title = "Song A", TrackNumber = 1 },
            new() { Id = 3, Title = "Song B", TrackNumber = 2 }
        };
        var albumTitle = "Test Album";

        // Act
        var queue = _service.BuildQueue(songs, albumTitle);

        // Assert
        queue.Should().HaveCount(3);
        queue[0].Title.Should().Be("Song A");
        queue[1].Title.Should().Be("Song B");
        queue[2].Title.Should().Be("Song C");
        queue[0].TrackNumber.Should().Be(1);
        queue[1].TrackNumber.Should().Be(2);
        queue[2].TrackNumber.Should().Be(3);
    }

    [Fact]
    public void BuildQueue_ShouldHandleNullTrackNumbers()
    {
        // Arrange
        var songs = new List<Song>
        {
            new() { Id = 1, Title = "Song A", TrackNumber = 1 },
            new() { Id = 2, Title = "Song Without Track", TrackNumber = null },
            new() { Id = 3, Title = "Song B", TrackNumber = 2 }
        };
        var albumTitle = "Test Album";

        // Act
        var queue = _service.BuildQueue(songs, albumTitle);

        // Assert
        queue.Should().HaveCount(3);
        // Songs with track numbers should come first
        queue[0].Title.Should().Be("Song A");
        queue[1].Title.Should().Be("Song B");
        // Null track numbers should come last
        queue[2].Title.Should().Be("Song Without Track");
    }

    [Fact]
    public void BuildQueue_ShouldSetAlbumTitleForAllTracks()
    {
        // Arrange
        var songs = new List<Song>
        {
            new() { Id = 1, Title = "Song 1", TrackNumber = 1 },
            new() { Id = 2, Title = "Song 2", TrackNumber = 2 }
        };
        var albumTitle = "Greatest Hits";

        // Act
        var queue = _service.BuildQueue(songs, albumTitle);

        // Assert
        queue.Should().AllSatisfy(track => track.Album.Should().Be("Greatest Hits"));
    }

    [Fact]
    public void GetNextTrack_ShouldReturnNextTrackWhenNotAtEnd()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 1, Title = "Track 1" },
            new() { Id = 2, Title = "Track 2" },
            new() { Id = 3, Title = "Track 3" }
        };

        // Act
        var nextTrack = _service.GetNextTrack(queue, 1);

        // Assert
        nextTrack.Should().NotBeNull();
        nextTrack!.Id.Should().Be(3);
        nextTrack.Title.Should().Be("Track 3");
    }

    [Fact]
    public void GetNextTrack_ShouldReturnNullWhenAtEnd()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 1, Title = "Track 1" },
            new() { Id = 2, Title = "Track 2" }
        };

        // Act
        var nextTrack = _service.GetNextTrack(queue, 1);

        // Assert
        nextTrack.Should().BeNull();
    }

    [Fact]
    public void GetPreviousTrackOrRestart_ShouldRestartWhenOverThreshold()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 1, Title = "Track 1" },
            new() { Id = 2, Title = "Track 2" },
            new() { Id = 3, Title = "Track 3" }
        };
        var currentIndex = 1;
        var currentTime = 5.0; // Over 3 second threshold

        // Act
        var (track, shouldRestart) = _service.GetPreviousTrackOrRestart(queue, currentIndex, currentTime);

        // Assert
        shouldRestart.Should().BeTrue();
        track.Should().NotBeNull();
        track!.Id.Should().Be(2); // Should return current track
        track.Title.Should().Be("Track 2");
    }

    [Fact]
    public void GetPreviousTrackOrRestart_ShouldGoToPreviousWhenUnderThreshold()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 1, Title = "Track 1" },
            new() { Id = 2, Title = "Track 2" },
            new() { Id = 3, Title = "Track 3" }
        };
        var currentIndex = 1;
        var currentTime = 2.0; // Under 3 second threshold

        // Act
        var (track, shouldRestart) = _service.GetPreviousTrackOrRestart(queue, currentIndex, currentTime);

        // Assert
        shouldRestart.Should().BeFalse();
        track.Should().NotBeNull();
        track!.Id.Should().Be(1); // Should return previous track
        track.Title.Should().Be("Track 1");
    }

    [Fact]
    public void GetPreviousTrackOrRestart_ShouldRestartWhenAtStartAndUnderThreshold()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 1, Title = "Track 1" },
            new() { Id = 2, Title = "Track 2" }
        };
        var currentIndex = 0; // At start
        var currentTime = 2.0; // Under threshold

        // Act
        var (track, shouldRestart) = _service.GetPreviousTrackOrRestart(queue, currentIndex, currentTime);

        // Assert
        shouldRestart.Should().BeTrue();
        track.Should().NotBeNull();
        track!.Id.Should().Be(1); // Should restart current (first) track
    }

    [Fact]
    public void GetPreviousTrackOrRestart_ShouldUseExactThreshold()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 1, Title = "Track 1" },
            new() { Id = 2, Title = "Track 2" }
        };
        var currentIndex = 1;
        var currentTime = 3.0; // Exactly at threshold

        // Act
        var (track, shouldRestart) = _service.GetPreviousTrackOrRestart(queue, currentIndex, currentTime);

        // Assert
        shouldRestart.Should().BeFalse(); // > threshold, not >= threshold
        track!.Id.Should().Be(1); // Should go to previous
    }

    [Fact]
    public void GetPreviousTrackOrRestart_ShouldRespectCustomThreshold()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 1, Title = "Track 1" },
            new() { Id = 2, Title = "Track 2" }
        };
        var currentIndex = 1;
        var currentTime = 4.0;
        var customThreshold = 5.0;

        // Act
        var (track, shouldRestart) = _service.GetPreviousTrackOrRestart(
            queue, currentIndex, currentTime, customThreshold);

        // Assert
        shouldRestart.Should().BeFalse(); // Under custom threshold
        track!.Id.Should().Be(1); // Should go to previous
    }

    [Fact]
    public void FindSongIndex_ShouldReturnCorrectIndex()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 10, Title = "Track 1" },
            new() { Id = 20, Title = "Track 2" },
            new() { Id = 30, Title = "Track 3" }
        };

        // Act
        var index = _service.FindSongIndex(queue, 20);

        // Assert
        index.Should().Be(1);
    }

    [Fact]
    public void FindSongIndex_ShouldReturnNegativeOneWhenNotFound()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 10, Title = "Track 1" },
            new() { Id = 20, Title = "Track 2" }
        };

        // Act
        var index = _service.FindSongIndex(queue, 999);

        // Assert
        index.Should().Be(-1);
    }

    [Fact]
    public void FindSongIndex_ShouldReturnZeroForFirstTrack()
    {
        // Arrange
        var queue = new List<MediaQueueService.QueueTrack>
        {
            new() { Id = 1, Title = "Track 1" },
            new() { Id = 2, Title = "Track 2" }
        };

        // Act
        var index = _service.FindSongIndex(queue, 1);

        // Assert
        index.Should().Be(0);
    }
}
