using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class BuyerContractConfiguration : IEntityTypeConfiguration<BuyerContract>
{
    public void Configure(EntityTypeBuilder<BuyerContract> builder)
    {
        builder.ToTable("AfterHoursBuyerContracts", t =>
            t.HasCheckConstraint("CK_AfterHoursBuyerContracts_Window", "\"ExpiresAtUtc\" > \"AvailableFromUtc\""));

        builder.Property(c => c.TemplateKey).IsRequired().HasMaxLength(16);
        builder.Property(c => c.BuyerName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.RotationStartUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(c => c.AvailableFromUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(c => c.ExpiresAtUtc).HasConversion(GameCycleConfiguration.Utc);

        // One contract per slot per window per cycle: concurrent first visits cannot duplicate a rotation.
        builder.HasIndex(c => new { c.GameCycleId, c.RotationStartUtc, c.Slot })
            .IsUnique()
            .HasDatabaseName("IX_AfterHoursBuyerContracts_Cycle_Window_Slot");

        builder.HasOne<GameCycle>()
            .WithMany()
            .HasForeignKey(c => c.GameCycleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BuyerContractCompletionConfiguration : IEntityTypeConfiguration<BuyerContractCompletion>
{
    public void Configure(EntityTypeBuilder<BuyerContractCompletion> builder)
    {
        builder.ToTable("AfterHoursBuyerContractCompletions");

        builder.Property(c => c.CompletedAtUtc).HasConversion(GameCycleConfiguration.Utc);

        // A player delivers a contract at most once; other players deliver it independently.
        builder.HasIndex(c => new { c.BuyerContractId, c.PlayerCycleStateId })
            .IsUnique()
            .HasDatabaseName("IX_AfterHoursBuyerContractCompletions_Contract_State");

        builder.HasIndex(c => c.PlayerCycleStateId);

        builder.HasOne<BuyerContract>()
            .WithMany()
            .HasForeignKey(c => c.BuyerContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlayerCycleState>()
            .WithMany()
            .HasForeignKey(c => c.PlayerCycleStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
