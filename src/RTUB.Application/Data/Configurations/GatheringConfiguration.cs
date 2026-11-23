using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Trintuna gatherings. No rows are seeded here (none on PROD); the dev seed adds the 7 almoços
/// (<c>SeedData.SeedTrintunaAsync</c>, full dataset only).
/// </summary>
public class GatheringConfiguration : IEntityTypeConfiguration<Gathering>
{
    public void Configure(EntityTypeBuilder<Gathering> builder)
    {
        builder.HasKey(g => g.Id);
        builder.HasIndex(g => g.Number).IsUnique();
        builder.Property(g => g.Place).IsRequired().HasMaxLength(200);
        builder.Property(g => g.Description).HasMaxLength(2000);
        builder.Property(g => g.OrganizerUserId).HasMaxLength(450);
        builder.HasOne(g => g.Organizer)
            .WithMany()
            .HasForeignKey(g => g.OrganizerUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
