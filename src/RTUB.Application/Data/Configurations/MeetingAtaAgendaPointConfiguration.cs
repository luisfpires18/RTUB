using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for MeetingAtaAgendaPoint entity
/// </summary>
public class MeetingAtaAgendaPointConfiguration : IEntityTypeConfiguration<MeetingAtaAgendaPoint>
{
    public void Configure(EntityTypeBuilder<MeetingAtaAgendaPoint> builder)
    {
        // Index on MeetingAtaId for fast queries
        builder.HasIndex(p => p.MeetingAtaId)
            .HasDatabaseName("IX_MeetingAtaAgendaPoints_MeetingAtaId");

        // MeetingAta relationship
        builder.HasOne(p => p.MeetingAta)
            .WithMany(a => a.AgendaPoints)
            .HasForeignKey(p => p.MeetingAtaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
