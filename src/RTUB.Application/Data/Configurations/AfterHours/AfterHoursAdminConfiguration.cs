using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class AfterHoursTuningSettingConfiguration : IEntityTypeConfiguration<AfterHoursTuningSetting>
{
    public void Configure(EntityTypeBuilder<AfterHoursTuningSetting> builder)
    {
        builder.ToTable("AfterHoursTuningSettings");
        builder.Property(s => s.Key).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Value).IsRequired().HasMaxLength(64);
        builder.Property(s => s.UpdatedByUserId).HasMaxLength(450);
        builder.Property(s => s.UpdatedAtUtc).HasConversion(GameCycleConfiguration.Utc);

        // One current value per key: concurrent edits update the same row, never add a second.
        builder.HasIndex(s => s.Key).IsUnique().HasDatabaseName("IX_AfterHoursTuningSettings_Key");
    }
}

public class AfterHoursCosmeticAwardConfiguration : IEntityTypeConfiguration<AfterHoursCosmeticAward>
{
    public void Configure(EntityTypeBuilder<AfterHoursCosmeticAward> builder)
    {
        builder.ToTable("AfterHoursCosmeticAwards");
        builder.Ignore(a => a.IsActive);
        // UserId is a historical value, not a foreign key: the award outlives the account.
        builder.Property(a => a.UserId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.RecipientName).IsRequired().HasMaxLength(256);
        builder.Property(a => a.Title).IsRequired().HasMaxLength(AfterHoursCosmeticAward.TitleMaxLength);
        builder.Property(a => a.Description).HasMaxLength(AfterHoursCosmeticAward.DescriptionMaxLength);
        builder.Property(a => a.GrantedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.RevokedByUserId).HasMaxLength(450);
        builder.Property(a => a.GrantedAtUtc).HasConversion(GameCycleConfiguration.Utc);
        builder.Property(a => a.RevokedAtUtc).HasConversion(GameCycleConfiguration.Utc);

        builder.HasIndex(a => new { a.UserId, a.RevokedAtUtc });

        builder.HasOne<FiscalYear>().WithMany().HasForeignKey(a => a.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CycleArchive>().WithMany().HasForeignKey(a => a.CycleArchiveId).OnDelete(DeleteBehavior.Restrict);
    }
}
