using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for Post entity
/// </summary>
public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        // Primary key
        builder.HasKey(p => p.Id);
        
        // Properties
        builder.Property(p => p.UserId)
            .IsRequired()
            .HasMaxLength(450);
        
        builder.Property(p => p.Content)
            .IsRequired()
            .HasMaxLength(5000);
        
        // Relationships
        builder.HasOne(p => p.Discussion)
            .WithMany(d => d.Posts)
            .HasForeignKey(p => p.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(p => p.Comments)
            .WithOne(c => c.Post)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        builder.HasIndex(p => p.DiscussionId);
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.CreatedAt);
    }
}
