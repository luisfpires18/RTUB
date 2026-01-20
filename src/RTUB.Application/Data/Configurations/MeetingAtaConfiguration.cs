using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MeetingAta entity
/// </summary>
public class MeetingAtaConfiguration : IEntityTypeConfiguration<MeetingAta>
{
    public void Configure(EntityTypeBuilder<MeetingAta> builder)
    {
        // Indexes
        builder.HasIndex(a => a.MeetingId)
            .HasDatabaseName("IX_MeetingAtas_MeetingId")
            .IsUnique();

        // Meeting relationship (One-to-One)
        builder.HasOne(a => a.Meeting)
            .WithOne(m => m.Ata)
            .HasForeignKey<MeetingAta>(a => a.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // User relationships
        builder.HasOne(a => a.PresidentUser)
            .WithMany()
            .HasForeignKey(a => a.PresidentUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(a => a.FirstSecretaryUser)
            .WithMany()
            .HasForeignKey(a => a.FirstSecretaryUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(a => a.SecondSecretaryUser)
            .WithMany()
            .HasForeignKey(a => a.SecondSecretaryUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
