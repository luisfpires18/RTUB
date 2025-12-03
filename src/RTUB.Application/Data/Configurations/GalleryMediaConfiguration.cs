using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for GalleryMedia entity
/// </summary>
public class GalleryMediaConfiguration : IEntityTypeConfiguration<GalleryMedia>
{
    public void Configure(EntityTypeBuilder<GalleryMedia> builder)
    {
        builder.ToTable("GalleryMedia");

        builder.HasKey(gm => gm.Id);

        builder.Property(gm => gm.UploaderId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length

        builder.Property(gm => gm.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(gm => gm.MediaType)
            .IsRequired();

        builder.Property(gm => gm.MediaUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(gm => gm.ThumbnailUrl)
            .HasMaxLength(2048);

        builder.Property(gm => gm.Year)
            .IsRequired();

        builder.Property(gm => gm.Month);

        builder.Property(gm => gm.Day);

        builder.Property(gm => gm.TakenAt);

        builder.Property(gm => gm.CreatedAt)
            .IsRequired();

        builder.Property(gm => gm.UpdatedAt);

        // Relationships
        builder.HasOne(gm => gm.Uploader)
            .WithMany()
            .HasForeignKey(gm => gm.UploaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(gm => gm.PeopleInMedia)
            .WithOne(pt => pt.GalleryMedia)
            .HasForeignKey(pt => pt.GalleryMediaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(gm => gm.UploaderId)
            .HasDatabaseName("IX_GalleryMedia_UploaderId");

        builder.HasIndex(gm => gm.Year)
            .HasDatabaseName("IX_GalleryMedia_Year");

        builder.HasIndex(gm => new { gm.Year, gm.CreatedAt })
            .HasDatabaseName("IX_GalleryMedia_Year_CreatedAt");
    }
}
