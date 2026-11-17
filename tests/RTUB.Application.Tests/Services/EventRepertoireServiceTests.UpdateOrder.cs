using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

// EventRepertoireServiceTests: Update Order tests.
public partial class EventRepertoireServiceTests
{
    [Fact]
    public async Task UpdateRepertoireOrderAsync_WithNewOrder_UpdatesOrder()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, _testEventDate);

        // Act - Reverse the order
        var newOrder = new List<int> { song3.Id, song2.Id, song1.Id };
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, newOrder);

        // Get updated repertoire
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].SongId.Should().Be(song3.Id);
        result[0].DisplayOrder.Should().Be(1);
        result[1].SongId.Should().Be(song2.Id);
        result[1].DisplayOrder.Should().Be(2);
        result[2].SongId.Should().Be(song1.Id);
        result[2].DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_ReordersItemsCorrectly()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        // Add songs in order 1, 2, 3
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, _testEventDate);

        // Verify initial order
        var initialRepertoire = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        initialRepertoire.Should().HaveCount(3);
        initialRepertoire[0].SongId.Should().Be(song1.Id);
        initialRepertoire[0].DisplayOrder.Should().Be(1);
        initialRepertoire[1].SongId.Should().Be(song2.Id);
        initialRepertoire[1].DisplayOrder.Should().Be(2);
        initialRepertoire[2].SongId.Should().Be(song3.Id);
        initialRepertoire[2].DisplayOrder.Should().Be(3);

        // Act - Reorder to: song3, song1, song2
        var newOrder = new List<int> { song3.Id, song1.Id, song2.Id };
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, newOrder);

        // Assert - Verify new order
        var updatedRepertoire = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        updatedRepertoire.Should().HaveCount(3);
        updatedRepertoire[0].SongId.Should().Be(song3.Id);
        updatedRepertoire[0].DisplayOrder.Should().Be(1);
        updatedRepertoire[1].SongId.Should().Be(song1.Id);
        updatedRepertoire[1].DisplayOrder.Should().Be(2);
        updatedRepertoire[2].SongId.Should().Be(song2.Id);
        updatedRepertoire[2].DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_WithPartialOrder_UpdatesOnlySpecifiedSongs()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, _testEventDate);

        // Act - Only reorder first two songs
        var newOrder = new List<int> { song2.Id, song1.Id, song3.Id };
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, newOrder);

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result[0].SongId.Should().Be(song2.Id);
        result[0].DisplayOrder.Should().Be(1);
        result[1].SongId.Should().Be(song1.Id);
        result[1].DisplayOrder.Should().Be(2);
        result[2].SongId.Should().Be(song3.Id);
        result[2].DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_EmptyOrder_DoesNothing()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, new List<int>());

        // Assert - Original order should remain
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(1);
        result[0].DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_WithEmptyList_LeavesOrderUnchanged()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);

        // Act
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, new List<int>());

        // Assert - Original order should remain
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(2);
        result[0].SongId.Should().Be(song1.Id);
        result[0].DisplayOrder.Should().Be(1);
        result[1].SongId.Should().Be(song2.Id);
        result[1].DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_ReverseOrder_UpdatesCorrectly()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("First", album.Id);
        var song2 = await _songService.CreateSongAsync("Second", album.Id);
        var song3 = await _songService.CreateSongAsync("Third", album.Id);
        var song4 = await _songService.CreateSongAsync("Fourth", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song4.Id, 4, _testEventDate);

        // Act - Reverse the order completely
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, new List<int> { song4.Id, song3.Id, song2.Id, song1.Id });

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result[0].Song!.Title.Should().Be("Fourth");
        result[0].DisplayOrder.Should().Be(1);
        result[1].Song!.Title.Should().Be("Third");
        result[1].DisplayOrder.Should().Be(2);
        result[2].Song!.Title.Should().Be("Second");
        result[2].DisplayOrder.Should().Be(3);
        result[3].Song!.Title.Should().Be("First");
        result[3].DisplayOrder.Should().Be(4);
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_SingleItemList_UpdatesDisplayOrder()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Only Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 5, _testEventDate);

        // Act
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, new List<int> { song.Id });

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().ContainSingle();
        result[0].DisplayOrder.Should().Be(1); // Should reset to 1
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_ShuffleOrder_AllItemsPreserved()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var songs = new List<RTUB.Core.Entities.Song>();
        for (int i = 1; i <= 5; i++)
        {
            var song = await _songService.CreateSongAsync($"Song {i}", album.Id);
            songs.Add(song);
            await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, i, _testEventDate);
        }

        // Act - Shuffle: 3, 1, 5, 2, 4
        var newOrder = new List<int> { songs[2].Id, songs[0].Id, songs[4].Id, songs[1].Id, songs[3].Id };
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, newOrder);

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(5);
        result[0].Song!.Title.Should().Be("Song 3");
        result[0].DisplayOrder.Should().Be(1);
        result[1].Song!.Title.Should().Be("Song 1");
        result[1].DisplayOrder.Should().Be(2);
        result[2].Song!.Title.Should().Be("Song 5");
        result[2].DisplayOrder.Should().Be(3);
        result[3].Song!.Title.Should().Be("Song 2");
        result[3].DisplayOrder.Should().Be(4);
        result[4].Song!.Title.Should().Be("Song 4");
        result[4].DisplayOrder.Should().Be(5);
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_OnlyReordersWithinSameDay()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Day 1 Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Day 1 Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Day 2 Song", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 1, day2);

        // Act - Reorder day 1 songs
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, day1, new List<int> { song2.Id, song1.Id });

        // Assert
        var day1Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).ToList();
        var day2Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day2)).ToList();

        day1Result[0].Song!.Title.Should().Be("Day 1 Song 2");
        day1Result[0].DisplayOrder.Should().Be(1);
        day1Result[1].Song!.Title.Should().Be("Day 1 Song 1");
        day1Result[1].DisplayOrder.Should().Be(2);

        // Day 2 should be unchanged
        day2Result[0].Song!.Title.Should().Be("Day 2 Song");
        day2Result[0].DisplayOrder.Should().Be(1);
    }
}
