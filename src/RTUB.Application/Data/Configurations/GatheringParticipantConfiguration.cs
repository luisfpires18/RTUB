using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public sealed class GatheringParticipantConfiguration : IEntityTypeConfiguration<GatheringParticipant>
{
    public void Configure(EntityTypeBuilder<GatheringParticipant> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.GatheringId, p.UserId }).IsUnique();
        builder.Property(p => p.UserId).IsRequired().HasMaxLength(450);
        builder.HasOne(p => p.Gathering)
            .WithMany(gathering => gathering.Participants)
            .HasForeignKey(p => p.GatheringId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
