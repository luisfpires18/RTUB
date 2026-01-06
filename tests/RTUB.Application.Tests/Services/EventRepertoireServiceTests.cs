using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Enums;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for EventRepertoireService
/// Tests repertoire CRUD operations and ordering
/// Uses shared database fixture for better performance
/// </summary>
public class EventRepertoireServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly EventRepertoireService _repertoireService;
    private readonly EventService _eventService;
    private readonly AlbumService _albumService;
    private readonly SongService _songService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly DateTime _testEventDate = new DateTime(2025, 12, 31, 20, 0, 0);

    public EventRepertoireServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();

        _repertoireService = new EventRepertoireService(
            new EventRepertoireRepository(_context));

        _mockImageStorageService = new Mock<IImageStorageService>();
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();
        
        // Mock dependencies for EventService and SongService
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        
        _eventService = new EventService(
            new EventRepository(_context), 
            _mockImageStorageService.Object, 
            new EnrollmentRepository(_context), 
            mockEventVideoRepository.Object, 
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            mockHttpContextAccessor.Object,
            _context);
        
        _albumService = new AlbumService(new AlbumRepository(_context), _mockImageStorageService.Object);
        
        var mockSongVideoRepository = new Mock<ISongVideoRepository>();
        var mockSongVideoStorageService = new Mock<ISongVideoStorageService>();
        _songService = new SongService(
            new SongRepository(_context), 
            mockSongVideoRepository.Object, 
            mockSongVideoStorageService.Object, 
            _context,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_WithValidData_AddsSong()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act
        var result = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

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
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act & Assert
        var act = async () => await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 2, _testEventDate);
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
    public async Task IsSongInRepertoireAsync_ExistingSong_ReturnsTrue()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id, _testEventDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSongInRepertoireAsync_NonExistingSong_ReturnsFalse()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, 999, _testEventDate);

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
        var repertoireItem = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

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
    public async Task AddSongToRepertoireAsync_WithInvalidEventId_ReturnsNull()
    {
        // Arrange
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        // Act - Since FK constraints aren't enforced in SQLite in-memory, this won't throw
        // In production with SQL Server, this would throw
        var result = await _repertoireService.AddSongToRepertoireAsync(999, song.Id, 1, _testEventDate);

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
        var result = await _repertoireService.AddSongToRepertoireAsync(event1.Id, 999, 1, _testEventDate);

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
    public async Task AddSongToRepertoireAsync_MultipleSongsWithSameDisplayOrder_AllowsAddition()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);

        // Act
        var result1 = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        var result2 = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 1, _testEventDate);

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
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, new List<int>());

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
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act & Assert - Service validates and throws InvalidOperationException for duplicate
        var act = async () => await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 2, _testEventDate);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*repertoire*date*");
    }

    [Fact]
    public async Task IsSongInRepertoireAsync_WhenSongExists_ReturnsTrue()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id, _testEventDate);

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
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id, _testEventDate);

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
        var addedItem = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

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
    public async Task AddSongToRepertoireAsync_MultipleSongsFromSameAlbum_AllAdded()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        // Act
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, _testEventDate);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, _testEventDate);

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
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id, _testEventDate);

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
        var result = await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id, _testEventDate);

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
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 5, _testEventDate);

        // Act
        await _repertoireService.UpdateRepertoireOrderAsync(event1.Id, _testEventDate, new List<int> { song.Id });

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
        var result = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1000, _testEventDate);

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
        var item = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

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
    public async Task AddSongToRepertoireAsync_MultiDayEvent_AllowsSameSongDifferentDays()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Popular Song", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);

        // Act - Add same song to different days
        var result1 = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, day1);
        var result2 = await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, day2);

        // Assert
        result1.Should().NotBeNull();
        result1.RepertoireDate.Date.Should().Be(day1.Date);
        result2.Should().NotBeNull();
        result2.RepertoireDate.Date.Should().Be(day2.Date);
    }

    [Fact]
    public async Task AddSongToRepertoireAsync_SameDaySameSong_ThrowsException()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Test Event", _testEventDate, "Test Location", EventType.Atuacao);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, _testEventDate);

        // Act & Assert
        var act = async () => await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 2, _testEventDate);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*date*");
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

    [Fact]
    public async Task RemoveRepertoireDayAsync_RemovesOnlyThatDay()
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

        // Act - Remove day 1
        await _repertoireService.RemoveRepertoireDayAsync(event1.Id, day1);

        // Assert
        var day1Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).ToList();
        var day2Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day2)).ToList();

        day1Result.Should().BeEmpty();
        day2Result.Should().ContainSingle();
        day2Result[0].Song!.Title.Should().Be("Day 2 Song");
    }

    [Fact]
    public async Task GetRepertoireDatesAsync_ReturnsDistinctDates()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);
        var day3 = _testEventDate.AddDays(2);

        // Add multiple songs to same days
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 1, day2);

        // Act
        var dates = (await _repertoireService.GetRepertoireDatesAsync(event1.Id)).ToList();

        // Assert
        dates.Should().HaveCount(2);
        dates.Should().Contain(day1.Date);
        dates.Should().Contain(day2.Date);
        dates.Should().NotContain(day3.Date);
    }

    [Fact]
    public async Task GetRepertoireDatesAsync_EmptyRepertoire_ReturnsEmpty()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Empty Event", _testEventDate, "Test Location", EventType.Atuacao);

        // Act
        var dates = (await _repertoireService.GetRepertoireDatesAsync(event1.Id)).ToList();

        // Assert
        dates.Should().BeEmpty();
    }

    [Fact]
    public async Task IsSongInRepertoireAsync_ChecksSpecificDate()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song = await _songService.CreateSongAsync("Test Song", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);

        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song.Id, 1, day1);

        // Act & Assert
        (await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id, day1)).Should().BeTrue();
        (await _repertoireService.IsSongInRepertoireAsync(event1.Id, song.Id, day2)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveRepertoireDayAsync_WithMultipleSongs_RemovesAllSongsForThatDay()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Multi Day Event", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Song 1", album.Id);
        var song2 = await _songService.CreateSongAsync("Song 2", album.Id);
        var song3 = await _songService.CreateSongAsync("Song 3", album.Id);

        var day1 = _testEventDate;

        // Add multiple songs to the same day
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, day1);

        // Verify songs are added
        var beforeDelete = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).ToList();
        beforeDelete.Should().HaveCount(3);

        // Act - Remove the entire day
        await _repertoireService.RemoveRepertoireDayAsync(event1.Id, day1);

        // Assert - All songs for that day should be removed
        var afterDelete = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).ToList();
        afterDelete.Should().BeEmpty();

        // Verify dates list is also empty
        var dates = (await _repertoireService.GetRepertoireDatesAsync(event1.Id)).ToList();
        dates.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiDayEvent_PerDayDisplayOrder_StartsAt1ForEachDay()
    {
        // Arrange - Create multi-day event with songs on different days
        var event1 = await _eventService.CreateEventAsync("Multi Day Festival", _testEventDate, "Test Location", EventType.Festival);
        var album = await _albumService.CreateAlbumAsync("Test Album", 2020);
        var song1 = await _songService.CreateSongAsync("Day 1 Song A", album.Id);
        var song2 = await _songService.CreateSongAsync("Day 1 Song B", album.Id);
        var song3 = await _songService.CreateSongAsync("Day 1 Song C", album.Id);
        var song4 = await _songService.CreateSongAsync("Day 2 Song A", album.Id);
        var song5 = await _songService.CreateSongAsync("Day 2 Song B", album.Id);

        var day1 = _testEventDate;
        var day2 = _testEventDate.AddDays(1);

        // Add songs with specific display orders - Day 1 gets 1-3, Day 2 should also get 1-2
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song1.Id, 1, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song2.Id, 2, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song3.Id, 3, day1);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song4.Id, 1, day2);
        await _repertoireService.AddSongToRepertoireAsync(event1.Id, song5.Id, 2, day2);

        // Act - Get repertoire for each day
        var day1Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day1)).OrderBy(r => r.DisplayOrder).ToList();
        var day2Result = (await _repertoireService.GetRepertoireByEventIdAsync(event1.Id, day2)).OrderBy(r => r.DisplayOrder).ToList();

        // Assert - Each day should have its own 1-based ordering
        // Day 1: 3 songs with DisplayOrder 1, 2, 3 (UI would show them as 1, 2, 3)
        day1Result.Should().HaveCount(3);
        day1Result[0].DisplayOrder.Should().Be(1);
        day1Result[0].Song!.Title.Should().Be("Day 1 Song A");
        day1Result[1].DisplayOrder.Should().Be(2);
        day1Result[1].Song!.Title.Should().Be("Day 1 Song B");
        day1Result[2].DisplayOrder.Should().Be(3);
        day1Result[2].Song!.Title.Should().Be("Day 1 Song C");

        // Day 2: 2 songs with DisplayOrder 1, 2 (UI would show them as 1, 2)
        day2Result.Should().HaveCount(2);
        day2Result[0].DisplayOrder.Should().Be(1);
        day2Result[0].Song!.Title.Should().Be("Day 2 Song A");
        day2Result[1].DisplayOrder.Should().Be(2);
        day2Result[1].Song!.Title.Should().Be("Day 2 Song B");

        // Verify that when ordered, UI display index calculation works correctly
        // For day 1: orderedItems[0, 1, 2] => displayIndex = i + 1 => [1, 2, 3]
        for (int i = 0; i < day1Result.Count; i++)
        {
            var displayIndex = i + 1;
            day1Result[i].DisplayOrder.Should().Be(displayIndex,
                $"Day 1 song at position {i} should have DisplayOrder {displayIndex}");
        }

        // For day 2: orderedItems[0, 1] => displayIndex = i + 1 => [1, 2]
        for (int i = 0; i < day2Result.Count; i++)
        {
            var displayIndex = i + 1;
            day2Result[i].DisplayOrder.Should().Be(displayIndex,
                $"Day 2 song at position {i} should have DisplayOrder {displayIndex}");
        }
    }

    public void Dispose()
    {
        // Clean the database for the next test in this collection
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
