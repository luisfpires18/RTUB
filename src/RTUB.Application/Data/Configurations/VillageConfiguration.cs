using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Village and related entities.
/// </summary>
public class VillageConfiguration : IEntityTypeConfiguration<Village>
{
    public void Configure(EntityTypeBuilder<Village> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(v => v.Kingdom)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Arkazia");

        builder.Property(v => v.PosX).IsRequired();
        builder.Property(v => v.PosY).IsRequired();
        builder.Property(v => v.LastTick).IsRequired();
        builder.Property(v => v.Wood).IsRequired();
        builder.Property(v => v.Stone).IsRequired();
        builder.Property(v => v.Food).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();

        builder.HasIndex(v => v.UserId)
            .IsUnique()
            .HasDatabaseName("IX_Villages_UserId");

        builder.HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Fields)
            .WithOne(f => f.Village)
            .HasForeignKey(f => f.VillageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Buildings)
            .WithOne(b => b.Village)
            .HasForeignKey(b => b.VillageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Troops)
            .WithOne(t => t.Village)
            .HasForeignKey(t => t.VillageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Missions)
            .WithOne(m => m.Village)
            .HasForeignKey(m => m.VillageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.ActiveBuildJob)
            .WithOne(j => j.Village)
            .HasForeignKey<VillageBuildJob>(j => j.VillageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VillageFieldConfiguration : IEntityTypeConfiguration<VillageField>
{
    public void Configure(EntityTypeBuilder<VillageField> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.VillageId).IsRequired();
        builder.Property(f => f.ResourceType).IsRequired();
        builder.Property(f => f.SlotIndex).IsRequired();
        builder.Property(f => f.Level).IsRequired().HasDefaultValue(0);

        builder.HasIndex(f => new { f.VillageId, f.ResourceType, f.SlotIndex })
            .IsUnique()
            .HasDatabaseName("IX_VillageFields_Village_Resource_Slot");
    }
}

public class VillageBuildingConfiguration : IEntityTypeConfiguration<VillageBuilding>
{
    public void Configure(EntityTypeBuilder<VillageBuilding> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.VillageId).IsRequired();
        builder.Property(b => b.BuildingType).IsRequired();
        builder.Property(b => b.Level).IsRequired().HasDefaultValue(0);

        builder.HasIndex(b => new { b.VillageId, b.BuildingType })
            .IsUnique()
            .HasDatabaseName("IX_VillageBuildings_Village_Type");
    }
}

public class VillageBuildJobConfiguration : IEntityTypeConfiguration<VillageBuildJob>
{
    public void Configure(EntityTypeBuilder<VillageBuildJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.VillageId).IsRequired();
        builder.Property(j => j.IsFieldUpgrade).IsRequired();
        builder.Property(j => j.ToLevel).IsRequired();
        builder.Property(j => j.StartedAt).IsRequired();
        builder.Property(j => j.DurationSecs).IsRequired();
        builder.Property(j => j.FinishesAt).IsRequired();
        builder.Property(j => j.Status).IsRequired().HasDefaultValue(VillageBuildJobStatus.Active);

        builder.HasIndex(j => j.VillageId)
            .HasDatabaseName("IX_VillageBuildJobs_VillageId");
    }
}

public class VillageTroopConfiguration : IEntityTypeConfiguration<VillageTroop>
{
    public void Configure(EntityTypeBuilder<VillageTroop> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.VillageId).IsRequired();
        builder.Property(t => t.UnitType).IsRequired();
        builder.Property(t => t.Count).IsRequired().HasDefaultValue(0);

        builder.HasIndex(t => new { t.VillageId, t.UnitType })
            .IsUnique()
            .HasDatabaseName("IX_VillageTroops_Village_Unit");
    }
}

public class VillageMissionConfiguration : IEntityTypeConfiguration<VillageMission>
{
    public void Configure(EntityTypeBuilder<VillageMission> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.VillageId).IsRequired();
        builder.Property(m => m.MissionKey).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Difficulty).IsRequired();
        builder.Property(m => m.Status).IsRequired().HasDefaultValue(VillageMissionStatus.Ongoing);
        builder.Property(m => m.SentTroopsJson).IsRequired();
        builder.Property(m => m.StartedAt).IsRequired();
        builder.Property(m => m.FinishesAt).IsRequired();
        builder.Property(m => m.Claimed).IsRequired().HasDefaultValue(false);

        builder.HasIndex(m => m.VillageId)
            .HasDatabaseName("IX_VillageMissions_VillageId");
    }
}
