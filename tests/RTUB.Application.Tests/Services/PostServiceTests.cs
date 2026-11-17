using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for PostService
/// Tests business logic and service layer operations for discussion posts
/// </summary>
public partial class PostServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly PostService _service;

    public PostServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();

        // Create mocks for new dependencies
        var mockDiscussionRepository = new Mock<IDiscussionRepository>();
        var mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
        var mockPostMediaRepository = new Mock<IPostMediaRepository>();
        var mockEventMediaStorageService = new Mock<IEventMediaStorageService>();
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();
        var mockUserProfileService = new Mock<IUserProfileService>();

        _service = new PostService(
            new PostRepository(_fixture.CreateContextFactory()),
            mockDiscussionRepository.Object,
            mockEnrollmentRepository.Object,
            mockPostMediaRepository.Object,
            mockEventMediaStorageService.Object,
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object,
            mockUserProfileService.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
