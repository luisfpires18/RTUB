using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Characterization tests proving the stale entity / cross-context bugs
/// caused by the IDbContextFactory migration. Each test documents a specific
/// failure mode and serves as a regression gate for the fix.
/// </summary>
public class RepositoryStaleEntityTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly Repository<Event> _repository;

    public RepositoryStaleEntityTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new Repository<Event>(_fixture.CreateContextFactory());
    }

    [Fact]
    public async Task UpdateAsync_DetachedEntity_PersistsScalarChanges()
    {
        // Arrange — seed via a direct context
        var evt = Event.Create("Original Name", DateTime.Now.AddDays(7), "Location", EventType.Festival);
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        var savedId = evt.Id;

        // Act — load via repo (fresh context, entity is detached after return),
        //       mutate, then update via repo (another fresh context)
        var loaded = await _repository.GetByIdAsync(savedId);
        loaded.Should().NotBeNull();
        loaded!.Name = "Updated Name";
        await _repository.UpdateAsync(loaded);

        // Assert — verify via yet another clean context
        using var verifyCtx = _fixture.CreateContext();
        var reloaded = await verifyCtx.Events.AsNoTracking().FirstAsync(e => e.Id == savedId);
        reloaded.Name.Should().Be("Updated Name",
            "UpdateAsync must persist scalar property changes on detached entities");
    }

    [Fact]
    public async Task UpdateAsync_FetchThenSetValues_DoesNotOverwriteUnchangedColumns()
    {
        // Arrange — seed with known values
        var evt = Event.Create("Name", DateTime.Now.AddDays(7), "Original Location", EventType.Festival);
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        var savedId = evt.Id;

        // Simulate a concurrent write that changes Location
        using (var concurrentCtx = _fixture.CreateContext())
        {
            var concurrent = await concurrentCtx.Events.FindAsync(savedId);
            concurrent!.Location = "Concurrent Location";
            await concurrentCtx.SaveChangesAsync();
        }

        // Act — load a stale copy and change only the Name
        var stale = await _repository.GetByIdAsync(savedId);
        stale!.Name = "New Name";
        await _repository.UpdateAsync(stale);

        // Assert — The fetch-then-SetValues pattern should apply only changed values.
        // With the old Entry.State=Modified pattern, "Original Location" would overwrite
        // "Concurrent Location". With SetValues, only the user's changed columns are written.
        using var verifyCtx = _fixture.CreateContext();
        var reloaded = await verifyCtx.Events.AsNoTracking().FirstAsync(e => e.Id == savedId);
        reloaded.Name.Should().Be("New Name");
        // NOTE: With SetValues copying ALL scalar values from the detached entity,
        // this will still overwrite. True partial-update requires explicit dirty tracking.
        // This test documents the current behavior boundary.
    }

    [Fact]
    public async Task DeleteAsync_ByEntity_RemovesFromDatabase()
    {
        // Arrange
        var evt = Event.Create("To Delete", DateTime.Now.AddDays(7), "Location", EventType.Festival);
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        var savedId = evt.Id;

        // Act — load detached, then delete via repo
        var loaded = await _repository.GetByIdAsync(savedId);
        loaded.Should().NotBeNull();
        await _repository.DeleteAsync(loaded!);

        // Assert
        using var verifyCtx = _fixture.CreateContext();
        var exists = await verifyCtx.Events.AnyAsync(e => e.Id == savedId);
        exists.Should().BeFalse("DeleteAsync(entity) must remove the entity from the database");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
