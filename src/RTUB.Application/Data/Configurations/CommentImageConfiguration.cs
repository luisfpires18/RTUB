using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for CommentImage entity
/// </summary>
public class CommentImageConfiguration : IEntityTypeConfiguration<CommentImage>
{
    public void Configure(EntityTypeBuilder<CommentImage> builder)
    {
        builder.ToTable("CommentImages");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(ci => ci.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ci => ci.SizeBytes)
            .IsRequired();

        builder.Property(ci => ci.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(ci => ci.CreatedAt)
            .IsRequired();

        builder.Property(ci => ci.UpdatedAt);

        // Relationships
        builder.HasOne(ci => ci.Comment)
            .WithMany(c => c.Images)
            .HasForeignKey(ci => ci.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(ci => ci.CommentId);
        builder.HasIndex(ci => new { ci.CommentId, ci.SortOrder });
    }
}
