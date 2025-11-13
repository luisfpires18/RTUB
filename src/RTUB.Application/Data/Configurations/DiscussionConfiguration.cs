using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework Core configuration for Discussion entity
/// </summary>
public class DiscussionConfiguration : IEntityTypeConfiguration<Discussion>
{
    public void Configure(EntityTypeBuilder<Discussion> builder)
    {
        // Primary key
        builder.HasKey(d => d.Id);
        
        // Properties
        builder.Property(d => d.Title)
            .HasMaxLength(200);
        
        // Relationships
        builder.HasOne(d => d.Event)
            .WithOne(e => e.Discussion)
            .HasForeignKey<Discussion>(d => d.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(d => d.Posts)
            .WithOne(p => p.Discussion)
            .HasForeignKey(p => p.DiscussionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        builder.HasIndex(d => d.EventId);
    }
}
