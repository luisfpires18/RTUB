using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class PlayerCargoConfiguration : IEntityTypeConfiguration<PlayerCargo>
{
    public void Configure(EntityTypeBuilder<PlayerCargo> builder)
    {
        builder.ToTable("AfterHoursPlayerCargo", t =>
            t.HasCheckConstraint("CK_AfterHoursPlayerCargo_Quantity", "\"Quantity\" >= 0"));

        builder.HasIndex(c => new { c.PlayerCycleStateId, c.CargoType })
            .IsUnique()
            .HasDatabaseName("IX_AfterHoursPlayerCargo_State_Type");

        builder.HasOne<PlayerCycleState>()
            .WithMany(s => s.Cargo)
            .HasForeignKey(c => c.PlayerCycleStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
