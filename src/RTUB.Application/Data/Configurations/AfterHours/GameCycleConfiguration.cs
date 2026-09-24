using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Application.Data.Configurations.AfterHours;

public class GameCycleConfiguration : IEntityTypeConfiguration<GameCycle>
{
    /// <summary>
    /// SQLite stores DateTime without a kind; every After Hours timestamp is written as UTC, so
    /// it is read back as UTC instead of Unspecified.
    /// </summary>
    internal static readonly ValueConverter<DateTime, DateTime> Utc =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<GameCycle> builder)
    {
        builder.ToTable("AfterHoursGameCycles", t =>
            t.HasCheckConstraint("CK_AfterHoursGameCycles_EndAfterStart", "\"EndUtc\" > \"StartUtc\""));

        builder.Property(c => c.StartUtc).HasConversion(Utc);
        builder.Property(c => c.EndUtc).HasConversion(Utc);

        builder.HasOne(c => c.FiscalYear)
            .WithMany()
            .HasForeignKey(c => c.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.FiscalYearId);

        // At most one Active cycle: the authoritative "current game" is never ambiguous.
        builder.HasIndex(c => c.Status)
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)GameCycleStatus.Active}")
            .HasDatabaseName("IX_AfterHoursGameCycles_SingleActive");
    }
}
