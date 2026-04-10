using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for EventContact entity.
/// </summary>
public class EventContactConfiguration : IEntityTypeConfiguration<EventContact>
{
    public void Configure(EntityTypeBuilder<EventContact> builder)
    {
        // Composite unique index — one contact record per user per event
        builder.HasIndex(e => new { e.EventId, e.UserId })
            .IsUnique()
            .HasDatabaseName("IX_EventContacts_EventId_UserId");

        builder.HasIndex(e => e.EventId)
            .HasDatabaseName("IX_EventContacts_EventId");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_EventContacts_UserId");
    }
}
