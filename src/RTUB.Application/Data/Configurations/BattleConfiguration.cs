using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTUB.Core.Entities;

namespace RTUB.Application.Data.Configurations;

/// <summary>
/// EF Core configuration for Battle entity
/// Maps domain entity to database schema
/// </summary>
public class BattleConfiguration : IEntityTypeConfiguration<Battle>
{
    public void Configure(EntityTypeBuilder<Battle> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.AttackerCharacterId)
            .IsRequired();

        builder.Property(b => b.DefenderCharacterId)
            .IsRequired();

        builder.Property(b => b.Seed)
            .IsRequired();

        builder.Property(b => b.Outcome)
            .IsRequired()
            .HasConversion<int>(); // Store enum as int

        builder.Property(b => b.AttackerXP)
            .IsRequired();

        builder.Property(b => b.DefenderXP)
            .IsRequired();

        builder.Property(b => b.AttackerFidelis)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(b => b.DefenderFidelis)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(b => b.ReplayJson)
            .IsRequired()
            .HasColumnType("TEXT"); // SQLite TEXT type for JSON

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        // Indexes for common queries
        builder.HasIndex(b => b.AttackerCharacterId)
            .HasDatabaseName("IX_Battles_AttackerCharacterId");

        builder.HasIndex(b => b.DefenderCharacterId)
            .HasDatabaseName("IX_Battles_DefenderCharacterId");

        builder.HasIndex(b => b.CreatedAt)
            .HasDatabaseName("IX_Battles_CreatedAt");

        // Relationships
        builder.HasOne(b => b.Attacker)
            .WithMany()
            .HasForeignKey(b => b.AttackerCharacterId)
            .OnDelete(DeleteBehavior.Restrict); // Don't delete battle if character is deleted (for history)

        builder.HasOne(b => b.Defender)
            .WithMany()
            .HasForeignKey(b => b.DefenderCharacterId)
            .OnDelete(DeleteBehavior.Restrict); // Don't delete battle if character is deleted (for history)
    }
}
