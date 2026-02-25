using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MeetingAtaService
/// Tests business logic and service layer operations with Repository pattern
/// </summary>
public class MeetingAtaServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MeetingAtaRepository _ataRepository;
    private readonly MeetingAtaService _ataService;
    private readonly Mock<IDbContextFactory<ApplicationDbContext>> _mockContextFactory;
    private ApplicationUser _testUser1;
    private ApplicationUser _testUser2;
    private Meeting _testMeeting;

    public MeetingAtaServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();

        // Setup IDbContextFactory mock for repository - return new context each time
        // This simulates the real behavior where repository creates fresh contexts
        var mockRepositoryContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        mockRepositoryContextFactory.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(() => _fixture.CreateContext()); // Create new context each time
        _ataRepository = new MeetingAtaRepository(mockRepositoryContextFactory.Object);

        // Setup IDbContextFactory mock to return our test context
        _mockContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        _mockContextFactory.Setup(f => f.CreateDbContext())
            .Returns(() => _fixture.CreateContext());

        _ataService = new MeetingAtaService(
            _ataRepository,
            _mockContextFactory.Object);

        // Create test users (only if they don't exist for shared database)
        var userId1 = "ata-test-user-1";
        var userId2 = "ata-test-user-2";

        if (!_context.Users.Any(u => u.Id == userId1))
        {
            _testUser1 = new ApplicationUser
            {
                Id = userId1,
                UserName = "ata_testuser1",
                Email = "ata_test1@test.com",
                FirstName = "Ata",
                LastName = "User1",
                Nickname = "AtaTestUser1"
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
                UserName = "ata_testuser2",
                Email = "ata_test2@test.com",
                FirstName = "Ata",
                LastName = "User2",
                Nickname = "AtaTestUser2"
            };
            _context.Users.Add(_testUser2);
        }
        else
        {
            _testUser2 = _context.Users.Find(userId2)!;
        }

        // Create test meeting
        _testMeeting = new Meeting
        {
            Title = "Test Meeting",
            Statement = "Test Statement",
            Date = DateTime.Now.AddDays(7),
            Location = "Test Location",
            Type = MeetingType.ConselhoVeteranos
        };
        _context.Meetings.Add(_testMeeting);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAtaAsync_WithValidData_CreatesAta()
    {
        // Arrange
        var ata = new MeetingAta
        {
            MeetingId = _testMeeting.Id,
            ActualStartTime = DateTime.Now,
            Location = "Test Location",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Test Quorum",
            AttendeesPresent = "User1, User2",
            AttendeesAbsent = "User3",
            Status = MeetingAtaStatus.Draft
        };

        // Act
        var result = await _ataService.CreateAtaAsync(ata);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.MeetingId.Should().Be(_testMeeting.Id);
        result.Status.Should().Be(MeetingAtaStatus.Draft);

        var savedAta = await _context.MeetingAtas.FindAsync(result.Id);
        savedAta.Should().NotBeNull();
        savedAta!.MeetingId.Should().Be(_testMeeting.Id);
    }

    [Fact]
    public async Task CreateAtaAsync_WithExistingAta_ThrowsException()
    {
        // Arrange
        var existingAta = new MeetingAta
        {
            MeetingId = _testMeeting.Id,
            ActualStartTime = DateTime.Now,
            Location = "Test Location",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Test Quorum",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Draft
        };
        _context.MeetingAtas.Add(existingAta);
        await _context.SaveChangesAsync();

        var newAta = new MeetingAta
        {
            MeetingId = _testMeeting.Id, // Same meeting
            ActualStartTime = DateTime.Now,
            Location = "Another Location",
            PresidentUserId = _testUser2.Id,
            QuorumBasis = "Another Quorum",
            AttendeesPresent = "User3",
            AttendeesAbsent = "User4",
            Status = MeetingAtaStatus.Draft
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _ataService.CreateAtaAsync(newAta));
    }

    [Fact]
    public async Task UpdateAtaAsync_WithValidData_UpdatesAta()
    {
        // Arrange
        var ata = new MeetingAta
        {
            MeetingId = _testMeeting.Id,
            ActualStartTime = DateTime.Now,
            Location = "Original Location",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Original Quorum",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Draft
        };
        _context.MeetingAtas.Add(ata);
        await _context.SaveChangesAsync();

        // Create a new ata object with updated values (simulating what would come from the UI)
        var updatedAta = new MeetingAta
        {
            Id = ata.Id,
            MeetingId = _testMeeting.Id,
            ActualStartTime = ata.ActualStartTime,
            Location = "Updated Location",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Updated Quorum",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Draft
        };

        // Act
        await _ataService.UpdateAtaAsync(updatedAta);

        // Assert - Query fresh from database to verify changes (repository uses new context)
        var freshContext = _fixture.CreateContext();
        try
        {
            var updatedAtaFromDb = await freshContext.MeetingAtas.FindAsync(ata.Id);
            updatedAtaFromDb.Should().NotBeNull();
            updatedAtaFromDb!.Location.Should().Be("Updated Location");
            updatedAtaFromDb.QuorumBasis.Should().Be("Updated Quorum");
        }
        finally
        {
            freshContext.Dispose();
        }
    }

    [Fact]
    public async Task UpdateAtaAsync_WithPublishedAta_ThrowsException()
    {
        // Arrange
        var ata = new MeetingAta
        {
            MeetingId = _testMeeting.Id,
            ActualStartTime = DateTime.Now,
            Location = "Test Location",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Test Quorum",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Published // Published ata
        };
        _context.MeetingAtas.Add(ata);
        await _context.SaveChangesAsync();

        ata.Location = "Hacked Location";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _ataService.UpdateAtaAsync(ata));
    }

    [Fact]
    public async Task UpdateAtaAsync_WithNonExistentAta_ThrowsException()
    {
        // Arrange
        var nonExistentAta = new MeetingAta
        {
            Id = 99999, // Non-existent ID
            MeetingId = _testMeeting.Id,
            ActualStartTime = DateTime.Now,
            Location = "Test Location",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Test Quorum",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Draft
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(async () =>
            await _ataService.UpdateAtaAsync(nonExistentAta));
    }

    [Fact]
    public async Task DeleteAtaAsync_WithDraftAta_DeletesAta()
    {
        // Arrange
        var ata = new MeetingAta
        {
            MeetingId = _testMeeting.Id,
            ActualStartTime = DateTime.Now,
            Location = "Test Location",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Test Quorum",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Draft
        };
        _context.MeetingAtas.Add(ata);
        await _context.SaveChangesAsync();
        var ataId = ata.Id;

        // Act
        await _ataService.DeleteAtaAsync(ataId);

        // Assert - Query fresh from database to verify deletion (repository uses new context)
        var freshContext = _fixture.CreateContext();
        try
        {
            var deletedAta = await freshContext.MeetingAtas.FindAsync(ataId);
            deletedAta.Should().BeNull();
        }
        finally
        {
            freshContext.Dispose();
        }
    }

    [Fact]
    public async Task DeleteAtaAsync_WithPublishedAta_ThrowsException()
    {
        // Arrange
        var ata = new MeetingAta
        {
            MeetingId = _testMeeting.Id,
            ActualStartTime = DateTime.Now,
            Location = "Test Location",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Test Quorum",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Published // Published ata
        };
        _context.MeetingAtas.Add(ata);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _ataService.DeleteAtaAsync(ata.Id));

        // Verify ata was NOT deleted
        var ataStillExists = await _context.MeetingAtas.FindAsync(ata.Id);
        ataStillExists.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAtaStatusForMeetingsAsync_ReturnsStatusForMultipleMeetings()
    {
        // Arrange
        var meeting1 = new Meeting
        {
            Title = "Meeting 1",
            Date = DateTime.Now.AddDays(1),
            Location = "Location 1",
            Statement = "Statement 1",
            Type = MeetingType.ConselhoVeteranos
        };
        var meeting2 = new Meeting
        {
            Title = "Meeting 2",
            Date = DateTime.Now.AddDays(2),
            Location = "Location 2",
            Statement = "Statement 2",
            Type = MeetingType.AssembleiaGeralOrdinaria
        };
        _context.Meetings.AddRange(meeting1, meeting2);
        await _context.SaveChangesAsync();

        var ata1 = new MeetingAta
        {
            MeetingId = meeting1.Id,
            ActualStartTime = DateTime.Now,
            Location = "Location 1",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Quorum 1",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Draft
        };
        var ata2 = new MeetingAta
        {
            MeetingId = meeting2.Id,
            ActualStartTime = DateTime.Now,
            Location = "Location 2",
            PresidentUserId = _testUser1.Id,
            QuorumBasis = "Quorum 2",
            AttendeesPresent = "User1",
            AttendeesAbsent = "User2",
            Status = MeetingAtaStatus.Published
        };
        _context.MeetingAtas.AddRange(ata1, ata2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ataService.GetAtaStatusForMeetingsAsync(new[] { meeting1.Id, meeting2.Id, 99999 });

        // Assert
        result.Should().HaveCount(3);
        result[meeting1.Id].Should().Be(MeetingAtaStatus.Draft);
        result[meeting2.Id].Should().Be(MeetingAtaStatus.Published);
        result[99999].Should().BeNull(); // Meeting without ATA
    }
}
