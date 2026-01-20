using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for BetComment entity
/// </summary>
public class BetCommentConfiguration : IEntityTypeConfiguration<BetComment>
{
    public void Configure(EntityTypeBuilder<BetComment> builder)
    {
        builder.ToTable("BetComments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Text)
            .HasMaxLength(1000);

        builder.Property(c => c.MediaUrl)
            .HasMaxLength(500);

        builder.Property(c => c.MediaType)
            .HasMaxLength(10);

        builder.Property(c => c.AuthorId)
            .IsRequired();

        builder.Property(c => c.BetId)
            .IsRequired();

        // Relationships
        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Bet)
            .WithMany()
            .HasForeignKey(c => c.BetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => c.BetId);
        builder.HasIndex(c => c.AuthorId);
        builder.HasIndex(c => c.CreatedAt);
    }
}
