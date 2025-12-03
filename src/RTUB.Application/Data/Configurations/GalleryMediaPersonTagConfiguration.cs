using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for GalleryMediaPersonTag entity
/// </summary>
public class GalleryMediaPersonTagConfiguration : IEntityTypeConfiguration<GalleryMediaPersonTag>
{
    public void Configure(EntityTypeBuilder<GalleryMediaPersonTag> builder)
    {
        builder.ToTable("GalleryMediaPersonTags");

        builder.HasKey(pt => pt.Id);

        builder.Property(pt => pt.GalleryMediaId)
            .IsRequired();

        builder.Property(pt => pt.UserId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length

        builder.Property(pt => pt.CreatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(pt => pt.GalleryMedia)
            .WithMany(gm => gm.PeopleInMedia)
            .HasForeignKey(pt => pt.GalleryMediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pt => pt.User)
            .WithMany()
            .HasForeignKey(pt => pt.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance
        builder.HasIndex(pt => pt.GalleryMediaId)
            .HasDatabaseName("IX_GalleryMediaPersonTag_GalleryMediaId");

        builder.HasIndex(pt => pt.UserId)
            .HasDatabaseName("IX_GalleryMediaPersonTag_UserId");

        // Unique constraint to prevent duplicate tags
        builder.HasIndex(pt => new { pt.GalleryMediaId, pt.UserId })
            .IsUnique()
            .HasDatabaseName("IX_GalleryMediaPersonTag_GalleryMediaId_UserId_Unique");
    }
}
