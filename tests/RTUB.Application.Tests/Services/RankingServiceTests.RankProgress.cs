// RankingService: rank progress.
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class RankingServiceTests
{
    [Fact]
    public void GetRankName_WithValidLevel_ReturnsCorrectName()
    {
        // Act
        var result = _service.GetRankName(3);

        // Assert
        result.Should().Be("Já vai alto");
    }

    [Fact]
    public void GetRankName_WithInvalidLevel_ReturnsUnknown()
    {
        // Act
        var result = _service.GetRankName(999);

        // Assert
        result.Should().Be("Desconhecido");
    }

    [Fact]
    public void GetXpForNextLevel_WithLevelBelowMax_ReturnsNextThreshold()
    {
        // Act
        var result = _service.GetXpForNextLevel(2);

        // Assert
        result.Should().Be(250); // Level 3 threshold
    }

    [Fact]
    public void GetXpForNextLevel_WithMaxLevel_ReturnsMaxValue()
    {
        // Act
        var result = _service.GetXpForNextLevel(5);

        // Assert
        result.Should().Be(int.MaxValue);
    }

    [Fact]
    public void GetXpForCurrentLevel_WithValidLevel_ReturnsCorrectThreshold()
    {
        // Act
        var result = _service.GetXpForCurrentLevel(3);

        // Assert
        result.Should().Be(250);
    }

    [Fact]
    public async Task GetRankProgressAsync_WithValidUser_ReturnsCorrectProgress()
    {
        // Arrange
        var userId = "user-123";
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };

        await _context.Rehearsals.AddAsync(rehearsal);
        var attendance = RehearsalAttendance.Create(1, userId);
        attendance.Attended = true;
        await _context.RehearsalAttendances.AddAsync(attendance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.CurrentXp.Should().Be(10); // 1 rehearsal * 10 XP
        result.CurrentLevel.Should().Be(1);
        result.CurrentRankName.Should().Be("Lei Seca");
        result.XpForCurrentLevel.Should().Be(0);
        result.XpForNextLevel.Should().Be(100);
        result.XpToNextLevel.Should().Be(90);
        result.ProgressPercentage.Should().Be(10);
        result.IsMaxLevel.Should().BeFalse();
    }

    [Fact]
    public async Task GetRankProgressAsync_WithMaxLevel_ReturnsMaxLevelProgress()
    {
        // Arrange
        var userId = "user-123";
        var rehearsal = new Rehearsal { Id = 1, Date = DateTime.UtcNow.AddDays(-1) };

        await _context.Rehearsals.AddAsync(rehearsal);

        // Add enough attendances to reach max level (1000 XP)
        for (int i = 1; i <= 100; i++)
        {
            var attendance = RehearsalAttendance.Create(1, userId);
            attendance.Attended = true;
            await _context.RehearsalAttendances.AddAsync(attendance);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRankProgressAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.CurrentXp.Should().Be(1000); // 100 rehearsals * 10 XP
        result.CurrentLevel.Should().Be(5);
        result.CurrentRankName.Should().Be("Veterano das Cervejas");
        result.IsMaxLevel.Should().BeTrue();
        result.XpToNextLevel.Should().Be(0);
        result.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithEventTypeSpecificXp_UseConfiguredValue()
    {
        // Arrange
        var userId = "user-123";
        var festivalEvent = Event.Create("Festival Event", DateTime.UtcNow.AddDays(-1), "Location", EventType.Festival);
        festivalEvent.Id = 1;

        await _context.Events.AddAsync(festivalEvent);
        var enrollment = Enrollment.Create(userId, 1);
        enrollment.WillAttend = true;
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Festival gives 50 XP according to config
        result.Should().Be(50);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithEventTypeNotInConfig_GivesZeroXp()
    {
        // Arrange
        var userId = "user-123";
        var nebraEvent = Event.Create("Nerba Event", DateTime.UtcNow.AddDays(-1), "Location", EventType.Nerba);
        nebraEvent.Id = 1;

        await _context.Events.AddAsync(nebraEvent);
        var enrollment = Enrollment.Create(userId, 1);
        enrollment.WillAttend = true;
        await _context.Enrollments.AddAsync(enrollment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Nerba not in config, gives 0 XP
        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateTotalXpAsync_WithMixedEventTypes_CalculatesCorrectTotal()
    {
        // Arrange
        var userId = "user-123";
        var festivalEvent = Event.Create("Festival", DateTime.UtcNow.AddDays(-1), "Location", EventType.Festival);
        festivalEvent.Id = 1;
        var atuacaoEvent = Event.Create("Atuacao", DateTime.UtcNow.AddDays(-2), "Location", EventType.Atuacao);
        atuacaoEvent.Id = 2;
        var casamentoEvent = Event.Create("Casamento", DateTime.UtcNow.AddDays(-3), "Location", EventType.Casamento);
        casamentoEvent.Id = 3;

        await _context.Events.AddRangeAsync(festivalEvent, atuacaoEvent, casamentoEvent);
        var enrollment1 = Enrollment.Create(userId, 1);
        enrollment1.WillAttend = true;
        var enrollment2 = Enrollment.Create(userId, 2);
        enrollment2.WillAttend = true;
        var enrollment3 = Enrollment.Create(userId, 3);
        enrollment3.WillAttend = true;
        await _context.Enrollments.AddRangeAsync(enrollment1, enrollment2, enrollment3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CalculateTotalXpAsync(userId);

        // Assert - Festival (50) + Atuacao (25) + Casamento (30) = 105
        result.Should().Be(105);
    }
}
