using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MeetingAtaAttachment entity
/// </summary>
public class MeetingAtaAttachmentConfiguration : IEntityTypeConfiguration<MeetingAtaAttachment>
{
    public void Configure(EntityTypeBuilder<MeetingAtaAttachment> builder)
    {
        // Index on MeetingAtaId for fast queries
        builder.HasIndex(a => a.MeetingAtaId)
            .HasDatabaseName("IX_MeetingAtaAttachments_MeetingAtaId");

        // MeetingAta relationship
        builder.HasOne(a => a.MeetingAta)
            .WithMany(ata => ata.Attachments)
            .HasForeignKey(a => a.MeetingAtaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
