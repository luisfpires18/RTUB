using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class PlayerCycleStateConfiguration : IEntityTypeConfiguration<PlayerCycleState>
{
    public void Configure(EntityTypeBuilder<PlayerCycleState> builder)
    {
        builder.ToTable("AfterHoursPlayerCycleStates", t =>
        {
            t.HasCheckConstraint("CK_AfterHoursPlayerCycleStates_WalletCash", "\"WalletCash\" >= 0");
            t.HasCheckConstraint("CK_AfterHoursPlayerCycleStates_BankCash", "\"BankCash\" >= 0");
            t.HasCheckConstraint("CK_AfterHoursPlayerCycleStates_Energy", "\"Energy\" >= 0");
            t.HasCheckConstraint("CK_AfterHoursPlayerCycleStates_Heat", "\"Heat\" >= 0");
        });

        builder.Property(s => s.UserId)
            .IsRequired()
            .HasMaxLength(450); // Standard Identity user ID length

        builder.Property(s => s.EnergyUpdatedAtUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(s => s.HeatUpdatedAtUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(s => s.JailUntilUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(s => s.EquippedWeaponKey).HasMaxLength(16);
        builder.Property(s => s.EquippedOutfitKey).HasMaxLength(16);
        builder.Property(s => s.EquippedVehicleToolKey).HasMaxLength(16);
        builder.Property(s => s.PvpInitiatedAtUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(s => s.PvpProtectedUntilUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(s => s.PvpRecoveryUntilUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(s => s.PvpCooldownUntilUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(s => s.DefenceWeaponKey).HasMaxLength(16);
        builder.Property(s => s.DefenceOutfitKey).HasMaxLength(16);
        builder.Property(s => s.DefenceVehicleToolKey).HasMaxLength(16);

        // One annual state per player per cycle; also what makes first-time creation race-safe.
        builder.HasIndex(s => new { s.GameCycleId, s.UserId })
            .IsUnique()
            .HasDatabaseName("IX_AfterHoursPlayerCycleStates_Cycle_User");

        builder.HasIndex(s => s.UserId);

        // A cycle with players is history: finish it, never delete it.
        builder.HasOne(s => s.GameCycle)
            .WithMany()
            .HasForeignKey(s => s.GameCycleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade); // Game data goes with the account, as MyTuno's does
    }
}
