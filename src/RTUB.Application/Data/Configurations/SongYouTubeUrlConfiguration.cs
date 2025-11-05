using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for SongYouTubeUrl entity
/// </summary>
public class SongYouTubeUrlConfiguration : IEntityTypeConfiguration<SongYouTubeUrl>
{
    public void Configure(EntityTypeBuilder<SongYouTubeUrl> builder)
    {
        builder.ToTable("SongYouTubeUrls");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Description)
            .HasMaxLength(200);

        // Configure relationship with Song
        builder.HasOne(e => e.Song)
            .WithMany(s => s.YouTubeUrls)
            .HasForeignKey(e => e.SongId)
            .OnDelete(DeleteBehavior.Cascade); // Cascade delete when song is deleted

        // Create index on SongId for better query performance
        builder.HasIndex(e => e.SongId);
    }
}
