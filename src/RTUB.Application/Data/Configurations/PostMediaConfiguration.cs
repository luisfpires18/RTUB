using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for PostMedia entity
/// </summary>
public class PostMediaConfiguration : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> builder)
    {
        builder.ToTable("PostMedia");

        builder.HasKey(pm => pm.Id);

        builder.Property(pm => pm.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(pm => pm.MediaType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(pm => pm.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(pm => pm.SizeBytes)
            .IsRequired();

        builder.Property(pm => pm.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(pm => pm.CreatedAt)
            .IsRequired();

        builder.Property(pm => pm.UpdatedAt);

        // Relationships
        builder.HasOne(pm => pm.Post)
            .WithMany(p => p.Media)
            .HasForeignKey(pm => pm.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(pm => pm.PostId);
        builder.HasIndex(pm => new { pm.PostId, pm.SortOrder });
    }
}
