using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for AuditLog entity
/// Adds indexes for common query patterns to improve performance
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        // Index for filtering by timestamp (common query pattern for audit log retrieval)
        builder.HasIndex(al => al.Timestamp)
            .HasDatabaseName("IX_AuditLogs_Timestamp");

        // Composite index for entity-specific queries (e.g., "show history for Event #5")
        builder.HasIndex(al => new { al.EntityType, al.EntityId })
            .HasDatabaseName("IX_AuditLogs_EntityType_EntityId");

        // Index for filtering by user (e.g., "show all actions by user X")
        builder.HasIndex(al => al.UserName)
            .HasDatabaseName("IX_AuditLogs_UserName");

        // Index for critical actions filtering
        builder.HasIndex(al => al.IsCriticalAction)
            .HasDatabaseName("IX_AuditLogs_IsCriticalAction");
    }
}
