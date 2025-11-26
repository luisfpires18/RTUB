using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for LeaderboardComment entity
/// </summary>
public class LeaderboardCommentConfiguration : IEntityTypeConfiguration<LeaderboardComment>
{
    public void Configure(EntityTypeBuilder<LeaderboardComment> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TargetUserId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length

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
        builder.HasOne(c => c.TargetUser)
            .WithMany()
            .HasForeignKey(c => c.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Likes)
            .WithOne(l => l.Comment)
            .HasForeignKey(l => l.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(c => new { c.TargetUserId, c.DeletedAt, c.CreatedAt })
            .HasDatabaseName("IX_LeaderboardComment_TargetUserId_DeletedAt_CreatedAt");

        builder.HasIndex(c => c.AuthorId)
            .HasDatabaseName("IX_LeaderboardComment_AuthorId");
    }
}
