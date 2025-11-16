using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MeetingRequest entity
/// </summary>
public class MeetingRequestConfiguration : IEntityTypeConfiguration<MeetingRequest>
{
    public void Configure(EntityTypeBuilder<MeetingRequest> builder)
    {
        // Configure the Author relationship to use AuthorUserId as the foreign key
        builder.HasOne(mr => mr.Author)
            .WithMany()
            .HasForeignKey(mr => mr.AuthorUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
