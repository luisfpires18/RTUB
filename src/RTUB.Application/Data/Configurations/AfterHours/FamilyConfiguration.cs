using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

/// <summary>Persistent family identity: no cycle foreign key.</summary>
public class FamilyConfiguration : IEntityTypeConfiguration<Family>
{
    public void Configure(EntityTypeBuilder<Family> builder)
    {
        builder.ToTable("AfterHoursFamilies");
        builder.Ignore(f => f.IsDisbanded);

        builder.Property(f => f.Name).IsRequired().HasMaxLength(24);
        builder.Property(f => f.NormalizedName).IsRequired().HasMaxLength(24);
        builder.Property(f => f.Motto).HasMaxLength(120);
        builder.Property(f => f.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(f => f.CreatedAtUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(f => f.DisbandedAtUtc).HasConversion(GameCycleConfiguration.Utc);

        // Case-insensitive uniqueness through the normalized name; disbanded names stay reserved.
        builder.HasIndex(f => f.NormalizedName).IsUnique().HasDatabaseName("IX_AfterHoursFamilies_NormalizedName");
    }
}

/// <summary>Persistent membership history: no cycle foreign key.</summary>
public class FamilyMembershipConfiguration : IEntityTypeConfiguration<FamilyMembership>
{
    public void Configure(EntityTypeBuilder<FamilyMembership> builder)
    {
        builder.ToTable("AfterHoursFamilyMemberships");
        builder.Ignore(m => m.IsActive);

        builder.Property(m => m.UserId).IsRequired().HasMaxLength(450);
        builder.Property(m => m.JoinedAtUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(m => m.LeftAtUtc).HasConversion(GameCycleConfiguration.Utc);

        // At most one active family per user.
        builder.HasIndex(m => m.UserId).IsUnique().HasFilter("\"LeftAtUtc\" IS NULL")
            .HasDatabaseName("IX_AfterHoursFamilyMemberships_ActiveUser");
        // At most one active Boss per family (the transfer demotes, saves, then promotes).
        builder.HasIndex(m => m.FamilyId).IsUnique().HasFilter($"\"LeftAtUtc\" IS NULL AND \"Role\" = {(int)FamilyRole.Boss}")
            .HasDatabaseName("IX_AfterHoursFamilyMemberships_ActiveBoss");
        builder.HasIndex(m => new { m.FamilyId, m.LeftAtUtc });
        builder.HasIndex(m => new { m.UserId, m.LeftAtUtc });

        builder.HasOne(m => m.Family).WithMany().HasForeignKey(m => m.FamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FamilyInvitationConfiguration : IEntityTypeConfiguration<FamilyInvitation>
{
    public void Configure(EntityTypeBuilder<FamilyInvitation> builder)
    {
        builder.ToTable("AfterHoursFamilyInvitations");

        builder.Property(i => i.InvitedUserId).IsRequired().HasMaxLength(450);
        builder.Property(i => i.InvitedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(i => i.CreatedAtUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(i => i.ResolvedAtUtc).HasConversion(GameCycleConfiguration.Utc);

        // One pending invitation per family and player; history rows are unconstrained.
        builder.HasIndex(i => new { i.FamilyId, i.InvitedUserId }).IsUnique()
            .HasFilter($"\"Status\" = {(int)FamilyInvitationStatus.Pending}")
            .HasDatabaseName("IX_AfterHoursFamilyInvitations_PendingPerFamilyUser");
        builder.HasIndex(i => new { i.InvitedUserId, i.Status });

        builder.HasOne<Family>().WithMany().HasForeignKey(i => i.FamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.InvitedUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Annual family state: one row per family and cycle.</summary>
public class FamilyCycleStateConfiguration : IEntityTypeConfiguration<FamilyCycleState>
{
    public void Configure(EntityTypeBuilder<FamilyCycleState> builder)
    {
        builder.ToTable("AfterHoursFamilyCycleStates", t =>
            t.HasCheckConstraint("CK_AfterHoursFamilyCycleStates_Treasury", "\"TreasuryCash\" >= 0"));

        builder.HasIndex(s => new { s.FamilyId, s.GameCycleId }).IsUnique()
            .HasDatabaseName("IX_AfterHoursFamilyCycleStates_Family_Cycle");

        builder.HasOne<Family>().WithMany().HasForeignKey(s => s.FamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GameCycle>().WithMany().HasForeignKey(s => s.GameCycleId).OnDelete(DeleteBehavior.Restrict);
    }
}
