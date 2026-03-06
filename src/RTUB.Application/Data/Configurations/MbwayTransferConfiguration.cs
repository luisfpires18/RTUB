using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MbwayTransfer entity
/// Configures relationships and indexes for optimal query performance
/// </summary>
public class MbwayTransferConfiguration : IEntityTypeConfiguration<MbwayTransfer>
{
    public void Configure(EntityTypeBuilder<MbwayTransfer> builder)
    {
        // Configure table name
        builder.ToTable("MbwayTransfers");

        // Configure foreign key relationship to ApplicationUser
        builder.HasOne(mt => mt.Member)
            .WithMany()
            .HasForeignKey(mt => mt.MemberUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure foreign key relationship to FiscalYear
        builder.HasOne(mt => mt.FiscalYear)
            .WithMany()
            .HasForeignKey(mt => mt.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index for filtering transfers by fiscal year (most common query)
        builder.HasIndex(mt => mt.FiscalYearId)
            .HasDatabaseName("IX_MbwayTransfers_FiscalYearId");

        // Index for filtering transfers by date
        builder.HasIndex(mt => mt.Date)
            .HasDatabaseName("IX_MbwayTransfers_Date");

        // Composite index for fiscal year and date queries
        builder.HasIndex(mt => new { mt.FiscalYearId, mt.Date })
            .HasDatabaseName("IX_MbwayTransfers_FiscalYearId_Date");
    }
}
