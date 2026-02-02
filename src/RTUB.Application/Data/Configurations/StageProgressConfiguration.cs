using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for StageProgress entity
/// Maps domain entity to database schema
/// </summary>
public class StageProgressConfiguration : IEntityTypeConfiguration<StageProgress>
{
    public void Configure(EntityTypeBuilder<StageProgress> builder)
    {
        builder.HasKey(sp => sp.Id);

        builder.Property(sp => sp.UserId)
            .IsRequired()
            .HasMaxLength(450); // Standard Identity user ID length

        builder.Property(sp => sp.CurrentStage)
            .IsRequired();

        builder.Property(sp => sp.HighestStage)
            .IsRequired();

        builder.Property(sp => sp.LastCheckpoint)
            .IsRequired();

        builder.Property(sp => sp.CurrentRegion)
            .IsRequired();

        builder.Property(sp => sp.EndlessModeUnlocked)
            .IsRequired();

        builder.Property(sp => sp.TotalStagesCleared)
            .IsRequired();

        builder.Property(sp => sp.TotalMiniBossesDefeated)
            .IsRequired();

        builder.Property(sp => sp.TotalBossesDefeated)
            .IsRequired();

        builder.Property(sp => sp.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(sp => sp.UserId)
            .IsUnique()
            .HasDatabaseName("IX_StageProgresses_UserId"); // One progress per user

        builder.HasIndex(sp => sp.HighestStage)
            .HasDatabaseName("IX_StageProgresses_HighestStage"); // For leaderboard queries

        // Relationship
        builder.HasOne(sp => sp.User)
            .WithMany()
            .HasForeignKey(sp => sp.UserId)
            .OnDelete(DeleteBehavior.Cascade); // Delete progress when user is deleted
    }
}
