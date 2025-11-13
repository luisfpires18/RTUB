using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Comment entity
/// </summary>
public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.PostId)
            .IsRequired();
        
        builder.Property(c => c.AuthorId)
            .IsRequired()
            .HasMaxLength(450); // Standard ASP.NET Identity user ID length
        
        builder.Property(c => c.Body)
            .IsRequired();
        
        builder.Property(c => c.CreatedAt)
            .IsRequired();
        
        builder.Property(c => c.IsEdited)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.Property(c => c.MentionsJson)
            .HasMaxLength(2000);
        
        builder.Property(c => c.RowVersion)
            .IsRowVersion();
        
        // Relationships
        builder.HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Indexes for performance
        builder.HasIndex(c => new { c.PostId, c.CreatedAt })
            .HasDatabaseName("IX_Comment_PostId_CreatedAt");
        
        builder.HasIndex(c => c.IsDeleted);
    }
}
