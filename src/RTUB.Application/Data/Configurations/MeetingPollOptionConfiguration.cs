using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public sealed class MeetingPollOptionConfiguration : IEntityTypeConfiguration<MeetingPollOption>
{
    public void Configure(EntityTypeBuilder<MeetingPollOption> builder)
    {
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Text).HasMaxLength(200).IsRequired();
    }
}
