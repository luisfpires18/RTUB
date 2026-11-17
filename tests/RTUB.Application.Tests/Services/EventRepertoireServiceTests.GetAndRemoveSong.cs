using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

// EventRepertoireServiceTests: Get And Remove Song tests.
public partial class EventRepertoireServiceTests
{
    [Fact]
    public async Task GetRepertoireByEventIdAsync_WithMultipleSongs_ReturnsOrderedList()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);

        // Act
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].SongId.Should().Be(song1.Id);
        result[1].SongId.Should().Be(song2.Id);
        result[2].SongId.Should().Be(song3.Id);
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_EmptyRepertoire_ReturnsEmpty()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);

        // Act
        var result = await _repertoireService.GetRepertoireByEventIdAsync(event1.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveSongFromRepertoireAsync_ExistingSong_RemovesSong()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var repertoireItem = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act
        await _repertoireService.RemoveSongFromRepertoireAsync(repertoireItem.Id);
        var result = await _repertoireService.GetRepertoireByEventIdAsync(event1.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_IncludesAlbumInformation()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Song.Should().NotBeNull();
        result[0].Song!.Album.Should().NotBeNull();
        result[0].Song!.Album!.Title.Should().Be("Test Album");
    }

    [Fact]
    public async Task RemoveSongFromRepertoireAsync_WithNonExistentId_DoesNotThrow()
    {
        // Act - Service silently handles missing items (idempotent operation)
        await _repertoireService.RemoveSongFromRepertoireAsync(999);

        // Assert - No exception thrown is the expected behavior
        true.Should().BeTrue();
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_MultiplEvents_ReturnsOnlyForSpecificEvent()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Event 1", _testEventDate, "Location 1", EventType.Atuacao);
        var event2 = await _eventService.CreateEventAsync("Event 2", _testEventDate.AddDays(1), "Location 2", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event2.Id, song2.Id, 1, _testEventDate);

        // Act
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].SongId.Should().Be(song1.Id);
        result[0].EventId.Should().Be(event1.Id);
    }

    [Fact]
    public async Task RemoveSongFromRepertoireAsync_RemovesItemAndReorderingWorks()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        var item2 = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, _testEventDate);

        // Act - Remove middle song
        await _repertoireService.RemoveSongFromRepertoireAsync(item2.Id);

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(2);
        result[0].SongId.Should().Be(song1.Id);
        result[1].SongId.Should().Be(song3.Id);
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_EmptyRepertoire_ReturnsEmptyList()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Empty Event", _testEventDate, "Test Location", EventType.Atuacao);

        // Act
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveSongFromRepertoireAsync_LastSong_LeavesEmptyRepertoire()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Only Song", album.Id);
        var item = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act
        await _repertoireService.RemoveSongFromRepertoireAsync(item.Id);

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_MultipleEvents_ReturnsCorrectRepertoire()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Event 1", _testEventDate, "Location 1", EventType.Atuacao);
        var event2 = await _eventService.CreateEventAsync("Event 2", _testEventDate.AddDays(1), "Location 2", EventType.Convivio);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event2.Id, song2.Id, 1, _testEventDate);

        // Act
        var result1 = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        var result2 = (await _repertoireService.GetRepertoireByEventIdAsync(event2.Id)).ToList();

        // Assert
        result1.Should().ContainSingle();
        result1[0].Song!.Title.Should().Be("Song 1");
        result2.Should().ContainSingle();
        result2[0].Song!.Title.Should().Be("Song 2");
    }

    // ========================================
    // Multi-Day Repertoire Tests
    // ========================================

    [Fact]
    public async Task GetRepertoireByEventIdAsync_SingleDayEvent_ReturnsOnlyThatDayRepertoire()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Single Day Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);

        // Act
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, _testEventDate)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].RepertoireDate.Date.Should().Be(_testEventDate.Date);
        result[1].RepertoireDate.Date.Should().Be(_testEventDate.Date);
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_WithDateFilter_ReturnsOnlyThatDate()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Day 1 Song", album.Id);
        var song2 = await _songService.CreateSongAsync("Day 2 Song", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 1, day2);

        // Act
        var day1Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).ToList();
        var day2Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day2)).ToList();

        // Assert
        day1Result.Should().ContainSingle();
        day1Result[0].Song!.Title.Should().Be("Day 1 Song");
        day2Result.Should().ContainSingle();
        day2Result[0].Song!.Title.Should().Be("Day 2 Song");
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_NoDateFilter_ReturnsAllDays()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Day 1 Song", album.Id);
        var song2 = await _songService.CreateSongAsync("Day 2 Song", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 1, day2);

        // Act - No date filter
        var allResults = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();

        // Assert
        allResults.Should().HaveCount(2);
        allResults.Should().Contain(r => r.Song!.Title == "Day 1 Song");
        allResults.Should().Contain(r => r.Song!.Title == "Day 2 Song");
    }
}
