using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for NaipeService
/// Tests business logic and service layer operations with Repository pattern
/// </summary>
public partial class NaipeServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly NaipeContentRepository _naipeContentRepository;
    private readonly NaipeCommentRepository _naipeCommentRepository;
    private readonly NaipeTypeConfigRepository _naipeTypeConfigRepository;
    private readonly NaipeService _naipeService;
    private readonly Mock<INaipeMediaStorageService> _mockMediaStorageService;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private ApplicationUser _testUser1;
    private ApplicationUser _testUser2;
    private ApplicationUser _adminUser;

    public NaipeServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _naipeContentRepository = new NaipeContentRepository(_fixture.CreateContextFactory());
        _naipeCommentRepository = new NaipeCommentRepository(_fixture.CreateContextFactory());
        _naipeTypeConfigRepository = new NaipeTypeConfigRepository(_fixture.CreateContextFactory());

        // Setup mocks
        _mockMediaStorageService = new Mock<INaipeMediaStorageService>();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup UserManager mock
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var auditContext = new AuditContext();
        var auditLogAppender = new AuditLogAppender();

        var cache = new MemoryCache(new MemoryCacheOptions());

        _naipeService = new NaipeService(
            _naipeContentRepository,
            _naipeCommentRepository,
            _naipeTypeConfigRepository,
            _mockMediaStorageService.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object,
            _mockUserManager.Object,
            _fixture.CreateContextFactory(),
            auditContext,
            _mockHttpContextAccessor.Object,
            cache);

        // Create test users (only if they don't exist for shared database)
        var userId1 = "naipe-test-user-1";
        var userId2 = "naipe-test-user-2";
        var adminUserId = "naipe-admin-user";

        if (!_context.Users.Any(u => u.Id == userId1))
        {
            _testUser1 = new ApplicationUser
            {
                Id = userId1,
                UserName = "naipe_testuser1",
                Email = "naipe_test1@test.com",
                FirstName = "Naipe",
                LastName = "User1",
                Nickname = "NaipeTestUser1"
            };
            _context.Users.Add(_testUser1);
        }
        else
        {
            _testUser1 = _context.Users.Find(userId1)!;
        }

        if (!_context.Users.Any(u => u.Id == userId2))
        {
            _testUser2 = new ApplicationUser
            {
                Id = userId2,
                UserName = "naipe_testuser2",
                Email = "naipe_test2@test.com",
                FirstName = "Naipe",
                LastName = "User2",
                Nickname = "NaipeTestUser2"
            };
            _context.Users.Add(_testUser2);
        }
        else
        {
            _testUser2 = _context.Users.Find(userId2)!;
        }

        if (!_context.Users.Any(u => u.Id == adminUserId))
        {
            _adminUser = new ApplicationUser
            {
                Id = adminUserId,
                UserName = "naipe_admin",
                Email = "naipe_admin@test.com",
                FirstName = "Naipe",
                LastName = "Admin",
                Nickname = "NaipeAdmin"
            };
            _context.Users.Add(_adminUser);
        }
        else
        {
            _adminUser = _context.Users.Find(adminUserId)!;
        }

        _context.SaveChanges();

        // Setup UserManager to return users and roles
        _mockUserManager.Setup(m => m.FindByIdAsync(_testUser1.Id))
            .ReturnsAsync(_testUser1);
        _mockUserManager.Setup(m => m.FindByIdAsync(_testUser2.Id))
            .ReturnsAsync(_testUser2);
        _mockUserManager.Setup(m => m.FindByIdAsync(_adminUser.Id))
            .ReturnsAsync(_adminUser);
        _mockUserManager.Setup(m => m.GetRolesAsync(_adminUser))
            .ReturnsAsync(new List<string> { "Admin" });
        _mockUserManager.Setup(m => m.GetRolesAsync(_testUser1))
            .ReturnsAsync(new List<string>());
        _mockUserManager.Setup(m => m.GetRolesAsync(_testUser2))
            .ReturnsAsync(new List<string>());
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private Task<List<string>> NaipeAuditAsync(int id) =>
        _context.AuditLogs.AsNoTracking().Where(a => a.EntityType == "NaipeContent" && a.EntityId == id)
            .OrderBy(a => a.Id).Select(a => a.Action).ToListAsync();

    private async Task<string?> LastNaipeChangesAsync(int id) =>
        (await _context.AuditLogs.AsNoTracking().Where(a => a.EntityType == "NaipeContent" && a.EntityId == id)
            .OrderByDescending(a => a.Id).FirstAsync()).Changes;
}
