using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Rehearsal entity
/// Maps domain entity to database schema
/// </summary>
public class RehearsalConfiguration : IEntityTypeConfiguration<Rehearsal>
{
    public void Configure(EntityTypeBuilder<Rehearsal> builder)
    {
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.Date)
            .IsRequired();
        
        builder.Property(r => r.Location)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(r => r.Theme)
            .HasMaxLength(500);
        
        builder.Property(r => r.Notes)
            .HasMaxLength(1000);
        
        builder.Property(r => r.StartTime)
            .IsRequired();
        
        builder.Property(r => r.EndTime)
            .IsRequired();
        
        builder.Property(r => r.IsCanceled)
            .IsRequired();
        
        builder.Property(r => r.CreatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasMany(r => r.Attendances)
            .WithOne(a => a.Rehearsal)
            .HasForeignKey(a => a.RehearsalId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Index for fast date lookups
        builder.HasIndex(r => r.Date);
    }
}
