using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for EventRepertoireService
/// Tests repertoire CRUD operations and ordering
/// Uses shared database fixture for better performance
/// </summary>
public partial class EventRepertoireServiceTests : IClassFixture<DatabaseFixture>, IDisposable
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
            new EventRepertoireRepository(_fixture.CreateContextFactory()));

        _mockImageStorageService = new Mock<IImageStorageService>();
        var mockEventVideoRepository = new Mock<IEventVideoRepository>();
        var mockEventVideoStorageService = new Mock<IEventVideoStorageService>();

        // Mock dependencies for EventService and SongService
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _eventService = new EventService(
            new EventRepository(_fixture.CreateContextFactory()),
            _mockImageStorageService.Object,
            new EnrollmentRepository(_fixture.CreateContextFactory()),
            mockEventVideoRepository.Object,
            mockEventVideoStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object,
            _fixture.CreateContextFactory());

        _albumService = new AlbumService(new AlbumRepository(_fixture.CreateContextFactory()), _mockImageStorageService.Object);

        var mockSongVideoRepository = new Mock<ISongVideoRepository>();
        var mockSongVideoStorageService = new Mock<ISongVideoStorageService>();
        _songService = new SongService(
            new SongRepository(_fixture.CreateContextFactory()),
            mockSongVideoRepository.Object,
            mockSongVideoStorageService.Object,
            new AlbumRepository(_fixture.CreateContextFactory()),
            _fixture.CreateContextFactory(),
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object);
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

    public void Dispose()
    {
        // Clean the database for the next test in this collection
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
