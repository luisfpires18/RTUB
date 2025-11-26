using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Activity entity
/// Adds indexes for common query patterns to improve performance
/// </summary>
public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        // Index for querying activities by report (most common query pattern)
        builder.HasIndex(a => a.ReportId)
            .HasDatabaseName("IX_Activities_ReportId");
    }
}
