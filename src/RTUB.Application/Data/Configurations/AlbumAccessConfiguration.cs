using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// Entity Framework configuration for AlbumAccess entity.
/// Configures the join table between Album and ApplicationUser for exclusive album access.
/// </summary>
public class AlbumAccessConfiguration : IEntityTypeConfiguration<AlbumAccess>
{
    public void Configure(EntityTypeBuilder<AlbumAccess> builder)
    {
        builder.HasKey(aa => aa.Id);

        // Composite unique index for preventing duplicate access entries
        // One user can only have one access entry per album
        builder.HasIndex(aa => new { aa.AlbumId, aa.UserId })
            .IsUnique()
            .HasDatabaseName("IX_AlbumAccesses_AlbumId_UserId");

        // Index for user-specific access queries
        builder.HasIndex(aa => aa.UserId)
            .HasDatabaseName("IX_AlbumAccesses_UserId");

        // Index for album-specific access queries
        builder.HasIndex(aa => aa.AlbumId)
            .HasDatabaseName("IX_AlbumAccesses_AlbumId");

        builder.Property(aa => aa.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(aa => aa.AddedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(aa => aa.Album)
            .WithMany(a => a.AlbumAccesses)
            .HasForeignKey(aa => aa.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(aa => aa.User)
            .WithMany()
            .HasForeignKey(aa => aa.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
