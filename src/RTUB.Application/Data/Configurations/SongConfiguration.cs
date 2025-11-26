using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Song entity
/// Adds indexes for common query patterns to improve performance
/// </summary>
public class SongConfiguration : IEntityTypeConfiguration<Song>
{
    public void Configure(EntityTypeBuilder<Song> builder)
    {
        // Index for querying songs by album (common query pattern)
        builder.HasIndex(s => s.AlbumId)
            .HasDatabaseName("IX_Songs_AlbumId");

        // Index for track number ordering within albums
        builder.HasIndex(s => new { s.AlbumId, s.TrackNumber })
            .HasDatabaseName("IX_Songs_AlbumId_TrackNumber");
    }
}
