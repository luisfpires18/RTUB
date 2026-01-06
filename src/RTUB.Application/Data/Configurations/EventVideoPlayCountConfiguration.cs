using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class EventVideoPlayCountConfiguration : IEntityTypeConfiguration<EventVideoPlayCount>
{
    public void Configure(EntityTypeBuilder<EventVideoPlayCount> builder)
    {
        builder.HasKey(evpc => evpc.Id);

        builder.Property(evpc => evpc.EventVideoId)
            .IsRequired();

        builder.Property(evpc => evpc.UserId)
            .HasMaxLength(450);

        builder.Property(evpc => evpc.PlayedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(evpc => evpc.EventVideo)
            .WithMany()
            .HasForeignKey(evpc => evpc.EventVideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(evpc => evpc.User)
            .WithMany()
            .HasForeignKey(evpc => evpc.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for performance when querying play counts by event video
        builder.HasIndex(evpc => evpc.EventVideoId);
        
        // Index for querying by user
        builder.HasIndex(evpc => evpc.UserId);
    }
}
