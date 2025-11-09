using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Event entity
/// Maps domain entity to database schema
/// </summary>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(e => e.Description)
            .HasMaxLength(1000);
        
        builder.Property(e => e.Location)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(e => e.ImageUrl)
            .HasMaxLength(500);
        
        builder.Property(e => e.ImageUrl)
            .HasMaxLength(500);
        
        builder.Property(e => e.Date)
            .IsRequired();
        
        builder.Property(e => e.Type)
            .IsRequired();
        
        builder.Property(e => e.CreatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasMany(e => e.Enrollments)
            .WithOne(en => en.Event)
            .HasForeignKey(en => en.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
