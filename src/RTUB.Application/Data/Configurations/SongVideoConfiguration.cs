using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for SongVideo entity
/// </summary>
public class SongVideoConfiguration : IEntityTypeConfiguration<SongVideo>
{
    public void Configure(EntityTypeBuilder<SongVideo> builder)
    {
        builder.ToTable("SongVideos");

        builder.HasKey(sv => sv.Id);

        builder.Property(sv => sv.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(sv => sv.Title)
            .HasMaxLength(200);

        builder.Property(sv => sv.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sv => sv.SizeBytes)
            .IsRequired();

        builder.Property(sv => sv.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(sv => sv.CreatedByUserId)
            .IsRequired();

        builder.Property(sv => sv.CreatedAt)
            .IsRequired();

        builder.Property(sv => sv.UpdatedAt);

        // Relationships
        builder.HasOne(sv => sv.Song)
            .WithMany(s => s.Videos)
            .HasForeignKey(sv => sv.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sv => sv.CreatedByUser)
            .WithMany()
            .HasForeignKey(sv => sv.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(sv => sv.SongId);
        builder.HasIndex(sv => new { sv.SongId, sv.SortOrder });
    }
}
