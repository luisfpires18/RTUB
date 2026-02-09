using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for BossModeProgress entity.
/// Maps domain entity to database schema.
/// </summary>
public class BossModeProgressConfiguration : IEntityTypeConfiguration<BossModeProgress>
{
    public void Configure(EntityTypeBuilder<BossModeProgress> builder)
    {
        builder.HasKey(bp => bp.Id);

        builder.Property(bp => bp.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(bp => bp.CurrentBossStage)
            .IsRequired();

        builder.Property(bp => bp.HighestBossStage)
            .IsRequired();

        builder.Property(bp => bp.TotalBossStagesCleared)
            .IsRequired();

        builder.Property(bp => bp.TotalFitabSpent)
            .IsRequired();

        builder.Property(bp => bp.TotalRunsAttempted)
            .IsRequired();

        builder.Property(bp => bp.DailyBossStage)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(bp => bp.DailyBossRemainingHP)
            .IsRequired(false);

        builder.Property(bp => bp.DailyBossMaxHP)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(bp => bp.LastDailyResetDate)
            .IsRequired();

        builder.Property(bp => bp.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(bp => bp.UserId)
            .IsUnique()
            .HasDatabaseName("IX_BossModeProgresses_UserId");

        builder.HasIndex(bp => bp.HighestBossStage)
            .HasDatabaseName("IX_BossModeProgresses_HighestBossStage");

        // Relationship
        builder.HasOne(bp => bp.User)
            .WithMany()
            .HasForeignKey(bp => bp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
