using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for EventRepertoire entity
/// Adds indexes for common query patterns to improve performance
/// </summary>
public class EventRepertoireConfiguration : IEntityTypeConfiguration<EventRepertoire>
{
    public void Configure(EntityTypeBuilder<EventRepertoire> builder)
    {
        // Composite index for querying repertoire by event (most common query)
        builder.HasIndex(er => new { er.EventId, er.DisplayOrder })
            .HasDatabaseName("IX_EventRepertoires_EventId_DisplayOrder");

        // Index for querying which events use a specific song
        builder.HasIndex(er => er.SongId)
            .HasDatabaseName("IX_EventRepertoires_SongId");

        // Unique constraint: one song can only appear once per event
        builder.HasIndex(er => new { er.EventId, er.SongId })
            .IsUnique()
            .HasDatabaseName("IX_EventRepertoires_EventId_SongId_Unique");
    }
}
