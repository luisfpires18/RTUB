using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public sealed class MeetingPollConfiguration : IEntityTypeConfiguration<MeetingPoll>
{
    public void Configure(EntityTypeBuilder<MeetingPoll> builder)
    {
        builder.HasKey(poll => poll.Id);
        builder.Property(poll => poll.Question).HasMaxLength(300).IsRequired();
        builder.HasIndex(poll => new { poll.Audience, poll.CreatedAt });
        builder.HasMany(poll => poll.Options)
            .WithOne(option => option.MeetingPoll)
            .HasForeignKey(option => option.MeetingPollId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
