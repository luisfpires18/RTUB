using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class AlbumConfiguration : IEntityTypeConfiguration<Album>
{
    public void Configure(EntityTypeBuilder<Album> builder)
    {
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(a => a.Description)
            .HasMaxLength(500);
        
        builder.Property(a => a.ImageUrl)
            .HasMaxLength(500);
        
        // Year is optional - removed .IsRequired() to match nullable int? in entity
        
        // Relationships
        builder.HasMany(a => a.Songs)
            .WithOne(s => s.Album)
            .HasForeignKey(s => s.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
