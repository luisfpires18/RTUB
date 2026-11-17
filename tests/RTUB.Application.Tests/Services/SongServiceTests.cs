using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for SongService
/// Tests song CRUD operations and YouTube URL management with Repository pattern
/// </summary>
public partial class SongServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly SongRepository _songRepository;
    private readonly SongService _songService;
    private readonly AlbumService _albumService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;
    private readonly Mock<ISongVideoRepository> _mockSongVideoRepository;
    private readonly Mock<ISongVideoStorageService> _mockSongVideoStorageService;

    public SongServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _songRepository = new SongRepository(_fixture.CreateContextFactory());
        _mockSongVideoRepository = new Mock<ISongVideoRepository>();
        _mockSongVideoStorageService = new Mock<ISongVideoStorageService>();

        // Mock dependencies for SongService
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _songService = new SongService(
            _songRepository,
            _mockSongVideoRepository.Object,
            _mockSongVideoStorageService.Object,
            new AlbumRepository(_fixture.CreateContextFactory()),
            _fixture.CreateContextFactory(),
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserManager.Object);

        _mockImageStorageService = new Mock<IImageStorageService>();
        _albumService = new AlbumService(new AlbumRepository(_fixture.CreateContextFactory()), _mockImageStorageService.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
