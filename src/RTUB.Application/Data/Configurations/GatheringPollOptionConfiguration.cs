using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public sealed class GatheringPollOptionConfiguration : IEntityTypeConfiguration<GatheringPollOption>
{
    public void Configure(EntityTypeBuilder<GatheringPollOption> builder)
    {
        builder.HasKey(option => option.Id);
        builder.HasIndex(option => new { option.GatheringId, option.Date }).IsUnique();
        builder.HasOne(option => option.Gathering)
            .WithMany(gathering => gathering.PollOptions)
            .HasForeignKey(option => option.GatheringId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
