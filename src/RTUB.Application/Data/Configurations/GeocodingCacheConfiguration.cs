using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

public class GeocodingCacheConfiguration : IEntityTypeConfiguration<GeocodingCache>
{
    public void Configure(EntityTypeBuilder<GeocodingCache> builder)
    {
        builder.HasKey(g => g.Id);
        
        builder.Property(g => g.CityName)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(g => g.CountryCode)
            .IsRequired()
            .HasMaxLength(10);
        
        builder.Property(g => g.Latitude)
            .IsRequired();
        
        builder.Property(g => g.Longitude)
            .IsRequired();
        
        builder.Property(g => g.Source)
            .HasMaxLength(50);
        
        // Create unique index on CityName + CountryCode for fast lookups and prevent duplicates
        builder.HasIndex(g => new { g.CityName, g.CountryCode })
            .IsUnique();
    }
}
