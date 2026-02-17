using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for CharacterAppearance entity.
/// Configures the one-to-one relationship with Character and unique index.
/// </summary>
public class CharacterAppearanceConfiguration : IEntityTypeConfiguration<CharacterAppearance>
{
    public void Configure(EntityTypeBuilder<CharacterAppearance> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CharacterId)
            .IsRequired();

        builder.Property(a => a.HairColor)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(a => a.EyeColor)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(a => a.SkinColor)
            .IsRequired()
            .HasMaxLength(7);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        // Unique index — one appearance per character
        builder.HasIndex(a => a.CharacterId)
            .IsUnique()
            .HasDatabaseName("IX_CharacterAppearances_CharacterId");
    }
}
