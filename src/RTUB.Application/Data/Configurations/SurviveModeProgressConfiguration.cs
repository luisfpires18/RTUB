using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for SurviveModeProgress entity.
/// </summary>
public class SurviveModeProgressConfiguration : IEntityTypeConfiguration<SurviveModeProgress>
{
    public void Configure(EntityTypeBuilder<SurviveModeProgress> builder)
    {
        builder.HasKey(sp => sp.Id);

        builder.Property(sp => sp.UserId).IsRequired().HasMaxLength(450);
        builder.Property(sp => sp.CurrentLevel).IsRequired().HasDefaultValue(1);
        builder.Property(sp => sp.HighestLevel).IsRequired().HasDefaultValue(1);
        builder.Property(sp => sp.CurrentRegion).IsRequired();
        builder.Property(sp => sp.LongestSurvivalTime).IsRequired().HasDefaultValue(0.0);
        builder.Property(sp => sp.TotalEnemiesKilled).IsRequired().HasDefaultValue(0);
        builder.Property(sp => sp.TotalLevelsCompleted).IsRequired().HasDefaultValue(0);
        builder.Property(sp => sp.TotalRunsAttempted).IsRequired().HasDefaultValue(0);
        builder.Property(sp => sp.IsRunActive).IsRequired().HasDefaultValue(false);
        builder.Property(sp => sp.RunStartLevel).IsRequired().HasDefaultValue(1);
        builder.Property(sp => sp.RunStartedAt).IsRequired(false);
        builder.Property(sp => sp.CreatedAt).IsRequired();

        builder.HasIndex(sp => sp.UserId).IsUnique()
            .HasDatabaseName("IX_SurviveModeProgresses_UserId");
        builder.HasIndex(sp => sp.HighestLevel)
            .HasDatabaseName("IX_SurviveModeProgresses_HighestLevel");

        builder.HasOne(sp => sp.User).WithMany()
            .HasForeignKey(sp => sp.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
