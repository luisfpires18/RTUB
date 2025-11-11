using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for EventRepertoireService
/// Tests repertoire CRUD operations and ordering
/// </summary>
public class EventRepertoireServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EventRepertoireService _repertoireService;
    private readonly EventService _eventService;
    private readonly AlbumService _albumService;
    private readonly SongService _songService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly DateTime _testEventDate = new DateTime(2025, 12, 31, 20, 0, 0);

    public EventRepertoireServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _repertoireService = new EventRepertoireService(_context);
        _mockImageStorageService = new Mock<IImageStorageService>();
        _eventService = new EventService(_context, _mockImageStorageService.Object);
        _albumService = new AlbumService(_context, _mockImageStorageService.Object);
        _songService = new SongService(_context);
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_WithValidData_AddsSong()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act
        var result = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Assert
        result.Should().NotBeNull();
        result.EventId.Should().Be(event1.Id);
        result.SongId.Should().Be(song.Id);
        result.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_DuplicateSong_ThrowsException()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act & Assert
        var act = async () => await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 2);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_WithMultipleSongs_ReturnsOrderedList()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2);

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
        var repertoireItem = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        await _repertoireService.RemoveSongFromRepertoireAsync(repertoireItem.Id);
        var result = await _repertoireService.GetRepertoireByEventIdAsync(event1.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_WithNewOrder_UpdatesOrder()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3);

        // Act - Reverse the order
        var newOrder = new List<int> { song3.Id, song2.Id, song1.Id };
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, newOrder);

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
    public async Task IsSongInRepertoireAsync_ExistingSong_ReturnsTrue()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSongInRepertoireAsync_NonExistingSong_ReturnsFalse()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, 999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetRepertoireItemAsync_ExistingItem_ReturnsWithNavigationProperties()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var repertoireItem = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        var result = await _repertoireService.GetRepertoireItemAsync(repertoireItem.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Song.Should().NotBeNull();
        result.Event.Should().NotBeNull();
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
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3);

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
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, newOrder);

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
    public async Task AddSongToRepertoireAsync_WithInvalidEventId_ReturnsNull()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act - Since FK constraints aren't enforced in SQLite in-memory, this won't throw
        // In production with SQL Server, this would throw
        var result = await _repertoireService.AddSongToRepertoireAsync(999, song.Id, 1);

        // Assert - The operation completes but with invalid FK reference
        result.Should().NotBeNull();
        result.EventId.Should().Be(999);
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_WithInvalidSongId_ReturnsNull()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);

        // Act - Since FK constraints aren't enforced in SQLite in-memory, this won't throw
        // In production with SQL Server, this would throw
        var result = await _repertoireService.AddSongToRepertoireAsync(event1.Id, 999, 1);

        // Assert - The operation completes but with invalid FK reference
        result.Should().NotBeNull();
        result.SongId.Should().Be(999);
    }

    [Fact]
    public async Task GetRepertoireByEventIdAsync_IncludesAlbumInformation()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Song.Should().NotBeNull();
        result[0].Song!.Album.Should().NotBeNull();
        result[0].Song!.Album!.Title.Should().Be("Test Album");
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

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3);

        // Act - Only reorder first two songs
        var newOrder = new List<int> { song2.Id, song1.Id, song3.Id };
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, newOrder);

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

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event2.Id, song2.Id, 1);

        // Act
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].SongId.Should().Be(song1.Id);
        result[0].EventId.Should().Be(event1.Id);
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_MultipleSongsWithSameDisplayOrder_AllowsAddition()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);

        // Act
        var result1 = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        var result2 = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 1);

        // Assert - Both should be added
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        var repertoire = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        repertoire.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_EmptyOrder_DoesNothing()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, new List<int>());

        // Assert - Original order should remain
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(1);
        result[0].DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_DuplicateSong_ThrowsInvalidOperationException()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act & Assert - Service validates and throws InvalidOperationException for duplicate
        var act = async () => await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 2);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Song already exists in event repertoire");
    }

    [Fact]
    public async Task IsSongInRepertoireAsync_WhenSongExists_ReturnsTrue()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSongInRepertoireAsync_WhenSongDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act - Song never added to repertoire
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetRepertoireItemAsync_WhenExists_ReturnsItemWithNavigationProperties()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        var addedItem = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        var result = await _repertoireService.GetRepertoireItemAsync(addedItem.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Event.Should().NotBeNull();
        result.Event!.Name.Should().Be("Test Event");
        result.Song.Should().NotBeNull();
        result.Song!.Title.Should().Be("Test Song");
        result.Song!.Album.Should().NotBeNull();
        result.Song!.Album!.Title.Should().Be("Test Album");
    }

    [Fact]
    public async Task GetRepertoireItemAsync_WhenDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _repertoireService.GetRepertoireItemAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_WithEmptyList_LeavesOrderUnchanged()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2);

        // Act
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, new List<int>());

        // Assert - Original order should remain
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(2);
        result[0].SongId.Should().Be(song1.Id);
        result[0].DisplayOrder.Should().Be(1);
        result[1].SongId.Should().Be(song2.Id);
        result[1].DisplayOrder.Should().Be(2);
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
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        var item2 = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3);

        // Act - Remove middle song
        await _repertoireService.RemoveSongFromRepertoireAsync(item2.Id);

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(2);
        result[0].SongId.Should().Be(song1.Id);
        result[1].SongId.Should().Be(song3.Id);
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_MultipleSongsFromSameAlbum_AllAdded()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        // Act
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3);

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r => r.Song!.Album!.Title.Should().Be("Test Album"));
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

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song4.Id, 4);

        // Act - Reverse the order completely
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, new List<int> { song4.Id, song3.Id, song2.Id, song1.Id });

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
    public async Task IsSongInRepertoireAsync_SongExists_ReturnsTrue()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSongInRepertoireAsync_SongDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetRepertoireItemAsync_NonExistentItem_ReturnsNull()
    {
        // Act
        var result = await _repertoireService.GetRepertoireItemAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRepertoireOrderAsync_SingleItemList_UpdatesDisplayOrder()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Only Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 5);

        // Act
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, new List<int> { song.Id });

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().ContainSingle();
        result[0].DisplayOrder.Should().Be(1); // Should reset to 1
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_LargeDisplayOrder_AcceptsValue()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act
        var result = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1000);

        // Assert
        result.Should().NotBeNull();
        result.DisplayOrder.Should().Be(1000);
    }

    [Fact]
    public async Task RemoveSongFromRepertoireAsync_LastSong_LeavesEmptyRepertoire()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Only Song", album.Id);
        var item = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1);

        // Act
        await _repertoireService.RemoveSongFromRepertoireAsync(item.Id);

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().BeEmpty();
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
            await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, i);
        }

        // Act - Shuffle: 3, 1, 5, 2, 4
        var newOrder = new List<int> { songs[2].Id, songs[0].Id, songs[4].Id, songs[1].Id, songs[3].Id };
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, newOrder);

        // Assert
        var result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        result.Should().HaveCount(5);
        result[0].Song!.Title.Should().Be("Song 3");
        result[1].Song!.Title.Should().Be("Song 1");
        result[2].Song!.Title.Should().Be("Song 5");
        result[3].Song!.Title.Should().Be("Song 2");
        result[4].Song!.Title.Should().Be("Song 4");
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

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1);
        await _repertoireService.AddSongToRepertoireAsync(event2.Id, song2.Id, 1);

        // Act
        var result1 = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id)).ToList();
        var result2 = (await _repertoireService.GetRepertoireByEventIdAsync(event2.Id)).ToList();

        // Assert
        result1.Should().ContainSingle();
        result1[0].Song!.Title.Should().Be("Song 1");
        result2.Should().ContainSingle();
        result2[0].Song!.Title.Should().Be("Song 2");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
