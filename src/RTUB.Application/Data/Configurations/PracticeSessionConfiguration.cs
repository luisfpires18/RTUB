using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for PracticeSession entity
/// </summary>
public class PracticeSessionConfiguration : IEntityTypeConfiguration<PracticeSession>
{
    public void Configure(EntityTypeBuilder<PracticeSession> builder)
    {
        builder.ToTable("PracticeSessions");

        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(ps => ps.InstrumentType)
            .IsRequired();

        builder.Property(ps => ps.StartTime)
            .IsRequired();

        builder.Property(ps => ps.EndTime);

        builder.Property(ps => ps.DurationMinutes)
            .HasDefaultValue(0);

        builder.Property(ps => ps.SongId);

        builder.Property(ps => ps.Notes)
            .HasMaxLength(500);

        builder.Property(ps => ps.XpAwarded)
            .HasDefaultValue(0);

        builder.Property(ps => ps.CreatedAt)
            .IsRequired();

        builder.Property(ps => ps.UpdatedAt);

        // Relationships
        builder.HasOne(ps => ps.User)
            .WithMany()
            .HasForeignKey(ps => ps.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ps => ps.Song)
            .WithMany()
            .HasForeignKey(ps => ps.SongId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for query optimization
        builder.HasIndex(ps => new { ps.UserId, ps.StartTime })
            .HasDatabaseName("IX_PracticeSessions_UserId_StartTime");

        builder.HasIndex(ps => ps.InstrumentType)
            .HasDatabaseName("IX_PracticeSessions_InstrumentType");
    }
}
