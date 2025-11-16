using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for LeaderboardCommentLike entity
/// </summary>
public class LeaderboardCommentLikeConfiguration : IEntityTypeConfiguration<LeaderboardCommentLike>
{
    public void Configure(EntityTypeBuilder<LeaderboardCommentLike> builder)
    {
        builder.HasKey(l => l.Id);
        
        builder.Property(l => l.CommentId)
            .IsRequired();
        
        builder.Property(l => l.UserId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length
        
        builder.Property(l => l.CreatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasOne(l => l.Comment)
            .WithMany(c => c.Likes)
            .HasForeignKey(l => l.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Unique constraint: a user can like a comment only once
        builder.HasIndex(l => new { l.CommentId, l.UserId })
            .IsUnique()
            .HasDatabaseName("IX_LeaderboardCommentLike_CommentId_UserId_Unique");
        
        // Index for performance when querying by user
        builder.HasIndex(l => l.UserId)
            .HasDatabaseName("IX_LeaderboardCommentLike_UserId");
    }
}
