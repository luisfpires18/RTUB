using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;
namespace RTUB.Application.Data.Configurations;
public class SongPlayAlongFavoriteConfiguration : IEntityTypeConfiguration<SongPlayAlongFavorite>
{
    public void Configure(EntityTypeBuilder<SongPlayAlongFavorite> builder)
    {
        builder.ToTable("SongPlayAlongFavorites");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.UserId).IsRequired();
        builder.Property(f => f.CreatedAt).IsRequired();
        builder.HasOne(f => f.Song)
            .WithMany()
            .HasForeignKey(f => f.SongId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        // One star per member per song.
        builder.HasIndex(f => new { f.UserId, f.SongId }).IsUnique();
    }
}
