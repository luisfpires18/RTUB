using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Post entity
/// </summary>
public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.DiscussionId)
            .IsRequired();
        
        builder.Property(p => p.AuthorId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length
        
        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(120);
        
        builder.Property(p => p.Body)
            .IsRequired();
        
        builder.Property(p => p.LastActivityAt)
            .IsRequired();
        
        builder.Property(p => p.CreatedAt)
            .IsRequired();
        
        builder.Property(p => p.IsEdited)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.Property(p => p.IsPinned)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.Property(p => p.IsLocked)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.Property(p => p.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.Property(p => p.MentionsJson)
            .HasMaxLength(2000);
        
        builder.Property(p => p.RowVersion)
            .IsRowVersion();
        
        // Relationships
        builder.HasOne(p => p.Discussion)
            .WithMany(d => d.Posts)
            .HasForeignKey(p => p.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(p => p.Comments)
            .WithOne(c => c.Post)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes for performance
        builder.HasIndex(p => new { p.DiscussionId, p.LastActivityAt })
            .HasDatabaseName("IX_Post_DiscussionId_LastActivity");
        
        builder.HasIndex(p => p.IsPinned);
        
        builder.HasIndex(p => p.IsDeleted);
    }
}
