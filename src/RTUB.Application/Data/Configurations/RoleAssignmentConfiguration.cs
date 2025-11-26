using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for RoleAssignment entity
/// Adds indexes for common query patterns to improve performance
/// </summary>
public class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        // Index for querying role assignments by user (common query pattern)
        builder.HasIndex(ra => ra.UserId)
            .HasDatabaseName("IX_RoleAssignments_UserId");

        // Index for querying role assignments by position
        builder.HasIndex(ra => ra.Position)
            .HasDatabaseName("IX_RoleAssignments_Position");

        // Composite index for finding current role assignments by year range
        builder.HasIndex(ra => new { ra.StartYear, ra.EndYear })
            .HasDatabaseName("IX_RoleAssignments_StartYear_EndYear");

        // Unique constraint: one person can only have one specific position per fiscal year period
        builder.HasIndex(ra => new { ra.UserId, ra.Position, ra.StartYear, ra.EndYear })
            .IsUnique()
            .HasDatabaseName("IX_RoleAssignments_UserId_Position_Years_Unique");
    }
}
