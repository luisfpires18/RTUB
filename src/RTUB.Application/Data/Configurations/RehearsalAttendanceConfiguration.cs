using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for RehearsalAttendance entity
/// Maps domain entity to database schema
/// </summary>
public class RehearsalAttendanceConfiguration : IEntityTypeConfiguration<RehearsalAttendance>
{
    public void Configure(EntityTypeBuilder<RehearsalAttendance> builder)
    {
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.RehearsalId)
            .IsRequired();
        
        builder.Property(a => a.UserId)
            .IsRequired();
        
        builder.Property(a => a.Attended)
            .IsRequired();
        
        builder.Property(a => a.CheckedInAt)
            .IsRequired();
        
        builder.Property(a => a.Notes)
            .HasMaxLength(500);
        
        builder.Property(a => a.CreatedAt)
            .IsRequired();
        
        // Relationships
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // One attendance per user per rehearsal
        builder.HasIndex(a => new { a.RehearsalId, a.UserId })
            .IsUnique();
    }
}
