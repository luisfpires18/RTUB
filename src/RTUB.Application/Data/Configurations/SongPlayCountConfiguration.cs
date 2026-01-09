using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class SongPlayCountConfiguration : IEntityTypeConfiguration<SongPlayCount>
{
    public void Configure(EntityTypeBuilder<SongPlayCount> builder)
    {
        builder.HasKey(spc => spc.Id);

        builder.Property(spc => spc.SongId)
            .IsRequired();

        builder.Property(spc => spc.UserId)
            .HasMaxLength(450);

        builder.Property(spc => spc.PlayedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(spc => spc.Song)
            .WithMany(s => s.PlayCounts)
            .HasForeignKey(spc => spc.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(spc => spc.User)
            .WithMany()
            .HasForeignKey(spc => spc.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for performance when querying play counts by song
        builder.HasIndex(spc => spc.SongId);

        // Index for querying by user
        builder.HasIndex(spc => spc.UserId);
    }
}
