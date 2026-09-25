using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class PlayerGearConfiguration : IEntityTypeConfiguration<PlayerGear>
{
    public void Configure(EntityTypeBuilder<PlayerGear> builder)
    {
        builder.ToTable("AfterHoursPlayerGear");

        builder.Property(g => g.ItemKey).IsRequired().HasMaxLength(16);

        // An item is bought at most once per player per cycle.
        builder.HasIndex(g => new { g.PlayerCycleStateId, g.ItemKey })
            .IsUnique()
            .HasDatabaseName("IX_AfterHoursPlayerGear_State_Item");

        builder.HasOne<PlayerCycleState>()
            .WithMany(s => s.Gear)
            .HasForeignKey(g => g.PlayerCycleStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
