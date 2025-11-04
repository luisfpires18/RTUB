using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for RehearsalService
/// Tests business logic and service layer operations
/// </summary>
public class RehearsalServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RehearsalService _rehearsalService;

    public RehearsalServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _rehearsalService = new RehearsalService(_context);
    }

    [Fact]
    public async Task CreateRehearsalAsync_WithValidData_ReturnsRehearsal()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        var location = "Centro Académico";
        var theme = "Fado practice";

        // Act
        var result = await _rehearsalService.CreateRehearsalAsync(date, location, theme);

        // Assert
        result.Should().NotBeNull();
        result.Date.Should().Be(date.Date);
        result.Location.Should().Be(location);
        result.Theme.Should().Be(theme);
    }

    [Fact]
    public async Task GetRehearsalByIdAsync_ExistingRehearsal_ReturnsRehearsal()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Act
        var result = await _rehearsalService.GetRehearsalByIdAsync(rehearsal.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(rehearsal.Id);
        result.Location.Should().Be("Test Location");
    }

    [Fact]
    public async Task GetRehearsalByIdAsync_NonExistingRehearsal_ReturnsNull()
    {
        // Act
        var result = await _rehearsalService.GetRehearsalByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRehearsalByDateAsync_ExistingRehearsal_ReturnsRehearsal()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        var rehearsal = Rehearsal.Create(date, "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Act
        var result = await _rehearsalService.GetRehearsalByDateAsync(date);

        // Assert
        result.Should().NotBeNull();
        result!.Date.Should().Be(date.Date);
    }

    [Fact]
    public async Task GetRehearsalByDateAsync_NonExistingRehearsal_ReturnsNull()
    {
        // Act
        var result = await _rehearsalService.GetRehearsalByDateAsync(DateTime.Now.AddDays(30));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRehearsalsAsync_WithinDateRange_ReturnsRehearsals()
    {
        // Arrange
        var startDate = DateTime.Now;
        var endDate = DateTime.Now.AddDays(30);
        _context.Rehearsals.Add(Rehearsal.Create(DateTime.Now.AddDays(5), "Location 1"));
        _context.Rehearsals.Add(Rehearsal.Create(DateTime.Now.AddDays(10), "Location 2"));
        _context.Rehearsals.Add(Rehearsal.Create(DateTime.Now.AddDays(40), "Location 3")); // Outside range
        await _context.SaveChangesAsync();

        // Act
        var result = await _rehearsalService.GetRehearsalsAsync(startDate, endDate);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUpcomingRehearsalsAsync_ReturnsOnlyFutureNonCanceled()
    {
        // Arrange
        _context.Rehearsals.Add(Rehearsal.Create(DateTime.Today.AddDays(-5), "Past")); // Past
        var upcoming1 = Rehearsal.Create(DateTime.Today.AddDays(5), "Future 1");
        var upcoming2 = Rehearsal.Create(DateTime.Today.AddDays(10), "Future 2");
        var canceled = Rehearsal.Create(DateTime.Today.AddDays(7), "Canceled");
        canceled.Cancel();
        
        _context.Rehearsals.AddRange(upcoming1, upcoming2, canceled);
        await _context.SaveChangesAsync();

        // Act
        var result = await _rehearsalService.GetUpcomingRehearsalsAsync(10);

        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContain(r => r.IsCanceled);
    }

    [Fact]
    public async Task UpdateRehearsalAsync_WithValidData_UpdatesRehearsal()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Original Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Act
        await _rehearsalService.UpdateRehearsalAsync(rehearsal.Id, "New Location", "New Theme", "New Notes");

        // Assert
        var updated = await _context.Rehearsals.FindAsync(rehearsal.Id);
        updated!.Location.Should().Be("New Location");
        updated.Theme.Should().Be("New Theme");
        updated.Notes.Should().Be("New Notes");
    }

    [Fact]
    public async Task UpdateRehearsalAsync_NonExistingRehearsal_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _rehearsalService.UpdateRehearsalAsync(999, "Location", null, null);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CancelRehearsalAsync_ValidId_CancelsRehearsal()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Act
        await _rehearsalService.CancelRehearsalAsync(rehearsal.Id);

        // Assert
        var canceled = await _context.Rehearsals.FindAsync(rehearsal.Id);
        canceled!.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task CancelRehearsalAsync_NonExistingRehearsal_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _rehearsalService.CancelRehearsalAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteRehearsalAsync_ValidId_DeletesRehearsal()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        _context.Rehearsals.Add(rehearsal);
        await _context.SaveChangesAsync();

        // Act
        await _rehearsalService.DeleteRehearsalAsync(rehearsal.Id);

        // Assert
        var deleted = await _context.Rehearsals.FindAsync(rehearsal.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRehearsalAsync_NonExistingRehearsal_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _rehearsalService.DeleteRehearsalAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
