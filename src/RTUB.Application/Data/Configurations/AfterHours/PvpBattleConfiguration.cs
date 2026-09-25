using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class PvpBattleConfiguration : IEntityTypeConfiguration<PvpBattle>
{
    public void Configure(EntityTypeBuilder<PvpBattle> builder)
    {
        builder.ToTable("AfterHoursPvpBattles");

        builder.Property(b => b.AttackerUserId).IsRequired().HasMaxLength(450);
        builder.Property(b => b.DefenderUserId).IsRequired().HasMaxLength(450);
        foreach (var key in new[] { nameof(PvpBattle.AttackerWeaponKey), nameof(PvpBattle.AttackerOutfitKey), nameof(PvpBattle.AttackerVehicleToolKey),
                     nameof(PvpBattle.DefenderWeaponKey), nameof(PvpBattle.DefenderOutfitKey), nameof(PvpBattle.DefenderVehicleToolKey) })
            builder.Property<string?>(key).HasMaxLength(16);

        builder.Property(b => b.AcceptedAtUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(b => b.AttackerCooldownUntilUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(b => b.AttackerRecoveryUntilUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(b => b.DefenderRecoveryUntilUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(b => b.DefenderProtectedUntilUtc).HasConversion(GameCycleConfiguration.Utc);

        // Same-target 24-hour rule and "my outgoing battles".
        builder.HasIndex(b => new { b.AttackerStateId, b.DefenderStateId, b.AcceptedAtUtc });
        // "My incoming battles".
        builder.HasIndex(b => new { b.DefenderStateId, b.AcceptedAtUtc });

        // The battle points at its receipt (not the other way round, so the receipts table is not
        // altered): one battle per receipt, written in the same transaction.
        builder.HasOne(b => b.Receipt)
            .WithOne(r => r.PvpBattle)
            .HasForeignKey<PvpBattle>(b => b.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GameCycle>().WithMany().HasForeignKey(b => b.GameCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlayerCycleState>().WithMany().HasForeignKey(b => b.AttackerStateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PlayerCycleState>().WithMany().HasForeignKey(b => b.DefenderStateId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Rounds).WithOne().HasForeignKey(r => r.PvpBattleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Cargo).WithOne().HasForeignKey(c => c.PvpBattleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PvpBattleRoundConfiguration : IEntityTypeConfiguration<PvpBattleRound>
{
    public void Configure(EntityTypeBuilder<PvpBattleRound> builder)
    {
        builder.ToTable("AfterHoursPvpBattleRounds");
        builder.HasIndex(r => new { r.PvpBattleId, r.Round }).IsUnique();
    }
}

public class PvpBattleCargoConfiguration : IEntityTypeConfiguration<PvpBattleCargo>
{
    public void Configure(EntityTypeBuilder<PvpBattleCargo> builder)
    {
        builder.ToTable("AfterHoursPvpBattleCargo", t =>
            t.HasCheckConstraint("CK_AfterHoursPvpBattleCargo_Quantity", "\"Quantity\" > 0"));
        builder.HasIndex(c => new { c.PvpBattleId, c.CargoType }).IsUnique();
    }
}
