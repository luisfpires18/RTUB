using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public sealed class GatheringPollVoteConfiguration : IEntityTypeConfiguration<GatheringPollVote>
{
    public void Configure(EntityTypeBuilder<GatheringPollVote> builder)
    {
        builder.HasKey(vote => vote.Id);
        // Task 166: one vote per member per date (a member may choose several dates of one gathering).
        builder.HasIndex(vote => new { vote.GatheringId, vote.UserId });
        builder.HasIndex(vote => new { vote.GatheringPollOptionId, vote.UserId }).IsUnique();
        builder.Property(vote => vote.UserId).IsRequired().HasMaxLength(450);
        builder.HasOne(vote => vote.Gathering)
            .WithMany(gathering => gathering.PollVotes)
            .HasForeignKey(vote => vote.GatheringId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(vote => vote.Option)
            .WithMany(option => option.Votes)
            .HasForeignKey(vote => vote.GatheringPollOptionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
