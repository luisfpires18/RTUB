using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for TrophyService
/// Tests business logic and service layer operations
/// </summary>
public class TrophyServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TrophyService _trophyService;
    private readonly Event _testEvent;

    public TrophyServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _trophyService = new TrophyService(_context);

        // Create a test event
        _testEvent = Event.Create("Test Festival", DateTime.Now.AddDays(-10), "Test Location", EventType.Festival);
        _context.Events.Add(_testEvent);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesTrophy()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", _testEvent.Id);

        // Act
        var result = await _trophyService.CreateAsync(trophy);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("1º Lugar");
        result.EventId.Should().Be(_testEvent.Id);
        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTrophy_ReturnsTrophy()
    {
        // Arrange
        var trophy = Trophy.Create("Melhor Apresentação", _testEvent.Id);
        _context.Trophies.Add(trophy);
        await _context.SaveChangesAsync();

        // Act
        var result = await _trophyService.GetByIdAsync(trophy.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(trophy.Id);
        result.Name.Should().Be("Melhor Apresentação");
        result.Event.Should().NotBeNull();
        result.Event!.Id.Should().Be(_testEvent.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingTrophy_ReturnsNull()
    {
        // Act
        var result = await _trophyService.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleTrophies_ReturnsAllTrophies()
    {
        // Arrange
        _context.Trophies.Add(Trophy.Create("1º Lugar", _testEvent.Id));
        _context.Trophies.Add(Trophy.Create("2º Lugar", _testEvent.Id));
        _context.Trophies.Add(Trophy.Create("Melhor Apresentação", _testEvent.Id));
        await _context.SaveChangesAsync();

        // Act
        var result = await _trophyService.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_WithNoTrophies_ReturnsEmptyCollection()
    {
        // Act
        var result = await _trophyService.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByEventIdAsync_WithEventTrophies_ReturnsTrophiesForEvent()
    {
        // Arrange
        var event1 = Event.Create("Festival 1", DateTime.Now, "Location 1", EventType.Festival);
        var event2 = Event.Create("Festival 2", DateTime.Now, "Location 2", EventType.Festival);
        _context.Events.AddRange(event1, event2);
        await _context.SaveChangesAsync();

        _context.Trophies.Add(Trophy.Create("Trophy 1", event1.Id));
        _context.Trophies.Add(Trophy.Create("Trophy 2", event1.Id));
        _context.Trophies.Add(Trophy.Create("Trophy 3", event2.Id));
        await _context.SaveChangesAsync();

        // Act
        var result = await _trophyService.GetByEventIdAsync(event1.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.EventId == event1.Id);
    }

    [Fact]
    public async Task GetByEventIdAsync_WithNoTrophies_ReturnsEmptyCollection()
    {
        // Act
        var result = await _trophyService.GetByEventIdAsync(_testEvent.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesTrophy()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", _testEvent.Id);
        _context.Trophies.Add(trophy);
        await _context.SaveChangesAsync();

        // Act
        trophy.Update("Campeão Geral");
        await _trophyService.UpdateAsync(trophy);

        // Assert
        var updated = await _context.Trophies.FindAsync(trophy.Id);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Campeão Geral");
    }

    [Fact]
    public async Task DeleteAsync_ExistingTrophy_RemovesTrophy()
    {
        // Arrange
        var trophy = Trophy.Create("1º Lugar", _testEvent.Id);
        _context.Trophies.Add(trophy);
        await _context.SaveChangesAsync();
        var trophyId = trophy.Id;

        // Act
        await _trophyService.DeleteAsync(trophyId);

        // Assert
        var deleted = await _context.Trophies.FindAsync(trophyId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingTrophy_DoesNotThrow()
    {
        // Act
        var act = async () => await _trophyService.DeleteAsync(999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAllAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        var trophy1 = Trophy.Create("First", _testEvent.Id);
        await Task.Delay(10); // Small delay to ensure different timestamps
        var trophy2 = Trophy.Create("Second", _testEvent.Id);
        await Task.Delay(10);
        var trophy3 = Trophy.Create("Third", _testEvent.Id);

        _context.Trophies.AddRange(trophy1, trophy2, trophy3);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _trophyService.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        // Most recent first
        result[0].CreatedAt.Should().BeAfter(result[1].CreatedAt);
        result[1].CreatedAt.Should().BeAfter(result[2].CreatedAt);
    }

    [Fact]
    public async Task GetByEventIdAsync_OrdersByName()
    {
        // Arrange
        _context.Trophies.Add(Trophy.Create("C Trophy", _testEvent.Id));
        _context.Trophies.Add(Trophy.Create("A Trophy", _testEvent.Id));
        _context.Trophies.Add(Trophy.Create("B Trophy", _testEvent.Id));
        await _context.SaveChangesAsync();

        // Act
        var result = (await _trophyService.GetByEventIdAsync(_testEvent.Id)).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("A Trophy");
        result[1].Name.Should().Be("B Trophy");
        result[2].Name.Should().Be("C Trophy");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
