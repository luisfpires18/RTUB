using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for Meeting entity
/// </summary>
public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        // Configure the Organizer relationship to use OrganizerUserId as the foreign key
        builder.HasOne(m => m.Organizer)
            .WithMany()
            .HasForeignKey(m => m.OrganizerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
