using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for Transaction entity
/// Adds indexes for common query patterns to improve performance
/// </summary>
public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        // Configure foreign key relationship to ApplicationUser
        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Composite index for filtering transactions by activity and type
        // Common pattern: "show all Income transactions for Activity X"
        builder.HasIndex(t => new { t.ActivityId, t.Type })
            .HasDatabaseName("IX_Transactions_ActivityId_Type");

        // Index for filtering by date (report generation, date range queries)
        builder.HasIndex(t => t.Date)
            .HasDatabaseName("IX_Transactions_Date");

        // Index for filtering transactions by user (CALOTES tracking)
        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("IX_Transactions_UserId");
    }
}
