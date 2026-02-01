using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Document entity
/// Maps domain entity to database schema
/// </summary>
public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FolderId)
            .IsRequired();

        builder.Property(d => d.DisplayName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.CloudflareUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.SizeBytes)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasMaxLength(100);

        builder.Property(d => d.CreatedByUserId)
            .HasMaxLength(256);

        builder.Property(d => d.CreatedByUserName)
            .HasMaxLength(256);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(d => d.FolderId)
            .HasDatabaseName("IX_Documents_FolderId");

        builder.HasIndex(d => d.ObjectKey)
            .HasDatabaseName("IX_Documents_ObjectKey");

        builder.HasIndex(d => d.CreatedAt)
            .HasDatabaseName("IX_Documents_CreatedAt");

        // Relationship
        builder.HasOne(d => d.Folder)
            .WithMany(f => f.Documents)
            .HasForeignKey(d => d.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
