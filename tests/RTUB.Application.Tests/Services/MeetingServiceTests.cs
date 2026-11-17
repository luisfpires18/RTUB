using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
/// Unit tests for MeetingService
/// Tests business logic for meeting operations and Veterano visibility filtering
/// Uses shared database fixture for better performance
/// </summary>
[Collection("MeetingService Collection")]
public partial class MeetingServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MeetingService _meetingService;
    private readonly ApplicationUser _veteranoUser;
    private readonly ApplicationUser _tunossauroUser;
    private readonly ApplicationUser _nonVeteranoUser;
    private readonly ApplicationUser _leitaoUser;

    public MeetingServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();

        // Create mocks for new dependencies
        var mockPushNotificationFactory = new Mock<IPushNotificationFactory>();
        var mockPushNotificationService = new Mock<IPushNotificationService>();

        _meetingService = new MeetingService(
            new MeetingRepository(_fixture.CreateContextFactory()),
            _fixture.CreateContextFactory(),
            mockPushNotificationFactory.Object,
            mockPushNotificationService.Object);

        // Disable auditing for test setup
        _context.DisableAuditing();

        // Create test users
        var currentYear = DateTime.Now.Year;
        _veteranoUser = new ApplicationUser
        {
            Id = "veterano-user-id",
            UserName = "veterano@test.com",
            NormalizedUserName = "VETERANO@TEST.COM",
            Email = "veterano@test.com",
            NormalizedEmail = "VETERANO@TEST.COM",
            FirstName = "Veterano",
            LastName = "User",
            Nickname = "VetTest",
            PhoneNumber = "123456789",
            Categories = new List<MemberCategory> { MemberCategory.Veterano },
            YearTuno = currentYear - 3,  // Started 3 years ago (>= 2 years makes them Veterano)
            MonthTuno = 1
        };

        _tunossauroUser = new ApplicationUser
        {
            Id = "tunossauro-user-id",
            UserName = "tunossauro@test.com",
            NormalizedUserName = "TUNOSSAURO@TEST.COM",
            Email = "tunossauro@test.com",
            NormalizedEmail = "TUNOSSAURO@TEST.COM",
            FirstName = "Tunossauro",
            LastName = "User",
            Nickname = "TunoTest",
            PhoneNumber = "111222333",
            Categories = new List<MemberCategory> { MemberCategory.Tunossauro },
            YearTuno = currentYear - 7,  // Started 7 years ago (>= 6 years makes them Tunossauro)
            MonthTuno = 1
        };

        _nonVeteranoUser = new ApplicationUser
        {
            Id = "non-veterano-user-id",
            UserName = "regular@test.com",
            NormalizedUserName = "REGULAR@TEST.COM",
            Email = "regular@test.com",
            NormalizedEmail = "REGULAR@TEST.COM",
            FirstName = "Regular",
            LastName = "User",
            Nickname = "RegTest",
            PhoneNumber = "987654321",
            Categories = new List<MemberCategory> { MemberCategory.Tuno },
            YearTuno = currentYear - 1,  // Started 1 year ago (< 2 years makes them Tuno)
            MonthTuno = 1
        };

        _leitaoUser = new ApplicationUser
        {
            Id = "leitao-user-id",
            UserName = "leitao@test.com",
            NormalizedUserName = "LEITAO@TEST.COM",
            Email = "leitao@test.com",
            NormalizedEmail = "LEITAO@TEST.COM",
            FirstName = "Leitao",
            LastName = "User",
            Nickname = "LeitaoTest",
            PhoneNumber = "555666777",
            Categories = new List<MemberCategory> { MemberCategory.Leitao }
        };

        // Only add users if they don't already exist (for shared database with multiple test runs)
        if (!_context.Users.Any(u => u.Id == _veteranoUser.Id))
        {
            _context.Users.Add(_veteranoUser);
            _context.Users.Add(_tunossauroUser);
            _context.Users.Add(_nonVeteranoUser);
            _context.Users.Add(_leitaoUser);
            _context.SaveChanges();
        }

        // Re-enable auditing
        _context.EnableAuditing();
    }

    public void Dispose()
    {
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
