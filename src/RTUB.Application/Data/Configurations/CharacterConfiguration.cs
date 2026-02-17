using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Character entity
/// Maps domain entity to database schema
/// </summary>
public class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(450); // Standard Identity user ID length

        builder.Property(c => c.Level)
            .IsRequired();

        builder.Property(c => c.XP)
            .IsRequired();

        builder.Property(c => c.HP)
            .IsRequired();

        builder.Property(c => c.Power)
            .IsRequired();

        builder.Property(c => c.Speed)
            .IsRequired();

        builder.Property(c => c.CriticalChance)
            .IsRequired();

        builder.Property(c => c.HpUpgrades)
            .IsRequired();

        builder.Property(c => c.PowerUpgrades)
            .IsRequired();

        builder.Property(c => c.SpeedUpgrades)
            .IsRequired();

        builder.Property(c => c.CriticalUpgrades)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(c => c.UserId)
            .IsUnique()
            .HasDatabaseName("IX_Characters_UserId"); // One character per user

        builder.HasIndex(c => c.Level)
            .HasDatabaseName("IX_Characters_Level"); // For matchmaking queries

        // Relationship
        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade); // Delete character when user is deleted

        // One-to-one: Character has optional Appearance
        builder.HasOne(c => c.Appearance)
            .WithOne(a => a.Character)
            .HasForeignKey<CharacterAppearance>(a => a.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
