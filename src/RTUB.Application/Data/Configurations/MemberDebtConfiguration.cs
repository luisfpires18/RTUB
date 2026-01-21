using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MemberDebt entity
/// Configures relationships and indexes for optimal query performance
/// </summary>
public class MemberDebtConfiguration : IEntityTypeConfiguration<MemberDebt>
{
    public void Configure(EntityTypeBuilder<MemberDebt> builder)
    {
        // Configure table name
        builder.ToTable("MemberDebts");

        // Configure foreign key relationship to ApplicationUser
        builder.HasOne(md => md.User)
            .WithMany()
            .HasForeignKey(md => md.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure foreign key relationship to FiscalYear
        builder.HasOne(md => md.FiscalYear)
            .WithMany()
            .HasForeignKey(md => md.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index for filtering debts by user
        builder.HasIndex(md => md.UserId)
            .HasDatabaseName("IX_MemberDebts_UserId");

        // Index for filtering debts by fiscal year
        builder.HasIndex(md => md.FiscalYearId)
            .HasDatabaseName("IX_MemberDebts_FiscalYearId");

        // Composite index for user and fiscal year queries (common pattern)
        // Marked as unique to enforce business rule: one debt per user per fiscal year
        builder.HasIndex(md => new { md.UserId, md.FiscalYearId })
            .IsUnique()
            .HasDatabaseName("IX_MemberDebts_UserId_FiscalYearId");
    }
}
