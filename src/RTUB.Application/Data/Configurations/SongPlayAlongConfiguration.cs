using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class SongPlayAlongConfiguration : IEntityTypeConfiguration<SongPlayAlong>
{
    public void Configure(EntityTypeBuilder<SongPlayAlong> builder)
    {
        builder.ToTable("SongPlayAlongs");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Chart).IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasOne(p => p.Song)
            .WithMany()
            .HasForeignKey(p => p.SongId)
            .OnDelete(DeleteBehavior.Cascade);

        // One chart per song.
        builder.HasIndex(p => p.SongId).IsUnique();
    }
}
