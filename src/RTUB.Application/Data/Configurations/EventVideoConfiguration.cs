using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for EventVideo entity
/// </summary>
public class EventVideoConfiguration : IEntityTypeConfiguration<EventVideo>
{
    public void Configure(EntityTypeBuilder<EventVideo> builder)
    {
        builder.ToTable("EventVideos");

        builder.HasKey(ev => ev.Id);

        builder.Property(ev => ev.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(ev => ev.Title)
            .HasMaxLength(200);

        builder.Property(ev => ev.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ev => ev.SizeBytes)
            .IsRequired();

        builder.Property(ev => ev.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(ev => ev.CreatedByUserId)
            .IsRequired();

        builder.Property(ev => ev.CreatedAt)
            .IsRequired();

        builder.Property(ev => ev.UpdatedAt);

        // Relationships
        builder.HasOne(ev => ev.Event)
            .WithMany(e => e.Videos)
            .HasForeignKey(ev => ev.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ev => ev.CreatedByUser)
            .WithMany()
            .HasForeignKey(ev => ev.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(ev => ev.EventId);
        builder.HasIndex(ev => new { ev.EventId, ev.SortOrder });
    }
}
