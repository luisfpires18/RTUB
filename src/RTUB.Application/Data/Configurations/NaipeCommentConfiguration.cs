using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for NaipeComment entity
/// </summary>
public class NaipeCommentConfiguration : IEntityTypeConfiguration<NaipeComment>
{
    public void Configure(EntityTypeBuilder<NaipeComment> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.NaipeContentId)
            .IsRequired();

        builder.Property(c => c.AuthorId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.Text)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.DeletedAt);

        // Relationships
        builder.HasOne(c => c.NaipeContent)
            .WithMany(nc => nc.Comments)
            .HasForeignKey(c => c.NaipeContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance
        builder.HasIndex(c => new { c.NaipeContentId, c.DeletedAt, c.CreatedAt })
            .HasDatabaseName("IX_NaipeComment_NaipeContentId_DeletedAt_CreatedAt");

        builder.HasIndex(c => c.AuthorId)
            .HasDatabaseName("IX_NaipeComment_AuthorId");
    }
}
