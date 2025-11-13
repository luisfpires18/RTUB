using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for Comment entity
/// </summary>
public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        // Primary key
        builder.HasKey(c => c.Id);
        
        // Properties
        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(450);
        
        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(2000);
        
        // Relationships
        builder.HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Indexes
        builder.HasIndex(c => c.PostId);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.CreatedAt);
    }
}
