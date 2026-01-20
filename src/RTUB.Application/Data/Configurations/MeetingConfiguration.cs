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
        // Indexes for common queries
        builder.HasIndex(m => m.Date)
            .HasDatabaseName("IX_Meetings_Date");

        // Configure the Organizer relationship to use OrganizerUserId as the foreign key
        builder.HasOne(m => m.Organizer)
            .WithMany()
            .HasForeignKey(m => m.OrganizerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure the TunoRepresentative relationship
        builder.HasOne(m => m.TunoRepresentative)
            .WithMany()
            .HasForeignKey(m => m.TunoRepresentativeUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure the DelegatedAtaWriterMember relationship
        builder.HasOne(m => m.DelegatedAtaWriterMember)
            .WithMany()
            .HasForeignKey(m => m.DelegatedAtaWriterMemberId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
