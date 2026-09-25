using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class PlayerObjectiveProgressConfiguration : IEntityTypeConfiguration<PlayerObjectiveProgress>
{
    public void Configure(EntityTypeBuilder<PlayerObjectiveProgress> builder)
    {
        builder.ToTable("AfterHoursPlayerObjectiveProgress", t =>
        {
            t.HasCheckConstraint("CK_AfterHoursPlayerObjectiveProgress_Progress", "\"Progress\" >= 0 AND \"Progress\" <= \"Target\"");
            t.HasCheckConstraint("CK_AfterHoursPlayerObjectiveProgress_Awards", "\"XpAwarded\" >= 0 AND \"PointsAwarded\" >= 0");
        });
        builder.Property(r => r.ObjectiveKey).IsRequired().HasMaxLength(8);
        builder.Property(r => r.CompletedAtUtc).HasConversion(GameCycleConfiguration.Utc);

        // One instance per player, period, day/week and objective.
        builder.HasIndex(r => new { r.PlayerCycleStateId, r.Period, r.PeriodKey, r.ObjectiveKey }).IsUnique()
            .HasDatabaseName("IX_AfterHoursPlayerObjectiveProgress_Instance");
        builder.HasIndex(r => new { r.GameCycleId, r.Period, r.PeriodKey });

        builder.HasOne<GameCycle>().WithMany().HasForeignKey(r => r.GameCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlayerCycleState>().WithMany().HasForeignKey(r => r.PlayerCycleStateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FamilyObjectiveProgressConfiguration : IEntityTypeConfiguration<FamilyObjectiveProgress>
{
    public void Configure(EntityTypeBuilder<FamilyObjectiveProgress> builder)
    {
        builder.ToTable("AfterHoursFamilyObjectiveProgress", t =>
        {
            t.HasCheckConstraint("CK_AfterHoursFamilyObjectiveProgress_Progress", "\"Progress\" >= 0 AND \"Progress\" <= \"Target\"");
            t.HasCheckConstraint("CK_AfterHoursFamilyObjectiveProgress_Points", "\"PointsAwarded\" >= 0");
        });
        builder.Property(r => r.ObjectiveKey).IsRequired().HasMaxLength(8);
        builder.Property(r => r.CompletedAtUtc).HasConversion(GameCycleConfiguration.Utc);

        builder.HasIndex(r => new { r.FamilyId, r.GameCycleId, r.Week, r.ObjectiveKey }).IsUnique()
            .HasDatabaseName("IX_AfterHoursFamilyObjectiveProgress_Instance");
        builder.HasIndex(r => new { r.GameCycleId, r.Week });

        builder.HasOne<Family>().WithMany().HasForeignKey(r => r.FamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GameCycle>().WithMany().HasForeignKey(r => r.GameCycleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PvpObjectiveCreditConfiguration : IEntityTypeConfiguration<PvpObjectiveCredit>
{
    public void Configure(EntityTypeBuilder<PvpObjectiveCredit> builder)
    {
        builder.ToTable("AfterHoursPvpObjectiveCredits", t =>
            t.HasCheckConstraint("CK_AfterHoursPvpObjectiveCredits_Points", "\"IndividualPoints\" >= 0"));
        builder.Property(c => c.DefenderUserId).IsRequired().HasMaxLength(450);

        builder.HasIndex(c => c.PvpBattleId).IsUnique();
        // A target counts once per attacker per week, and once per family per week.
        builder.HasIndex(c => new { c.GameCycleId, c.Week, c.AttackerStateId, c.DefenderUserId }).IsUnique()
            .HasFilter("\"CountsForIndividual\" = 1")
            .HasDatabaseName("IX_AfterHoursPvpObjectiveCredits_IndividualTarget");
        builder.HasIndex(c => new { c.GameCycleId, c.Week, c.FamilyId, c.DefenderUserId }).IsUnique()
            .HasFilter("\"CountsForFamily\" = 1")
            .HasDatabaseName("IX_AfterHoursPvpObjectiveCredits_FamilyTarget");

        builder.HasOne(c => c.PvpBattle).WithMany().HasForeignKey(c => c.PvpBattleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GameCycle>().WithMany().HasForeignKey(c => c.GameCycleId).OnDelete(DeleteBehavior.Restrict);
    }
}
