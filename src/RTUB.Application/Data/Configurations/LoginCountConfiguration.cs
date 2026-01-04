using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for LoginCount entity
/// Adds indexes for common query patterns to improve performance
/// </summary>
public class LoginCountConfiguration : IEntityTypeConfiguration<LoginCount>
{
    public void Configure(EntityTypeBuilder<LoginCount> builder)
    {
        // Composite unique index to prevent duplicate login counts for the same user on the same day
        // One user can only have one login count record per day
        builder.HasIndex(lc => new { lc.UserId, lc.Date })
            .IsUnique()
            .HasDatabaseName("IX_LoginCounts_UserId_Date");

        // Index for user-specific queries (e.g., "show all login history for user")
        builder.HasIndex(lc => lc.UserId)
            .HasDatabaseName("IX_LoginCounts_UserId");

        // Index for date-based queries (e.g., "show all logins on a specific date")
        builder.HasIndex(lc => lc.Date)
            .HasDatabaseName("IX_LoginCounts_Date");
    }
}
