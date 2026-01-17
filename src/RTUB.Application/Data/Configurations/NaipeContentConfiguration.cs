using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for NaipeContent entity
/// </summary>
public class NaipeContentConfiguration : IEntityTypeConfiguration<NaipeContent>
{
    public void Configure(EntityTypeBuilder<NaipeContent> builder)
    {
        builder.ToTable("NaipeContents");

        builder.HasKey(nc => nc.Id);

        builder.Property(nc => nc.InstrumentType)
            .IsRequired();

        builder.Property(nc => nc.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(nc => nc.Description)
            .HasMaxLength(1000);

        builder.Property(nc => nc.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(nc => nc.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(nc => nc.IsVideo)
            .IsRequired();

        builder.Property(nc => nc.SortOrder)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(nc => nc.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(nc => nc.CreatedAt)
            .IsRequired();

        builder.Property(nc => nc.UpdatedAt);

        // Relationships
        builder.HasOne(nc => nc.CreatedByUser)
            .WithMany()
            .HasForeignKey(nc => nc.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(nc => nc.Comments)
            .WithOne(c => c.NaipeContent)
            .HasForeignKey(c => c.NaipeContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(nc => nc.PlayCounts)
            .WithOne(pc => pc.NaipeContent)
            .HasForeignKey(pc => pc.NaipeContentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(nc => nc.InstrumentType)
            .HasDatabaseName("IX_NaipeContent_InstrumentType");

        builder.HasIndex(nc => new { nc.InstrumentType, nc.SortOrder })
            .HasDatabaseName("IX_NaipeContent_InstrumentType_SortOrder");

        builder.HasIndex(nc => nc.CreatedByUserId)
            .HasDatabaseName("IX_NaipeContent_CreatedByUserId");
    }
}
