using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for Enrollment entity
/// Adds indexes for common query patterns to improve performance
/// </summary>
public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.Property(e => e.CategoryAtEvent)
            .HasConversion<int?>();

        // Composite unique index for preventing duplicate enrollments
        // One user can only enroll once per event
        builder.HasIndex(e => new { e.EventId, e.UserId })
            .IsUnique()
            .HasDatabaseName("IX_Enrollments_EventId_UserId");

        // Index for user-specific enrollment queries (e.g., "show my enrollments")
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_Enrollments_UserId");

        // Index for event-specific enrollment queries (e.g., "show all attendees for event")
        builder.HasIndex(e => e.EventId)
            .HasDatabaseName("IX_Enrollments_EventId");
    }
}
