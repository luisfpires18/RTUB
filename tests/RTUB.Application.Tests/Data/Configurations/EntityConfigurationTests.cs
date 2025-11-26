using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RTUB.Application.Data;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data.Configurations;

/// <summary>
/// Tests for EF Core entity configurations
/// Validates that indexes and constraints are properly configured
/// </summary>
public class EntityConfigurationTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;

    public EntityConfigurationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _context = _fixture.CreateContext();
    }

    [Fact]
    public void SongConfiguration_HasAlbumIdIndex()
    {
        // Arrange
        var songEntity = _context.Model.FindEntityType(typeof(Song));

        // Act
        var index = songEntity?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && 
                                i.Properties[0].Name == nameof(Song.AlbumId));

        // Assert
        index.Should().NotBeNull("Index on AlbumId should exist");
        index!.GetDatabaseName().Should().Be("IX_Songs_AlbumId");
    }

    [Fact]
    public void SongConfiguration_HasAlbumIdTrackNumberCompositeIndex()
    {
        // Arrange
        var songEntity = _context.Model.FindEntityType(typeof(Song));

        // Act
        var index = songEntity?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 2 &&
                                i.Properties.Any(p => p.Name == nameof(Song.AlbumId)) &&
                                i.Properties.Any(p => p.Name == nameof(Song.TrackNumber)));

        // Assert
        index.Should().NotBeNull("Composite index on AlbumId and TrackNumber should exist");
        index!.GetDatabaseName().Should().Be("IX_Songs_AlbumId_TrackNumber");
    }

    [Fact]
    public void EventRepertoireConfiguration_HasEventIdDisplayOrderIndex()
    {
        // Arrange
        var entity = _context.Model.FindEntityType(typeof(EventRepertoire));

        // Act
        var index = entity?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 2 &&
                                i.Properties.Any(p => p.Name == nameof(EventRepertoire.EventId)) &&
                                i.Properties.Any(p => p.Name == nameof(EventRepertoire.DisplayOrder)));

        // Assert
        index.Should().NotBeNull("Composite index on EventId and DisplayOrder should exist");
        index!.GetDatabaseName().Should().Be("IX_EventRepertoires_EventId_DisplayOrder");
    }

    [Fact]
    public void EventRepertoireConfiguration_HasSongIdIndex()
    {
        // Arrange
        var entity = _context.Model.FindEntityType(typeof(EventRepertoire));

        // Act
        var index = entity?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && 
                                i.Properties[0].Name == nameof(EventRepertoire.SongId));

        // Assert
        index.Should().NotBeNull("Index on SongId should exist");
        index!.GetDatabaseName().Should().Be("IX_EventRepertoires_SongId");
    }

    [Fact]
    public void EventRepertoireConfiguration_HasUniqueEventIdSongIdConstraint()
    {
        // Arrange
        var entity = _context.Model.FindEntityType(typeof(EventRepertoire));

        // Act
        var index = entity?.GetIndexes()
            .FirstOrDefault(i => i.IsUnique &&
                                i.Properties.Count == 2 &&
                                i.Properties.Any(p => p.Name == nameof(EventRepertoire.EventId)) &&
                                i.Properties.Any(p => p.Name == nameof(EventRepertoire.SongId)));

        // Assert
        index.Should().NotBeNull("Unique constraint on EventId and SongId should exist");
        index!.GetDatabaseName().Should().Be("IX_EventRepertoires_EventId_SongId_Unique");
    }

    [Fact]
    public void RoleAssignmentConfiguration_HasUserIdIndex()
    {
        // Arrange
        var entity = _context.Model.FindEntityType(typeof(RoleAssignment));

        // Act
        var index = entity?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && 
                                i.Properties[0].Name == nameof(RoleAssignment.UserId));

        // Assert
        index.Should().NotBeNull("Index on UserId should exist");
        index!.GetDatabaseName().Should().Be("IX_RoleAssignments_UserId");
    }

    [Fact]
    public void RoleAssignmentConfiguration_HasPositionIndex()
    {
        // Arrange
        var entity = _context.Model.FindEntityType(typeof(RoleAssignment));

        // Act
        var index = entity?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && 
                                i.Properties[0].Name == nameof(RoleAssignment.Position));

        // Assert
        index.Should().NotBeNull("Index on Position should exist");
        index!.GetDatabaseName().Should().Be("IX_RoleAssignments_Position");
    }

    [Fact]
    public void RoleAssignmentConfiguration_HasStartYearEndYearIndex()
    {
        // Arrange
        var entity = _context.Model.FindEntityType(typeof(RoleAssignment));

        // Act
        var index = entity?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 2 &&
                                i.Properties.Any(p => p.Name == nameof(RoleAssignment.StartYear)) &&
                                i.Properties.Any(p => p.Name == nameof(RoleAssignment.EndYear)));

        // Assert
        index.Should().NotBeNull("Composite index on StartYear and EndYear should exist");
        index!.GetDatabaseName().Should().Be("IX_RoleAssignments_StartYear_EndYear");
    }

    [Fact]
    public void RoleAssignmentConfiguration_HasUniqueUserPositionYearsConstraint()
    {
        // Arrange
        var entity = _context.Model.FindEntityType(typeof(RoleAssignment));

        // Act
        var index = entity?.GetIndexes()
            .FirstOrDefault(i => i.IsUnique &&
                                i.Properties.Count == 4 &&
                                i.Properties.Any(p => p.Name == nameof(RoleAssignment.UserId)) &&
                                i.Properties.Any(p => p.Name == nameof(RoleAssignment.Position)) &&
                                i.Properties.Any(p => p.Name == nameof(RoleAssignment.StartYear)) &&
                                i.Properties.Any(p => p.Name == nameof(RoleAssignment.EndYear)));

        // Assert
        index.Should().NotBeNull("Unique constraint on UserId, Position, StartYear, and EndYear should exist");
        index!.GetDatabaseName().Should().Be("IX_RoleAssignments_UserId_Position_Years_Unique");
    }

    [Fact]
    public void ActivityConfiguration_HasReportIdIndex()
    {
        // Arrange
        var entity = _context.Model.FindEntityType(typeof(Activity));

        // Act
        var index = entity?.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && 
                                i.Properties[0].Name == nameof(Activity.ReportId));

        // Assert
        index.Should().NotBeNull("Index on ReportId should exist");
        index!.GetDatabaseName().Should().Be("IX_Activities_ReportId");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
