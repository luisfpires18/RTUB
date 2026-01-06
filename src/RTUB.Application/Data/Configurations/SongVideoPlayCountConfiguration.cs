using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class SongVideoPlayCountConfiguration : IEntityTypeConfiguration<SongVideoPlayCount>
{
    public void Configure(EntityTypeBuilder<SongVideoPlayCount> builder)
    {
        builder.HasKey(svpc => svpc.Id);

        builder.Property(svpc => svpc.SongVideoId)
            .IsRequired();

        builder.Property(svpc => svpc.UserId)
            .HasMaxLength(450);

        builder.Property(svpc => svpc.PlayedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(svpc => svpc.SongVideo)
            .WithMany()
            .HasForeignKey(svpc => svpc.SongVideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(svpc => svpc.User)
            .WithMany()
            .HasForeignKey(svpc => svpc.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for performance when querying play counts by song video
        builder.HasIndex(svpc => svpc.SongVideoId);
        
        // Index for querying by user
        builder.HasIndex(svpc => svpc.UserId);
    }
}
