using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for MeetingParticipation entity
/// Maps domain entity to database schema
/// </summary>
public class MeetingParticipationConfiguration : IEntityTypeConfiguration<MeetingParticipation>
{
    public void Configure(EntityTypeBuilder<MeetingParticipation> builder)
    {
        builder.HasKey(mp => mp.Id);

        builder.Property(mp => mp.MeetingId)
            .IsRequired();

        builder.Property(mp => mp.UserId)
            .IsRequired();

        builder.Property(mp => mp.WillAttend)
            .IsRequired();

        builder.Property(mp => mp.ParticipatedAt)
            .IsRequired();

        builder.Property(mp => mp.Notes)
            .HasMaxLength(500);

        builder.Property(mp => mp.CreatedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(mp => mp.User)
            .WithMany()
            .HasForeignKey(mp => mp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mp => mp.Meeting)
            .WithMany()
            .HasForeignKey(mp => mp.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // One participation per user per meeting
        builder.HasIndex(mp => new { mp.MeetingId, mp.UserId })
            .IsUnique()
            .HasDatabaseName("IX_MeetingParticipations_MeetingId_UserId");

        // Index for user-specific participation queries
        builder.HasIndex(mp => mp.UserId)
            .HasDatabaseName("IX_MeetingParticipations_UserId");

        // Index for meeting-specific participation queries
        builder.HasIndex(mp => mp.MeetingId)
            .HasDatabaseName("IX_MeetingParticipations_MeetingId");
    }
}
