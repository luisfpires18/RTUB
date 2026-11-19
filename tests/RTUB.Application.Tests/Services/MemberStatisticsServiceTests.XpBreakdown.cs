// MemberStatisticsService: xp breakdown.
using FluentAssertions;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MemberStatisticsServiceTests
{
    [Fact]
    public async Task Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var xpSettings = Options.Create(new XpSettings());

        // Act & Assert
        var act = () => new MemberStatisticsService(null!, xpSettings);
        act.Should().Throw<ArgumentNullException>().WithParameterName("contextFactory");
    }

    [Fact]
    public async Task Constructor_WithNullXpSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MemberStatisticsService(_fixture.CreateContextFactory(), null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("xpSettings");
    }

    [Fact]
    public async Task GetUserXpBreakdownAsync_WithNoActivities_ReturnsZeroXp()
    {
        // Arrange
        var userId = "user-no-activities";
        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserXpBreakdownAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.TotalXp.Should().Be(0);
        result.RehearsalCount.Should().Be(0);
        result.RehearsalXpTotal.Should().Be(0);
        result.EventsByType.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserXpBreakdownAsync_WithRehearsalsAndEvents_CalculatesCorrectly()
    {
        // Arrange
        var userId = "user-with-activities";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester"
        };
        await _context.Users.AddAsync(user);

        // Create 2 past rehearsals
        var rehearsal1 = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-5) };
        var rehearsal2 = new Rehearsal { Id = 2, Date = DateTime.UtcNow.AddDays(-3) };
        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2);

        var attendance1 = RehearsalAttendance.Create(1, userId);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(2, userId);
        attendance2.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2);

        // Create events of different types
        var festivalEvent = Event.Create("Festival", DateTime.UtcNow.AddDays(-4), "Location", EventType.Festival);
        festivalEvent.Id = 1;
        var atuacaoEvent1 = Event.Create("Atuacao 1", DateTime.UtcNow.AddDays(-2), "Location", EventType.Atuacao);
        atuacaoEvent1.Id = 2;
        var atuacaoEvent2 = Event.Create("Atuacao 2", DateTime.UtcNow.AddDays(-1), "Location", EventType.Atuacao);
        atuacaoEvent2.Id = 3;
        await _context.Events.AddRangeAsync(festivalEvent, atuacaoEvent1, atuacaoEvent2);

        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = true;
        var enrollment3 = Enrollment.Create(userId, 3);
        enrollment3.WillAttend = true;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2, enrollment3);

        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserXpBreakdownAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.RehearsalCount.Should().Be(2);
        result.RehearsalXpPerUnit.Should().Be(TestXpPerRehearsal);
        result.RehearsalXpTotal.Should().Be(2 * TestXpPerRehearsal);

        result.EventsByType.Should().HaveCount(2);

        var festivalXp = result.EventsByType.FirstOrDefault(e => e.TypeName == "Festival");
        festivalXp.Should().NotBeNull();
        festivalXp!.Count.Should().Be(1);
        festivalXp.XpPerUnit.Should().Be(TestXpForFestival);
        festivalXp.TotalXp.Should().Be(TestXpForFestival);

        var atuacaoXp = result.EventsByType.FirstOrDefault(e => e.TypeName == "Atuacao");
        atuacaoXp.Should().NotBeNull();
        atuacaoXp!.Count.Should().Be(2);
        atuacaoXp.XpPerUnit.Should().Be(TestXpForAtuacao);
        atuacaoXp.TotalXp.Should().Be(2 * TestXpForAtuacao);

        result.TotalXp.Should().Be((2 * TestXpPerRehearsal) + TestXpForFestival + (2 * TestXpForAtuacao));
    }

    [Fact]
    public async Task GetUserAttendedActivitiesAsync_WithNoActivities_ReturnsEmptyList()
    {
        // Arrange
        var userId = "user-no-activities";
        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserAttendedActivitiesAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserAttendedActivitiesAsync_WithActivities_ReturnsOrderedList()
    {
        // Arrange
        var userId = "user-with-activities-2";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser2",
            Email = "test2@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester2"
        };
        await _context.Users.AddAsync(user);

        // Create rehearsals
        var rehearsal1 = new Rehearsal { Id = 10, Date = DateTime.UtcNow.AddDays(-5), Theme = "Fado Practice" };
        var rehearsal2 = new Rehearsal { Id = 11, Date = DateTime.UtcNow.AddDays(-1) };
        await _context.Rehearsals.AddRangeAsync(rehearsal1, rehearsal2);

        var attendance1 = RehearsalAttendance.Create(10, userId);
        attendance1.Attended = true;
        var attendance2 = RehearsalAttendance.Create(11, userId);
        attendance2.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(attendance1, attendance2);

        // Create event
        var festivalEvent = Event.Create("Summer Festival", DateTime.UtcNow.AddDays(-3), "Location", EventType.Festival);
        festivalEvent.Id = 10;
        await _context.Events.AddAsync(festivalEvent);

        var enrollment = Enrollment.Create(userId, 10);
        enrollment.WillAttend = true;
        await _context.Enrollments.AddAsync(enrollment);

        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserAttendedActivitiesAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);

        // Should be ordered by date descending (newest first)
        result[0].Date.Should().BeCloseTo(DateTime.UtcNow.AddDays(-1), TimeSpan.FromSeconds(1));
        result[0].Name.Should().Be("Ensaio");
        result[0].Type.Should().Be("Ensaio");
        result[0].IsRehearsal.Should().BeTrue();
        result[0].XpEarned.Should().Be(TestXpPerRehearsal);

        result[1].Date.Should().BeCloseTo(DateTime.UtcNow.AddDays(-3), TimeSpan.FromSeconds(1));
        result[1].Name.Should().Be("Summer Festival");
        result[1].Type.Should().NotBeEmpty();
        result[1].IsRehearsal.Should().BeFalse();
        result[1].XpEarned.Should().Be(TestXpForFestival);

        result[2].Date.Should().BeCloseTo(DateTime.UtcNow.AddDays(-5), TimeSpan.FromSeconds(1));
        result[2].Name.Should().Be("Ensaio - Fado Practice");
        result[2].Type.Should().Be("Ensaio");
        result[2].IsRehearsal.Should().BeTrue();
        result[2].XpEarned.Should().Be(TestXpPerRehearsal);
    }

    [Fact]
    public async Task GetUserXpBreakdownAsync_OnlyCountsPastActivities()
    {
        // Arrange
        var userId = "user-with-future-activities";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser3",
            Email = "test3@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester3"
        };
        await _context.Users.AddAsync(user);

        // Create past and future rehearsals
        var pastRehearsal = new Rehearsal { Id = 20, Date = DateTime.UtcNow.AddDays(-2) };
        var futureRehearsal = new Rehearsal { Id = 21, Date = DateTime.UtcNow.AddDays(2) };
        await _context.Rehearsals.AddRangeAsync(pastRehearsal, futureRehearsal);

        var pastAttendance = RehearsalAttendance.Create(20, userId);
        pastAttendance.Attended = true;
        var futureAttendance = RehearsalAttendance.Create(21, userId);
        futureAttendance.Attended = true;
        await _context.RehearsalAttendances.AddRangeAsync(pastAttendance, futureAttendance);

        await _context.SaveChangesAsync();

        var beforeDate = DateTime.UtcNow;

        // Act
        var result = await _service.GetUserXpBreakdownAsync(userId, beforeDate);

        // Assert
        result.Should().NotBeNull();
        result.RehearsalCount.Should().Be(1); // Only past rehearsal
        result.RehearsalXpTotal.Should().Be(TestXpPerRehearsal);
        result.TotalXp.Should().Be(TestXpPerRehearsal);
    }
}
