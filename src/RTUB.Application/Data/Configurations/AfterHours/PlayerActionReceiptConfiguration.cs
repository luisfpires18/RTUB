using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class PlayerActionReceiptConfiguration : IEntityTypeConfiguration<PlayerActionReceipt>
{
    public void Configure(EntityTypeBuilder<PlayerActionReceipt> builder)
    {
        builder.ToTable("AfterHoursPlayerActionReceipts");

        builder.Property(r => r.IdempotencyKey).IsRequired().HasMaxLength(64);
        builder.Property(r => r.Request).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CrimeId).HasMaxLength(8);
        builder.Property(r => r.JailUntilUtc).HasConversion(GameCycleConfiguration.Utc);

        // One accepted action per key per player state; the backstop behind the in-transaction lookup.
        builder.HasIndex(r => new { r.PlayerCycleStateId, r.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("IX_AfterHoursPlayerActionReceipts_State_Key");

        builder.HasOne(r => r.PlayerCycleState)
            .WithMany()
            .HasForeignKey(r => r.PlayerCycleStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
