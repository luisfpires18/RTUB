using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for FolderViewer entity
/// Maps domain entity to database schema
/// </summary>
public class FolderViewerConfiguration : IEntityTypeConfiguration<FolderViewer>
{
    public void Configure(EntityTypeBuilder<FolderViewer> builder)
    {
        builder.HasKey(fv => fv.Id);

        builder.Property(fv => fv.FolderId)
            .IsRequired();

        builder.Property(fv => fv.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(fv => fv.UserName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(fv => fv.CreatedByUserId)
            .HasMaxLength(256);

        builder.Property(fv => fv.CreatedByUserName)
            .HasMaxLength(256);

        builder.Property(fv => fv.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(fv => fv.FolderId)
            .HasDatabaseName("IX_FolderViewers_FolderId");

        builder.HasIndex(fv => fv.UserId)
            .HasDatabaseName("IX_FolderViewers_UserId");

        // Composite index for unique constraint: one viewer permission per user per folder
        builder.HasIndex(fv => new { fv.FolderId, fv.UserId })
            .IsUnique()
            .HasDatabaseName("IX_FolderViewers_FolderId_UserId");

        // Relationship
        builder.HasOne(fv => fv.Folder)
            .WithMany(f => f.FolderViewers)
            .HasForeignKey(fv => fv.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
