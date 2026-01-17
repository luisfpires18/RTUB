using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for NaipeTypeConfig entity
/// </summary>
public class NaipeTypeConfigConfiguration : IEntityTypeConfiguration<NaipeTypeConfig>
{
    public void Configure(EntityTypeBuilder<NaipeTypeConfig> builder)
    {
        builder.ToTable("NaipeTypeConfigs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InstrumentType)
            .IsRequired();

        builder.Property(x => x.PictureUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.IsVisible)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        // Unique index on InstrumentType - only one config per type
        builder.HasIndex(x => x.InstrumentType)
            .IsUnique();

        // Index for sorting
        builder.HasIndex(x => x.SortOrder);
    }
}
