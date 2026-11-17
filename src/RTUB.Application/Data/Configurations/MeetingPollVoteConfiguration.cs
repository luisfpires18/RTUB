using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public sealed class MeetingPollVoteConfiguration : IEntityTypeConfiguration<MeetingPollVote>
{
    public void Configure(EntityTypeBuilder<MeetingPollVote> builder)
    {
        builder.HasKey(vote => vote.Id);
        builder.HasIndex(vote => new { vote.MeetingPollId, vote.UserId }).IsUnique();
        builder.HasOne(vote => vote.MeetingPoll)
            .WithMany(poll => poll.Votes)
            .HasForeignKey(vote => vote.MeetingPollId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(vote => vote.Option)
            .WithMany(option => option.Votes)
            .HasForeignKey(vote => vote.MeetingPollOptionId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
