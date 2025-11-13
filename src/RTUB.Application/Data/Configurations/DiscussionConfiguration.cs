using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Discussion entity
/// </summary>
public class DiscussionConfiguration : IEntityTypeConfiguration<Discussion>
{
    public void Configure(EntityTypeBuilder<Discussion> builder)
    {
        builder.HasKey(d => d.Id);
        
        builder.Property(d => d.EventId)
            .IsRequired();
        
        builder.Property(d => d.CreatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasOne(d => d.Event)
            .WithMany()
            .HasForeignKey(d => d.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(d => d.Posts)
            .WithOne(p => p.Discussion)
            .HasForeignKey(p => p.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Index for faster lookup by event
        builder.HasIndex(d => d.EventId)
            .IsUnique(); // One discussion per event
    }
}
