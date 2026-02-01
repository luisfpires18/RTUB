using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MeetingAtaConfirmation entity
/// </summary>
public class MeetingAtaConfirmationConfiguration : IEntityTypeConfiguration<MeetingAtaConfirmation>
{
    public void Configure(EntityTypeBuilder<MeetingAtaConfirmation> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.MeetingAtaId)
            .IsRequired();

        builder.Property(c => c.UserId)
            .IsRequired();

        builder.Property(c => c.Notes)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(c => c.MeetingAta)
            .WithMany(a => a.Confirmations)
            .HasForeignKey(c => c.MeetingAtaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One confirmation per user per ATA
        builder.HasIndex(c => new { c.MeetingAtaId, c.UserId })
            .IsUnique()
            .HasDatabaseName("IX_MeetingAtaConfirmations_AtaId_UserId");
    }
}
