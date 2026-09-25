using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

/// <summary>Yearbook: immutable snapshots of finished cycles. Unique indexes make archiving idempotent.</summary>
public class CycleArchiveConfiguration : IEntityTypeConfiguration<CycleArchive>
{
    public void Configure(EntityTypeBuilder<CycleArchive> builder)
    {
        builder.ToTable("AfterHoursCycleArchives");

        builder.Property(a => a.FiscalYearLabel).IsRequired().HasMaxLength(16);
        builder.Property(a => a.StartUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(a => a.EndUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(a => a.ArchivedAtUtc).HasConversion(GameCycleConfiguration.Utc);

        // Exactly one archive per cycle, and a cycle is started by at most one rollover.
        builder.HasIndex(a => a.GameCycleId).IsUnique().HasDatabaseName("IX_AfterHoursCycleArchives_Cycle");
        builder.HasIndex(a => a.NextGameCycleId).IsUnique().HasDatabaseName("IX_AfterHoursCycleArchives_NextCycle");

        builder.HasOne<GameCycle>().WithMany().HasForeignKey(a => a.GameCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GameCycle>().WithMany().HasForeignKey(a => a.NextGameCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FiscalYear>().WithMany().HasForeignKey(a => a.FiscalYearId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Players).WithOne().HasForeignKey(p => p.CycleArchiveId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Families).WithOne().HasForeignKey(f => f.CycleArchiveId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class YearbookPlayerEntryConfiguration : IEntityTypeConfiguration<YearbookPlayerEntry>
{
    public void Configure(EntityTypeBuilder<YearbookPlayerEntry> builder)
    {
        builder.ToTable("AfterHoursYearbookPlayers");

        builder.Property(p => p.UserId).IsRequired().HasMaxLength(450);
        builder.Property(p => p.DisplayName).IsRequired().HasMaxLength(256);
        builder.Property(p => p.FamilyName).HasMaxLength(24);

        builder.HasIndex(p => new { p.CycleArchiveId, p.UserId }).IsUnique().HasDatabaseName("IX_AfterHoursYearbookPlayers_Archive_User");
        builder.HasIndex(p => p.UserId);

        builder.HasOne<Family>().WithMany().HasForeignKey(p => p.FamilyId).OnDelete(DeleteBehavior.Restrict);
        // UserId is a historical value, not a foreign key: the entry outlives the account.
    }
}

public class YearbookFamilyEntryConfiguration : IEntityTypeConfiguration<YearbookFamilyEntry>
{
    public void Configure(EntityTypeBuilder<YearbookFamilyEntry> builder)
    {
        builder.ToTable("AfterHoursYearbookFamilies");

        builder.Property(f => f.FamilyName).IsRequired().HasMaxLength(24);

        builder.HasIndex(f => new { f.CycleArchiveId, f.FamilyId }).IsUnique().HasDatabaseName("IX_AfterHoursYearbookFamilies_Archive_Family");

        builder.HasOne<Family>().WithMany().HasForeignKey(f => f.FamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(f => f.Members).WithOne().HasForeignKey(m => m.YearbookFamilyEntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class YearbookFamilyMemberConfiguration : IEntityTypeConfiguration<YearbookFamilyMember>
{
    public void Configure(EntityTypeBuilder<YearbookFamilyMember> builder)
    {
        builder.ToTable("AfterHoursYearbookFamilyMembers");

        builder.Property(m => m.UserId).IsRequired().HasMaxLength(450);
        builder.Property(m => m.DisplayName).IsRequired().HasMaxLength(256);

        // UserId is a historical value, not a foreign key: the roster entry outlives the account.
        builder.HasIndex(m => new { m.YearbookFamilyEntryId, m.UserId }).IsUnique().HasDatabaseName("IX_AfterHoursYearbookFamilyMembers_Entry_User");
    }
}
