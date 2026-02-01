using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Folder entity
/// Maps domain entity to database schema
/// </summary>
public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.NormalizedKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.IsSpecial)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(f => f.SpecialVisibility)
            .IsRequired(false);

        builder.Property(f => f.CreatedByUserId)
            .HasMaxLength(256);

        builder.Property(f => f.CreatedByUserName)
            .HasMaxLength(256);

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(f => f.NormalizedKey)
            .HasDatabaseName("IX_Folders_NormalizedKey");

        builder.HasIndex(f => f.IsSpecial)
            .HasDatabaseName("IX_Folders_IsSpecial");

        // Relationships
        builder.HasMany(f => f.Documents)
            .WithOne(d => d.Folder)
            .HasForeignKey(d => d.FolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.FolderViewers)
            .WithOne(fv => fv.Folder)
            .HasForeignKey(fv => fv.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
